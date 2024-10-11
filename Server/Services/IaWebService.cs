using System.Globalization;
using System.Text;
using System.Text.Json;
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
  // private readonly string _ia_url = "http://rss00992:32845";
  private readonly string _ia_url = "http://tmp-iias:32845";
  private readonly JsonSerializerOptions _serializeOptions = new JsonSerializerOptions
  {
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) }
  };

  async public Task<ResponseService<RunPipelineOutDTO>> RunPipeline(RunPipelineFileDTO trainFile, RunPipelineFileDTO testFile, Project project, string pipeline)
  {
    var response = new ResponseService<RunPipelineOutDTO>();
    try
    {
      using (var http = new HttpClient())
      {
        var requestContent = new MultipartFormDataContent
        {
            { new ByteArrayContent(trainFile.File), "train_dataset", trainFile.FileName },
            { new ByteArrayContent(testFile.File), "test_dataset", testFile.FileName },
            { new StringContent(project.CsvColumnToPredict), "dataset_target" },
            { new StringContent(SerializeToSnakeLowerCase(pipeline), Encoding.UTF8, "application/json"), "pipeline" }
        };
        var httpResponse = await http.PostAsync($"{_ia_url}/pipeline/run", requestContent);
        httpResponse.EnsureSuccessStatusCode();
        string httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
        response.SetSuccessful(JsonSerializer.Deserialize<RunPipelineOutDTO>(httpResponseBody, _serializeOptions)!);
      }
      return response;
    }
    catch (Exception exception)
    {
      response.AddError("Run Pipeline route : " + exception.Message);
      return response;
    }
  }

  async public Task<ResponseService<ProcessPipeline?>> GeneratePipeline()
  {
    var response = new ResponseService<ProcessPipeline?>();
    try
    {
      using (var http = new HttpClient())
      {
        var httpResponse = await http.GetAsync($"{_ia_url}/pipeline/generate");
        var jsonOptions = new JsonSerializerOptions
        {
          PropertyNameCaseInsensitive = true,
          PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };

        httpResponse.EnsureSuccessStatusCode();
        string httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
        response.SetSuccessful(JsonSerializer.Deserialize<ProcessPipeline>(httpResponseBody, jsonOptions)!);
      }
      return response;
    }
    catch (Exception exception)
    {
      response.AddError("Generate Pipeline route : " + exception.Message);
      return response;
    }
  }

  async public Task<ResponseService<JobStatusOutDTO>> GetJobStatus(string processRemoteId)
  {
    var response = new ResponseService<JobStatusOutDTO>();
    Console.WriteLine("IA Web service getting job status via api route from ia web service");
    try
    {
      using (var http = new HttpClient())
      {
        var httpResponse = await http.GetAsync($"{_ia_url}/jobs/{processRemoteId}/status");
        httpResponse.EnsureSuccessStatusCode();
        string httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
        response.SetSuccessful(JsonSerializer.Deserialize<JobStatusOutDTO>(httpResponseBody, _serializeOptions)!);
        Console.WriteLine($"Response get job status: {JsonSerializer.Deserialize<JobStatusOutDTO>(httpResponseBody, _serializeOptions)}");
      }
      return response;
    }
    catch (Exception exception)
    {
      response.AddError("Get Job Status route : " + exception.Message);
      return response;
    }
  }

  async public Task<ResponseService<CancelJobOutDTO>> CancelJob(Process process)
  {
    var response = new ResponseService<CancelJobOutDTO>();
    try
    {
      using var http = new HttpClient();
      var httpResponse = await http.PostAsync($"{_ia_url}/job/{process.Id}/cancel", null);
      httpResponse.EnsureSuccessStatusCode();
      string httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
      response.SetSuccessful(JsonSerializer.Deserialize<CancelJobOutDTO>(httpResponseBody, _serializeOptions)!);
      return response;
    }
    catch (Exception exception)
    {
      response.AddError("Cancel Job route : " + exception.Message);
      return response;
    }
  }

  async public Task<ResponseService<GetJobMetricsOutDTO>> RetrieveJobMetric(string processRemoteId)
  {
    var response = new ResponseService<GetJobMetricsOutDTO>();
    try
    {
      using var http = new HttpClient();
      var httpResponse = await http.GetAsync($"{_ia_url}/jobs/{processRemoteId}/metrics");
      string httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
      response.SetSuccessful(JsonSerializer.Deserialize<GetJobMetricsOutDTO>(httpResponseBody, _serializeOptions)!);
      return response;
    }
    catch (Exception exception)
    {
      response.AddError("Retrieve Job route : " + exception.Message);
      return response;
    }
  }

  async public Task<ResponseService<ModelBibliography>> GetBibliography(string iaRemoteProcessId, int jobMetricId = 0)
  {
    var response = new ResponseService<ModelBibliography>();
    try
    {
      using (var http = new HttpClient())
      {
        var httpResponse = await http.GetAsync($"{_ia_url}/jobs/{iaRemoteProcessId}/model/{jobMetricId}/bibliography");
        httpResponse.EnsureSuccessStatusCode();
        string httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
        response.SetSuccessful(JsonSerializer.Deserialize<ModelBibliography>(httpResponseBody)!);
      }
      return response;
    }
    catch (Exception exception)
    {
      response.AddError("Get Bibliography route : " + exception.Message);
      return response;
    }
  }

  async public Task<ResponseService<IFormFile>> DownloadModel(string iaRemoteprocessId, long jobMetricId = 0)
  {
    var response = new ResponseService<IFormFile>();
    try
    {
      using var http = new HttpClient();
      var httpResponse = await http.GetAsync($"{_ia_url}/job/{iaRemoteprocessId}/model/{jobMetricId}/download");
      httpResponse.EnsureSuccessStatusCode();
      using var stream = await httpResponse.Content.ReadAsStreamAsync();
      var formFile = new FormFile(stream, 0, stream.Length, "file", $"Job{iaRemoteprocessId}/Model{jobMetricId}.pickle")
      {
        Headers = new HeaderDictionary(),
        ContentType = httpResponse.Content.Headers.ContentType?.ToString() ?? "application/octet-stream"
      };
      response.SetSuccessful(formFile);
      return response;
    }
    catch (Exception exception)
    {
      response.AddError("Download Model route : " + exception.Message);
      return response;
    }
  }

  async public Task<ResponseService<ExplainModelOutDTO>> ExplainModel(byte[] projectTestFile, string processRemoteId, int modelRemoteId)
  {
    var response = new ResponseService<ExplainModelOutDTO>();
    try
    {
      using (var http = new HttpClient())
      {
        var requestContent = new MultipartFormDataContent
        {
            { new ByteArrayContent(projectTestFile), "dataset" },
        };
        var httpResponse = await http.PostAsync($"{_ia_url}/jobs/{processRemoteId}/model/{modelRemoteId}/explain", requestContent);
        httpResponse.EnsureSuccessStatusCode();
        string httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
        response.SetSuccessful(JsonSerializer.Deserialize<ExplainModelOutDTO>(httpResponseBody, _serializeOptions)!);
      }
      return response;
    }
    catch (Exception exception)
    {
      response.AddError("Explain Model route : " + exception.Message);
      return response;
    }
  }

  async public Task<ResponseService<GetModelTextExplanationOutDTO>> GetTextExplanation(string processRemoteId, int modelRemoteId)
  {
    var response = new ResponseService<GetModelTextExplanationOutDTO>();
    try
    {
      using (var http = new HttpClient())
      {
        var httpResponse = await http.GetAsync($"{_ia_url}/jobs/{processRemoteId}/model/{modelRemoteId}/explanations");
        httpResponse.EnsureSuccessStatusCode();
        string httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
        response.SetSuccessful(JsonSerializer.Deserialize<GetModelTextExplanationOutDTO>(httpResponseBody, _serializeOptions)!);
      }
      return response;
    }
    catch (Exception exception)
    {
      response.AddError("Get Text Explanation route : " + exception.Message);
      return response;
    }
  }

  async public Task<ResponseService<SplitOutDTO>> Split(IFormFile file, Project project)
  {
    var response = new ResponseService<SplitOutDTO>();
    byte[] formatedFile;
    if (project.RowDepedenciesLinked.Length == 0)
    {
      formatedFile = await IFormFileToByteArray(file);
    }
    else
    {
      formatedFile = await FormatFileForPipeline(file, project);
    }
    try
    {
      using (var http = new HttpClient())
      {
        var requestContent = new MultipartFormDataContent
        {
            { new ByteArrayContent(formatedFile), "dataset", file.FileName },
            { new StringContent(project.CsvColumnToPredict), "dataset_target" },
        };
        var httpResponse = await http.PostAsync($"{_ia_url}/split", requestContent);
        httpResponse.EnsureSuccessStatusCode();
        string httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
        response.SetSuccessful(JsonSerializer.Deserialize<SplitOutDTO>(httpResponseBody, _serializeOptions)!);
      }
      return response;
    }
    catch (Exception exception)
    {
      response.AddError("Split route : " + exception.Message);
      return response;
    }
  }

  async public Task<ResponseService<GetSplitStatusOutDTO>> GetSplitStatus(string splitProcessId)
  {
    var response = new ResponseService<GetSplitStatusOutDTO>();
    try
    {
      using (var http = new HttpClient())
      {
        var httpResponse = await http.GetAsync($"{_ia_url}/split/{splitProcessId}/status");
        httpResponse.EnsureSuccessStatusCode();
        string httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
        response.SetSuccessful(JsonSerializer.Deserialize<GetSplitStatusOutDTO>(httpResponseBody, _serializeOptions)!);
      }
      return response;
    }
    catch (Exception exception)
    {
      response.AddError("Get Split Status route : " + exception.Message);
      return response;
    }
  }

  async public Task<ResponseService<IFormFile>> GetSplitTrainFile(string splitProcessId)
  {
    var response = new ResponseService<IFormFile>();
    try
    {
      MemoryStream trainStream;
      using (var http = new HttpClient())
      {
        var httpResponse = await http.GetAsync($"{_ia_url}/split/{splitProcessId}/train");
        httpResponse.EnsureSuccessStatusCode();
        var content = await httpResponse.Content.ReadAsByteArrayAsync();
        trainStream = new MemoryStream(content);
        var formFile = new FormFile(baseStream: trainStream, baseStreamOffset: 0, length: content.Length, name: "", fileName: $"Train{splitProcessId}.csv")
        {
          Headers = new HeaderDictionary()
        };
        formFile.ContentType = httpResponse.Content.Headers.ContentType?.MediaType?.ToString() ?? "text/csv";
        response.SetSuccessful(formFile);
      }
      return response;
    }
    catch (Exception exception)
    {
      response.AddError("Get Split Train File route : " + exception.Message);
      return response;
    }
  }

  async public Task<ResponseService<IFormFile>> GetSplitTestFile(string splitProcessId)
  {
    var response = new ResponseService<IFormFile>();
    MemoryStream testStream;
    try
    {
      using (var http = new HttpClient())
      {
        var httpResponse = await http.GetAsync($"{_ia_url}/split/{splitProcessId}/test");
        httpResponse.EnsureSuccessStatusCode();
        var content = await httpResponse.Content.ReadAsByteArrayAsync();
        testStream = new MemoryStream(content);
        var formFile = new FormFile(baseStream: testStream, baseStreamOffset: 0, length: content.Length, name: "", fileName: $"Test{splitProcessId}.csv")
        {
          Headers = new HeaderDictionary()
        };
        formFile.ContentType = httpResponse.Content.Headers.ContentType?.MediaType?.ToString() ?? "text/csv";
        response.SetSuccessful(formFile);
        return response;
      }
    }
    catch (Exception exception)
    {
      response.AddError("Get Split Test File route : " + exception.Message);
      return response;
    }
  }

  public async Task<ResponseService<IFormFile>> DownloadModelFile(string jobId, int modelRemoteId)
  {
    var response = new ResponseService<IFormFile>();
    MemoryStream stream;
    try
    {
      using (var http = new HttpClient())
      {
        var httpResponse = await http.GetAsync($"{_ia_url}/jobs/{jobId}/model/{modelRemoteId}/download");
        httpResponse.EnsureSuccessStatusCode();
        var content = await httpResponse.Content.ReadAsByteArrayAsync();
        stream = new MemoryStream(content);
        var formFile = new FormFile(baseStream: stream, baseStreamOffset: 0, length: content.Length, name: "", fileName: $"{jobId}_{modelRemoteId}.pickle")
        {
          Headers = new HeaderDictionary()
        };
        formFile.ContentType = httpResponse.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        response.SetSuccessful(formFile);
        return response;
      }
    }
    catch (Exception exception)
    {
      response.AddError("Download Model File route : " + exception.Message);
      return response;
    }
  }

  public async Task<ResponseService<Dictionary<string, Plot>>> GetExplainShapPlots(string explainProcessRemoteId)
  {
    var response = new ResponseService<Dictionary<string, Plot>>();
    try
    {
      using (var http = new HttpClient())
      {
        var httpResponse = await http.GetAsync($"{_ia_url}/explain/{explainProcessRemoteId}/shap_plots?format=json");
        httpResponse.EnsureSuccessStatusCode();
        string httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
        response.SetSuccessful(JsonSerializer.Deserialize<Dictionary<string, Plot>>(httpResponseBody, _serializeOptions)!);
      }
      return response;
    }
    catch (Exception exception)
    {
      response.AddError("Get Explain ShapPlots route : " + exception.Message);
      return response;
    }
  }

  public async Task<ResponseService<GetModelFeaturesInDTO>> GetExplainFeatures(string explainProcessRemoteId)
  {
    var response = new ResponseService<GetModelFeaturesInDTO>();
    try
    {
      using (var http = new HttpClient())
      {
        var httpResponse = await http.GetAsync($"{_ia_url}/explain/{explainProcessRemoteId}/features");
        httpResponse.EnsureSuccessStatusCode();
        string httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
        response.SetSuccessful(JsonSerializer.Deserialize<GetModelFeaturesInDTO>(httpResponseBody, _serializeOptions)!);
      }
      return response;
    }
    catch (Exception exception)
    {
      response.AddError("Get Explain Features route : " + exception.Message);
      return response;
    }
  }

  public async Task<ResponseService<ProjectFileFormated>> PredictValueUsingAiModel(FileStorageResponseGet modelFile, ProjectFileFormated dataTable, string columnToPredict)
  {
    var serviceResponse = new ResponseService<ProjectFileFormated>();
    var jsonRowToPredict = new List<Dictionary<string, object>>();

    var columnsWithoutTheOneToPredict = dataTable.Columns.Where(col => col.Label != columnToPredict).ToList(); // Remove the column to predict

    foreach (var row in dataTable.Rows)
    {
      var serializedKvp = new Dictionary<string, object>();

      foreach (var keyValuePair in row)
      {
        var key = keyValuePair.Key;
        var value = keyValuePair.Value;
        var initialColLabel = columnsWithoutTheOneToPredict.FirstOrDefault(col => col.Name == key)?.Label;

        if (initialColLabel != null)
        {
          serializedKvp[initialColLabel] = value;
        }
      }

      jsonRowToPredict.Add(serializedKvp);
    }

    using (var http = new HttpClient())
    {
      var requestContent = new MultipartFormDataContent
        {
          { new ByteArrayContent(modelFile.FileBytes!), "pipeline", "model" },
          { new StringContent(JsonSerializer.Serialize(jsonRowToPredict), Encoding.UTF8, "application/json"), "json_row" }
        };
      var httpResponse = await http.PostAsync($"{_ia_url}/predict", requestContent);
      httpResponse.EnsureSuccessStatusCode();
      string httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
      var predictResponse = JsonSerializer.Deserialize<PredictWithModelAiResponseDTO>(httpResponseBody);

      if (predictResponse == null)
      {
        serviceResponse.AddError("Prediction response is null.");
        return serviceResponse;
      }

      ProjectFileFormated dataTableWithPrediction;

      int index = 0;
      foreach (var predictionRowValue in predictResponse.prediction)
      {
        dataTable.Rows[index][columnToPredict.Replace(" ", "")] = predictionRowValue;
        index++;
      }

      serviceResponse.SetSuccessful(dataTable);
    }

    // Return the service response
    return serviceResponse;
  }


  private static string SerializeToSnakeLowerCase(string pipeline)
  {
    var deserializeOptions = new JsonSerializerOptions
    {
      PropertyNameCaseInsensitive = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) }
    };
    var deserializedPipelineSnakeLowerCase = JsonSerializer.Deserialize<ProcessPipeline>(pipeline, deserializeOptions);

    var serializeOptions = new JsonSerializerOptions
    {
      PropertyNameCaseInsensitive = true,
      PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
      Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) }
    };

    return JsonSerializer.Serialize(deserializedPipelineSnakeLowerCase, serializeOptions);
  }

  private async static Task<byte[]> FormatFileForPipeline(IFormFile file, Project project)
  {
    var config = new CsvConfiguration(CultureInfo.InvariantCulture)
    {
      HasHeaderRecord = true,
      DetectDelimiter = true
    };

    using var reader = new StreamReader(file.OpenReadStream());
    using var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);
    var records = csvReader.GetRecords<dynamic>().ToList();

    using var memoryStream = new MemoryStream();
    using var writer = new StreamWriter(memoryStream);
    using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);

    var headerRow = ((IDictionary<string, object>)records.First()).Keys;
    var filteredHeaders = headerRow
        .Where(header => project.RowDepedenciesLinked.Contains(header) && !project.DatasetHasIdColumnList.Contains(header))
        .ToList();
    csvWriter.WriteField(filteredHeaders);
    csvWriter.NextRecord();

    foreach (var record in records)
    {
      var recordAsDict = (IDictionary<string, object>)record;
      var filteredRecord = recordAsDict
          .Where(pair => project.RowDepedenciesLinked.Contains(pair.Key) && !project.DatasetHasIdColumnList.Contains(pair.Key))
          .ToDictionary(pair => pair.Key, pair => pair.Value);
      csvWriter.WriteRecord(filteredRecord);
      csvWriter.NextRecord();
    }
    await writer.FlushAsync();


    return memoryStream.ToArray();
  }

  public async static Task<byte[]> IFormFileToByteArray(IFormFile file)
  {
    await using var memoryStream = new MemoryStream();
    await file.CopyToAsync(memoryStream);
    return memoryStream.ToArray();
  }

}
