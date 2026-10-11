using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CsvHelper;
using CsvHelper.Configuration;
using Server.DTOs;
using Server.Models;
using static Server.Models.Model;
using static Server.Models.Process;
using static Server.Services.FileStorageService;
using static Server.Services.ProjectService;

namespace Server.Services;

public class IaWebService : ApplicationService
{
  // Service instances are transient; share the transport to keep connections pooled.
  private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };
  private readonly string _iaUrl;
  private static readonly JsonSerializerOptions SerializeOptions = new()
  {
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) }
  };

  public IaWebService(IConfiguration configuration)
  {
    _iaUrl = (configuration[Configuration.IamlBaseUrl] ?? "http://localhost:8000").TrimEnd('/');
    if (!Uri.TryCreate(_iaUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
      throw new ArgumentException("Iaml:BaseUrl must be an absolute HTTP or HTTPS URL.");
  }

  public async Task<ResponseService<ProcessPipeline?>> GeneratePipeline()
  {
    var result = new ResponseService<ProcessPipeline?>();
    var response = await GetJson<JsonElement>("pipeline/generate");
    if (!result.EnsureOtherServiceIsSuccess(response)) return result;
    try
    {
      var study = IamlStudyCompatibility.PreparePayload(response.Data);
      result.SetSuccessful(study.Deserialize<ProcessPipeline>(SerializeOptions));
    }
    catch (Exception ex) { SetFailure(result, ex); }
    return result;
  }
  public Task<ResponseService<JsonElement>> GetHealth() => GetJson<JsonElement>("health");
  public Task<ResponseService<JsonElement>> GetCatalogue() => GetJson<JsonElement>("pipeline/catalogue");
  public Task<ResponseService<JsonElement>> ValidateConfiguration(JsonElement configuration) => PostCompatibleConfiguration("pipeline/validate", configuration);
  public Task<ResponseService<JsonElement>> EditConfiguration(JsonElement request) => PostCompatibleConfiguration("pipeline/edit", request);
  public Task<ResponseService<JsonElement>> DiffConfiguration(JsonElement request) => PostCompatibleConfiguration("pipeline/diff", request, includeBase: true);
  public async Task<ResponseService<IFormFile>> ExportConfiguration(JsonElement request, string format)
  {
    var result = new ResponseService<IFormFile>();
    // Validation resolves omitted default recipes, so JSON and Python exports both
    // use the same repaired canonical study shown in the editor.
    var validated = await ValidateConfiguration(request);
    if (!result.EnsureOtherServiceIsSuccess(validated)) return result;
    return await Download("pipeline/export?format=" + Uri.EscapeDataString(format), format == "python" ? "study.py" : "study.json",
      JsonContent(validated.Data.GetProperty("configuration")), format == "json" ? CompatibleJsonExport : null);
  }

  public async Task<ResponseService<RunPipelineOutDTO>> RunPipeline(RunPipelineFileDTO trainFile, RunPipelineFileDTO? testFile, Project project, string pipeline, int? maxDuration)
  {
    try
    {
      var configuration = StudyConfiguration(pipeline, maxDuration);
      if (!configuration.ContainsKey("pipeline"))
      {
        var validated = await ValidateConfiguration(JsonSerializer.SerializeToElement(configuration));
        if (!validated.IsSuccess)
        {
          var failed = new ResponseService<RunPipelineOutDTO>();
          failed.EnsureOtherServiceIsSuccess(validated);
          return failed;
        }
        configuration = StudyConfiguration(validated.Data.GetProperty("configuration").GetRawText());
      }
      using var content = DataContent(trainFile, testFile, project.CsvColumnToPredict, project.RowDepedencies ? project.RowDepedenciesLinked : [],
        project.DatasetHasIdColumn ? project.DatasetHasIdColumnList : [], project.SurvivalModel ? project.SurvivalModelDatasetTargetTime : null, project.SurvivalModelEventValue);
      content.Add(new StringContent(configuration.ToJsonString(), Encoding.UTF8, "application/json"), "configuration");
      var response = await SendJson<RunPipelineOutDTO>(HttpMethod.Post, "pipeline/run", content);
      if (response.IsSuccess) response.Data.ConfigurationJson = configuration.ToJsonString();
      return response;
    }
    catch (Exception ex) { return Failure<RunPipelineOutDTO>(ex); }
  }

  public async Task<ResponseService<RunPipelineOutDTO>> StartBaselineAsync(RunPipelineFileDTO trainFile, RunPipelineFileDTO? testFile, StartProjectInDTO project)
  {
    try
    {
      var configuration = StudyConfiguration(project.Pipeline);
      // A fit's search parameters are explicitly forbidden on the baseline route.
      if (configuration["fit_options"] is JsonObject fit)
      {
        fit.Remove("patience");
        fit.Remove("n_candidates");
      }
      using var content = DataContent(trainFile, testFile, project.CsvColumnToPredict, project.RowDepedencies ? project.RowDepedenciesLinked : [],
        project.DatasetHasIdColumn ? project.DatasetHasIdColumnList : [], project.SurvivalModel ? project.SurvivalModelDatasetTargetTime : null, project.SurvivalModelEventValue);
      content.Add(new StringContent(configuration.ToJsonString(), Encoding.UTF8, "application/json"), "configuration");
      return await SendJson<RunPipelineOutDTO>(HttpMethod.Post, "baseline", content);
    }
    catch (Exception ex) { return Failure<RunPipelineOutDTO>(ex); }
  }

  public Task<ResponseService<JobStatusOutDTO>> GetJobStatus(string id) => GetJson<JobStatusOutDTO>($"jobs/{Id(id)}/status?with_logs=true");
  public Task<ResponseService<CancelJobOutDTO>> CancelJob(Process process) => CancelJob(process.IaRemoteId);
  public Task<ResponseService<CancelJobOutDTO>> CancelJob(string remoteId) => SendJson<CancelJobOutDTO>(HttpMethod.Post, $"jobs/{Id(remoteId)}/cancel");
  public Task<ResponseService<GetJobMetricsOutDTO>> RetrieveJobMetric(string id) => GetJson<GetJobMetricsOutDTO>($"jobs/{Id(id)}/metrics");
  public Task<ResponseService<JsonElement>> GetJobHistory(string id) => GetJson<JsonElement>($"jobs/{Id(id)}/history");
  public Task<ResponseService<JsonElement>> GetJobAudit(string id) => GetJson<JsonElement>($"jobs/{Id(id)}/audit");
  public Task<ResponseService<ModelBibliography>> GetBibliography(string id, int rid = 0) => GetJson<ModelBibliography>($"jobs/{Id(id)}/model/{rid}/bibliography");
  public Task<ResponseService<IFormFile>> DownloadModel(string id, long rid = 0) => Download($"jobs/{Id(id)}/model/{rid}/download", "pyiaml_model.pickle");
  public Task<ResponseService<IFormFile>> DownloadModelFile(string id, int rid) => DownloadModel(id, rid);
  public Task<ResponseService<GetModelTextExplanationOutDTO>> GetTextExplanation(string id, int rid) => GetJson<GetModelTextExplanationOutDTO>($"jobs/{Id(id)}/model/{rid}/explanations");
  public Task<ResponseService<JsonElement>> GetModelReport(string id, int rid) => GetJson<JsonElement>($"jobs/{Id(id)}/model/{rid}/report");
  public Task<ResponseService<IFormFile>> GetModelMarkdownReport(string id, int rid) => Download($"jobs/{Id(id)}/model/{rid}/report?format=markdown", "model-report.md");
  public Task<ResponseService<JsonElement>> GetModelAudit(string id, int rid) => GetJson<JsonElement>($"jobs/{Id(id)}/model/{rid}/audit");
  public Task<ResponseService<JsonElement>> PredictCandidate(string id, int rid, JsonElement request) => PostJson<JsonElement>($"jobs/{Id(id)}/model/{rid}/predict", request);

  public async Task<ResponseService<JsonElement>> EvaluateCandidate(string id, int rid, Project project, RunPipelineFileDTO file)
  {
    try
    {
      // Evaluation consumes only features and targets; unlike fit it does not
      // extract groups_columns before checking the fitted model's input schema.
      var groups = project.RowDepedencies ? project.RowDepedenciesLinked : [];
      var ids = project.DatasetHasIdColumn ? project.DatasetHasIdColumnList : [];
      using var content = DataContent(file, null, project.CsvColumnToPredict, [],
        ids.Concat(groups).ToArray(), project.SurvivalModel ? project.SurvivalModelDatasetTargetTime : null, project.SurvivalModelEventValue);
      return await SendJson<JsonElement>(HttpMethod.Post, $"jobs/{Id(id)}/model/{rid}/evaluate", content);
    }
    catch (Exception ex) { return Failure<JsonElement>(ex); }
  }

  public async Task<ResponseService<ExplainModelOutDTO>> ExplainCandidate(Project project, byte[] dataset, string id, int rid, JsonElement? options = null)
  {
    try
    {
      var groups = project.RowDepedencies ? project.RowDepedenciesLinked : [];
      var ids = project.DatasetHasIdColumn ? project.DatasetHasIdColumnList : [];
      var excluded = ids.Concat(groups).Except([project.CsvColumnToPredict, project.SurvivalModelDatasetTargetTime ?? ""]);
      var filtered = FilterCsv(dataset, excluded);
      using var content = new MultipartFormDataContent
      {
        { new ByteArrayContent(filtered), "dataset", "observations.csv" },
        { new StringContent(project.CsvColumnToPredict), "dataset_target" }
      };
      if (project.SurvivalModel) AddSurvival(content, project.SurvivalModelDatasetTargetTime, project.SurvivalModelEventValue);
      if (options.HasValue) content.Add(new StringContent(options.Value.GetRawText(), Encoding.UTF8, "application/json"), "options");
      return await SendJson<ExplainModelOutDTO>(HttpMethod.Post, $"jobs/{Id(id)}/model/{rid}/explain", content);
    }
    catch (Exception ex) { return Failure<ExplainModelOutDTO>(ex); }
  }

  public async Task<ResponseService<ExplainModelOutDTO>> ExplainModel(byte[] dataset, byte[] model)
  {
    using var content = new MultipartFormDataContent
    {
      { new ByteArrayContent(dataset), "dataset", "observations.csv" },
      { new ByteArrayContent(model), "pipeline", "pyiaml_model.pickle" }
    };
    return await SendJson<ExplainModelOutDTO>(HttpMethod.Post, "explain", content);
  }

  public async Task<ResponseService<ExplainModelOutDTO>> ExplainObservation(FileStorageResponseGet model, JsonElement rows)
  {
    try
    {
      // The JSON dataset preserves codes such as "001", strings and nulls.
      // CSV would let pandas infer different values before schema conversion.
      using var content = new MultipartFormDataContent
      {
        { new ByteArrayContent(model.FileBytes!), "pipeline", "pyiaml_model.pickle" },
        { new StringContent(rows.GetRawText(), Encoding.UTF8, "application/json"), "dataset" },
        { new StringContent("{\"nsamples\":64}", Encoding.UTF8, "application/json"), "options" }
      };
      return await SendJson<ExplainModelOutDTO>(HttpMethod.Post, "explain", content);
    }
    catch (Exception ex) { return Failure<ExplainModelOutDTO>(ex); }
  }

  public async Task<ResponseService<ExplainModelOutDTO>> ExplainModel(byte[] dataset, byte[] model, Project project, JsonElement? options = null)
  {
    try
    {
      var groups = project.RowDepedencies ? project.RowDepedenciesLinked : [];
      var ids = project.DatasetHasIdColumn ? project.DatasetHasIdColumnList : [];
      var excluded = ids.Concat(groups).Except([project.CsvColumnToPredict, project.SurvivalModelDatasetTargetTime ?? ""]);
      using var content = new MultipartFormDataContent
      {
        { new ByteArrayContent(FilterCsv(dataset, excluded)), "dataset", "observations.csv" },
        { new ByteArrayContent(model), "pipeline", "pyiaml_model.pickle" },
        { new StringContent(project.CsvColumnToPredict), "dataset_target" }
      };
      if (project.SurvivalModel) AddSurvival(content, project.SurvivalModelDatasetTargetTime, project.SurvivalModelEventValue);
      if (options.HasValue) content.Add(new StringContent(options.Value.GetRawText(), Encoding.UTF8, "application/json"), "options");
      return await SendJson<ExplainModelOutDTO>(HttpMethod.Post, "explain", content);
    }
    catch (Exception ex) { return Failure<ExplainModelOutDTO>(ex); }
  }

  public Task<ResponseService<ExplainStatusOutDTO>> GetExplainStatus(string id) => GetJson<ExplainStatusOutDTO>($"explain/{Id(id)}/status");
  public Task<ResponseService<ExplainResultsOutDTO>> GetExplainResults(string id) => GetJson<ExplainResultsOutDTO>($"explain/{Id(id)}/results");
  public Task<ResponseService<GetModelFeaturesInDTO>> GetExplainFeatures(string id, string? key = null) => GetJson<GetModelFeaturesInDTO>($"explain/{Id(id)}/features" + (key == null ? "" : "?key=" + Uri.EscapeDataString(key)));
  public Task<ResponseService<Dictionary<string, Plot>>> GetExplainShapPlots(string id, string? key = null, string? plots = null, string? ps = null, string? scatterFeature = null)
  {
    var query = "?format=json";
    if (key != null) query += "&key=" + Uri.EscapeDataString(key);
    if (plots != null) query += "&plots=" + Uri.EscapeDataString(plots);
    if (ps != null) query += "&ps=" + Uri.EscapeDataString(ps);
    if (scatterFeature != null) query += "&scatter_feature=" + Uri.EscapeDataString(scatterFeature);
    return GetJson<Dictionary<string, Plot>>($"explain/{Id(id)}/shap_plots{query}");
  }

  public async Task<ResponseService<SplitOutDTO>> Split(IFormFile file, Project project)
  {
    try
    {
      // Split retains every column. Identifier columns are removed before training.
      using var content = new MultipartFormDataContent
      {
        { new ByteArrayContent(await IFormFileToByteArray(file)), "dataset", file.FileName },
        { new StringContent("0.2"), "ratio" },
        { new StringContent("42"), "random_state" }
      };
      if (project.RowDepedencies)
        content.Add(new StringContent(JsonSerializer.Serialize(project.RowDepedenciesLinked)), "groups_columns");
      return await SendJson<SplitOutDTO>(HttpMethod.Post, "split", content);
    }
    catch (Exception ex) { return Failure<SplitOutDTO>(ex); }
  }

  public Task<ResponseService<IFormFile>> GetSplitTrainFile(string id) => Download($"split/{Id(id)}/train", $"Train{id}.csv");
  public Task<ResponseService<IFormFile>> GetSplitTestFile(string id) => Download($"split/{Id(id)}/test", $"Test{id}.csv");

  public async Task<ResponseService<JsonElement>> PredictWithArtifact(FileStorageResponseGet model, JsonElement request)
  {
    try
    {
      if (request.ValueKind != JsonValueKind.Object) throw new ArgumentException("The prediction request must be a JSON object.");
      using var content = new MultipartFormDataContent
      {
        { new ByteArrayContent(model.FileBytes!), "pipeline", "pyiaml_model.pickle" }
      };
      foreach (var field in request.EnumerateObject())
      {
        if (field.Name is not ("json_row" or "method" or "target" or "times"))
          throw new ArgumentException($"Unsupported prediction field: {field.Name}.");
        if (field.Name == "method" && field.Value.ValueKind != JsonValueKind.String)
          throw new ArgumentException("The prediction method must be a string.");
        var value = field.Name == "method" ? field.Value.GetString()! : field.Value.GetRawText();
        content.Add(new StringContent(value, Encoding.UTF8, "application/json"), field.Name);
      }
      return await SendJson<JsonElement>(HttpMethod.Post, "predict", content);
    }
    catch (Exception ex) { return Failure<JsonElement>(ex); }
  }

  public async Task<ResponseService<ProjectFileFormated>> PredictValueUsingAiModel(FileStorageResponseGet modelFile, ProjectFileFormated table, string targetColumn)
  {
    var response = new ResponseService<ProjectFileFormated>();
    try
    {
      var columns = table.Columns.Where(c => c.Label != targetColumn && !c.IsColToPredict && !c.IsGroupColumn && !c.IsIdColumn).ToList();
      var rows = table.Rows.Select(row => columns.ToDictionary(c => c.Label, c => row.TryGetValue(c.Name, out var value) ? value : null)).ToArray();
      var request = JsonSerializer.SerializeToElement(new { json_row = rows, method = "predict" });
      var predicted = await PredictWithArtifact(modelFile, request);
      if (!response.EnsureOtherServiceIsSuccess(predicted)) return response;
      var values = predicted.Data.GetProperty("prediction").EnumerateArray().ToArray();
      if (values.Length != table.Rows.Count) throw new InvalidDataException("The number of predictions does not match the number of observations.");
      var outputKey = table.Columns.FirstOrDefault(c => c.Label == targetColumn)?.Name ?? targetColumn;
      for (var i = 0; i < values.Length; i++) table.Rows[i][outputKey] = values[i].Clone();
      response.SetSuccessful(table);
    }
    catch (Exception ex) { SetFailure(response, ex); }
    return response;
  }

  public async Task<ResponseService<JsonElement>> GetStatistics(IFormCollection form, bool plots = true)
  {
    try
    {
      // The creation wizard annotates the source file without changing it.
      // Apply exclusions here, as during training; this local field is not part
      // of the IAML API. Groups stay available for IAML to extract separately.
      var excluded = form.TryGetValue("excluded_columns", out var excludedValue)
        ? JsonSerializer.Deserialize<string[]>(excludedValue.ToString())
          ?? throw new ArgumentException("The list of excluded columns must be a JSON array.")
        : [];
      if (excluded.Any(string.IsNullOrWhiteSpace))
        throw new ArgumentException("Excluded columns must have non-empty names.");
      var target = form["dataset_target"].ToString();
      var time = form["dataset_target_time"].ToString();
      if (excluded.Contains(target, StringComparer.Ordinal) || (!string.IsNullOrEmpty(time) && excluded.Contains(time, StringComparer.Ordinal)))
        throw new ArgumentException("The target and follow-up time columns cannot be excluded from statistics.");
      using var content = new MultipartFormDataContent();
      foreach (var file in form.Files)
      {
        var bytes = await IFormFileToByteArray(file);
        if (file.Name == "train_dataset") bytes = FilterCsv(bytes, excluded);
        content.Add(new ByteArrayContent(bytes), file.Name, file.FileName);
      }
      foreach (var field in form)
      {
        if (field.Key == "excluded_columns") continue;
        var value = field.Value.ToString();
        if (field.Key == "configuration")
        {
          using var document = JsonDocument.Parse(value);
          value = IamlStudyCompatibility.PreparePayload(document.RootElement).GetRawText();
        }
        content.Add(new StringContent(value, Encoding.UTF8), field.Key);
      }
      return await SendJson<JsonElement>(HttpMethod.Post, "statistics?format=json&plots=" + (plots ? "true" : "false"), content);
    }
    catch (Exception ex) { return Failure<JsonElement>(ex); }
  }

  public Task<ResponseService<JsonElement>> GetStatistics(JsonElement request, bool plots = true) =>
    PostCompatibleConfiguration("statistics?format=json&plots=" + (plots ? "true" : "false"), request);

  private async Task<ResponseService<JsonElement>> PostCompatibleConfiguration(string path, JsonElement request, bool includeBase = false)
  {
    try
    {
      var prepared = IamlStudyCompatibility.PreparePayload(request, includeBase);
      var response = await PostJson<JsonElement>(path, prepared);
      if (response.IsSuccess && path is "pipeline/validate" or "pipeline/edit")
        response.Data = IamlStudyCompatibility.PreparePayload(response.Data);
      return response;
    }
    catch (Exception ex) { return Failure<JsonElement>(ex); }
  }

  private Task<ResponseService<T>> GetJson<T>(string path) => SendJson<T>(HttpMethod.Get, path);
  private Task<ResponseService<T>> PostJson<T>(string path, JsonElement request) => SendJson<T>(HttpMethod.Post, path, JsonContent(request));
  private static StringContent JsonContent(JsonElement request) => new(request.GetRawText(), Encoding.UTF8, "application/json");
  private static string Id(string id) => Uri.EscapeDataString(id);

  private async Task<ResponseService<T>> SendJson<T>(HttpMethod method, string path, HttpContent? content = null)
  {
    var result = new ResponseService<T>();
    try
    {
      using var request = new HttpRequestMessage(method, $"{_iaUrl}/{path}") { Content = content };
      using var response = await Http.SendAsync(request);
      if (!response.IsSuccessStatusCode)
      {
        await ReadError(result, response);
        return result;
      }
      var body = await response.Content.ReadAsStringAsync();
      var data = JsonSerializer.Deserialize<T>(body, SerializeOptions);
      if (data is null) throw new InvalidDataException("DrIA returned an empty response.");
      result.SetSuccessful(data);
    }
    catch (Exception ex) { SetFailure(result, ex); }
    return result;
  }

  private static byte[] CompatibleJsonExport(byte[] bytes)
  {
    using var document = JsonDocument.Parse(bytes);
    return Encoding.UTF8.GetBytes(IamlStudyCompatibility.PreparePayload(document.RootElement).GetRawText());
  }

  private async Task<ResponseService<IFormFile>> Download(string path, string fileName, HttpContent? content = null, Func<byte[], byte[]>? prepare = null)
  {
    var result = new ResponseService<IFormFile>();
    try
    {
      using var request = new HttpRequestMessage(content == null ? HttpMethod.Get : HttpMethod.Post, $"{_iaUrl}/{path}") { Content = content };
      using var response = await Http.SendAsync(request);
      if (!response.IsSuccessStatusCode)
      {
        await ReadError(result, response);
        return result;
      }
      var bytes = await response.Content.ReadAsByteArrayAsync();
      if (prepare != null) bytes = prepare(bytes);
      // Own the memory stream for the lifetime of the returned file. Do not return
      // a stream disposed with the upstream response.
      var stream = new MemoryStream(bytes, writable: false);
      var file = new FormFile(stream, 0, bytes.Length, "file", fileName)
      {
        Headers = new HeaderDictionary(),
        ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream"
      };
      result.SetSuccessful(file);
    }
    catch (Exception ex) { SetFailure(result, ex); }
    return result;
  }

  private static async Task ReadError(ResponseServiceAbstract result, HttpResponseMessage response)
  {
    result.HttpStatusCode = (int)response.StatusCode;
    var body = await response.Content.ReadAsStringAsync();
    var error = $"DrIA: HTTP {(int)response.StatusCode}.";
    try
    {
      using var json = JsonDocument.Parse(body);
      if (json.RootElement.TryGetProperty("error", out var message) && message.ValueKind == JsonValueKind.String)
        error = message.GetString()!;
    }
    catch (JsonException) { /* Upstream proxy responses need not be JSON. */ }
    result.Error = error;
    result.AddError(error);
  }

  private static ResponseService<T> Failure<T>(Exception error)
  {
    var response = new ResponseService<T>();
    SetFailure(response, error);
    return response;
  }

  private static void SetFailure(ResponseServiceAbstract result, Exception error)
  {
    result.HttpStatusCode = error is HttpRequestException or TaskCanceledException ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status400BadRequest;
    result.Error = error is HttpRequestException or TaskCanceledException ? "The DrIA service is unavailable or did not respond in time." : error.Message;
    result.AddError(result.Error);
  }

  private static JsonObject StudyConfiguration(string json, int? maxDuration = null)
  {
    if (JsonNode.Parse(json) is not JsonObject study || study["schema_version"] is not JsonValue version || !version.TryGetValue<int>(out var value) || value != 1)
      throw new ArgumentException("This configuration uses the previous DrIA format. Generate a new study with schema_version: 1.");
    IamlStudyCompatibility.PrepareStudy(study);
    if (maxDuration.HasValue)
    {
      if (maxDuration.Value is < 60 or > 3600) throw new ArgumentException("The search time limit must be between 60 and 3600 seconds.");
      if (study["options"] == null) study["options"] = new JsonObject();
      if (study["options"] is not JsonObject options) throw new ArgumentException("Study options must be a JSON object.");
      options["max_duration"] = maxDuration.Value;
      if (options["max_stage_duration"] is JsonValue stageDuration && stageDuration.TryGetValue<int>(out var stageBudget) && stageBudget > maxDuration.Value)
        options["max_stage_duration"] = maxDuration.Value;
    }
    return study;
  }

  private static MultipartFormDataContent DataContent(RunPipelineFileDTO train, RunPipelineFileDTO? test, string target, string[] groups, string[] ids, string? time, string? eventValue)
  {
    if (string.IsNullOrWhiteSpace(target)) throw new ArgumentException("The target column is required.");
    if (time == target) throw new ArgumentException("The event and time columns must be different.");
    if (groups.Any(name => name == target || name == time))
      throw new ArgumentException("Grouping columns must be different from target columns.");
    var excluded = ids.Concat(groups).Except([target, time ?? ""]).ToArray();
    // External training groups keep validation grouped while both feature files
    // share the fitted model's schema, including during independent evaluation.
    var groupFile = groups.Length > 0 ? SelectCsv(train.File, groups) : null;
    var content = new MultipartFormDataContent
    {
      { new ByteArrayContent(FilterCsv(train.File, excluded)), "train_dataset", train.FileName },
      { new StringContent(target), "dataset_target" }
    };
    if (test is { File.Length: > 0 }) content.Add(new ByteArrayContent(FilterCsv(test.File, excluded)), "test_dataset", test.FileName);
    if (groupFile != null) content.Add(new ByteArrayContent(groupFile), "groups", "groups.csv");
    if (time != null) AddSurvival(content, time, eventValue);
    return content;
  }

  private static void AddSurvival(MultipartFormDataContent content, string? time, string? eventValue)
  {
    if (string.IsNullOrWhiteSpace(time)) throw new ArgumentException("The survival time column is required.");
    content.Add(new StringContent(time), "dataset_target_time");
    if (!string.IsNullOrWhiteSpace(eventValue))
    {
      using var json = JsonDocument.Parse(eventValue);
      if (json.RootElement.ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False))
        throw new ArgumentException("The event value must be a JSON string, number or boolean.");
      content.Add(new StringContent(json.RootElement.GetRawText()), "event_value");
    }
  }

  private static byte[] FilterCsv(byte[] file, IEnumerable<string> excluded)
  {
    var excludedColumns = excluded.ToHashSet(StringComparer.Ordinal);
    if (excludedColumns.Count == 0) return file;
    return ProjectCsv(file, excludedColumns, null);
  }

  private static byte[] SelectCsv(byte[] file, string[] selected)
  {
    if (selected.Length == 0 || selected.Any(string.IsNullOrWhiteSpace) || selected.Distinct(StringComparer.Ordinal).Count() != selected.Length)
      throw new ArgumentException("Grouping column names must be unique and non-empty.");
    return ProjectCsv(file, null, selected.ToHashSet(StringComparer.Ordinal));
  }

  private static byte[] ProjectCsv(byte[] file, HashSet<string>? excluded, HashSet<string>? selected)
  {
    using var reader = new StreamReader(new MemoryStream(file));
    using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true, DetectDelimiter = true });
    if (!csv.Read() || !csv.ReadHeader()) throw new ArgumentException("The data file does not contain a header.");
    var headers = csv.HeaderRecord ?? throw new ArgumentException("The data file does not contain a header.");
    if (selected != null)
    {
      var missing = selected.Except(headers, StringComparer.Ordinal).ToArray();
      if (missing.Length > 0) throw new ArgumentException($"Grouping columns missing from the training file: {string.Join(", ", missing)}.");
    }
    var kept = headers.Select((name, index) => (name, index))
      .Where(c => selected != null ? selected.Contains(c.name) : !excluded!.Contains(c.name)).ToArray();
    using var memory = new MemoryStream();
    using var writer = new StreamWriter(memory, new UTF8Encoding(false), leaveOpen: true);
    using var output = new CsvWriter(writer, CultureInfo.InvariantCulture);
    foreach (var column in kept) output.WriteField(column.name);
    output.NextRecord();
    while (csv.Read())
    {
      foreach (var column in kept) output.WriteField(csv.GetField(column.index));
      output.NextRecord();
    }
    writer.Flush();
    return memory.ToArray();
  }

  public static async Task<byte[]> IFormFileToByteArray(IFormFile file)
  {
    await using var memory = new MemoryStream();
    await file.CopyToAsync(memory);
    return memory.ToArray();
  }
}
