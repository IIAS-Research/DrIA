using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CsvHelper;
using CsvHelper.Configuration;
using Server.DTOs;
using Server.Models;

namespace Server.Services;

public class IaWebService : ApplicationService
{
  private readonly string _ia_url = "http://rss00992:32845";
  private readonly JsonSerializerOptions _serializeOptions = new JsonSerializerOptions
  {
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) }
  };

  async public Task<ResponseService<RunPipelineOutDTO>> RunPipeline(IFormFile trainFile, IFormFile testFile, Project project, string pipeline)
  {
    var response = new ResponseService<RunPipelineOutDTO>();
    try
    {
      using (var http = new HttpClient())
      {
        var requestContent = new MultipartFormDataContent
        {
            { new ByteArrayContent(await IFormFileToByteArray(trainFile)), "train_dataset", trainFile.FileName },
            { new ByteArrayContent(await IFormFileToByteArray(testFile)), "test_dataset", testFile.FileName },
            { new StringContent(project.CsvColumnToPredict), "dataset_target" },
            { new StringContent(pipeline, Encoding.UTF8, "application/json"), "pipeline" }
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
      response.AddError(exception.Message);
      return response;
    }
  }

  async public Task<ResponseService<GeneratePipelineWebServiceJsonDTO>> GeneratePipeline()
  {
    var response = new ResponseService<GeneratePipelineWebServiceJsonDTO>();
    try
    {
      using (var http = new HttpClient())
      {
        var httpResponse = await http.GetAsync($"{_ia_url}/pipeline/generate");

        httpResponse.EnsureSuccessStatusCode();
        var responseBody = await httpResponse.Content.ReadFromJsonAsync<object>();
        response.SetSuccessful(new GeneratePipelineWebServiceJsonDTO { Pipeline = responseBody! });
      }
      return response;
    }
    catch (Exception exception)
    {
      response.AddError(exception.Message);
      return response;
    }
  }

  async public Task<ResponseService<JobStatusOutDTO>> GetJobStatus(Process process)
  {
    var response = new ResponseService<JobStatusOutDTO>();
    try
    {
      using (var http = new HttpClient())
      {
        var httpResponse = await http.GetAsync($"{_ia_url}/job/{process.Id}/status");
        httpResponse.EnsureSuccessStatusCode();
        string httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
        response.SetSuccessful(JsonSerializer.Deserialize<JobStatusOutDTO>(httpResponseBody)!);
      }
      return response;
    }
    catch (Exception exception)
    {
      response.AddError(exception.Message);
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
      response.SetSuccessful(JsonSerializer.Deserialize<CancelJobOutDTO>(httpResponseBody)!);
      return response;
    }
    catch (Exception exception)
    {
      response.AddError(exception.Message);
      return response;
    }
  }

  async public Task<ResponseService<GetJobMetricsOutDTO>> RetrieveJobMetric(Process process)
  {
    var response = new ResponseService<GetJobMetricsOutDTO>();
    try
    {
      using var http = new HttpClient();
      var httpResponse = await http.GetAsync($"{_ia_url}/job/{process.Id}/metrics");
      httpResponse.EnsureSuccessStatusCode();
      string httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
      response.SetSuccessful(JsonSerializer.Deserialize<GetJobMetricsOutDTO>(httpResponseBody)!);
      return response;
    }
    catch (Exception exception)
    {
      response.AddError(exception.Message);
      return response;
    }
  }

  async public Task<ResponseService<IFormFile>> DownloadModel(Process process, long jobMetricId = 0)
  {
    var response = new ResponseService<IFormFile>();
    try
    {
      using var http = new HttpClient();
      var httpResponse = await http.GetAsync($"{_ia_url}/job/{process.Id}/model/{jobMetricId}/download");
      httpResponse.EnsureSuccessStatusCode();
      using var stream = await httpResponse.Content.ReadAsStreamAsync();
      var formFile = new FormFile(stream, 0, stream.Length, "file", $"Job{process.Id}/Model{jobMetricId}.pickle")
      {
        Headers = new HeaderDictionary(),
        ContentType = httpResponse.Content.Headers.ContentType?.ToString() ?? "application/octet-stream"
      };
      response.SetSuccessful(formFile);
      return response;
    }
    catch (Exception exception)
    {
      response.AddError(exception.Message);
      return response;
    }
  }

  async public Task<ResponseService<ExplainModelOutDTO>> Explain(IFormFile file)
  {
    var response = new ResponseService<ExplainModelOutDTO>();
    try
    {
      using var http = new HttpClient();
      using (var formData = new MultipartFormDataContent())
      {
        using var fileStream = file.OpenReadStream();
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        formData.Add(fileContent, "dataset", file.FileName);
        var httpResponse = await http.PostAsync($"{_ia_url}/explain", formData);
        httpResponse.EnsureSuccessStatusCode();
        string httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
        response.SetSuccessful(JsonSerializer.Deserialize<ExplainModelOutDTO>(httpResponseBody)!);
      }
      return response;
    }
    catch (Exception exception)
    {
      response.AddError(exception.Message);
      return response;
    }
  }

  async public Task<ResponseService<GetModelTextExplanationOutDTO>> GetTextExplanation(Process process, long jobMetricId = 0)
  {
    var response = new ResponseService<GetModelTextExplanationOutDTO>();
    try
    {
      using (var http = new HttpClient())
      {
        var httpResponse = await http.GetAsync($"{_ia_url}/job/{process.Id}/model/{jobMetricId}/explain");
        httpResponse.EnsureSuccessStatusCode();
        string httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
        response.SetSuccessful(JsonSerializer.Deserialize<GetModelTextExplanationOutDTO>(httpResponseBody)!);
      }
      return response;
    }
    catch (Exception exception)
    {
      response.AddError(exception.Message);
      return response;
    }
  }

  async public Task<ResponseService<GetModelBibliographyOutDTO>> GetBibliography(Process process, long jobMetricId = 0)
  {
    var response = new ResponseService<GetModelBibliographyOutDTO>();
    try
    {
      using (var http = new HttpClient())
      {
        var httpResponse = await http.GetAsync($"{_ia_url}/job/{process.Id}/model/{jobMetricId}/bibliography");
        httpResponse.EnsureSuccessStatusCode();
        string httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
        response.SetSuccessful(JsonSerializer.Deserialize<GetModelBibliographyOutDTO>(httpResponseBody)!);
      }
      return response;
    }
    catch (Exception exception)
    {
      response.AddError(exception.Message);
      return response;
    }
  }

  async public Task<ResponseService<SplitOutDTO>> Split(IFormFile file, Project project)
  {
    var response = new ResponseService<SplitOutDTO>();
    byte[] formatedFile = await FormatFileForPipeline(file, project);
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
      response.AddError(exception.Message);
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
      response.AddError(exception.Message);
      return response;
    }
  }

  async public Task<ResponseService<IFormFile>> GetSplitTrainFile(string splitProcessId)
  {
    var response = new ResponseService<IFormFile>();
    try
    {
      using (var http = new HttpClient())
      {
        var httpResponse = await http.GetAsync($"{_ia_url}/split/{splitProcessId}/train");
        httpResponse.EnsureSuccessStatusCode();
        var content = await httpResponse.Content.ReadAsByteArrayAsync();
        using var stream = new MemoryStream(content);
        var formFile = new FormFile(stream, 0, stream.Length, "", $"Train{splitProcessId}.csv")
        {
          ContentType = httpResponse.Content.Headers.ContentType?.ToString() ?? "text/csv"
        };
        response.SetSuccessful(formFile);
      }
      return response;
    }
    catch (Exception exception)
    {
      response.AddError(exception.Message);
      return response;
    }
  }

  async public Task<ResponseService<IFormFile>> GetSplitTestFile(string splitProcessId)
  {
    var response = new ResponseService<IFormFile>();
    try
    {
      using (var http = new HttpClient())
      {
        var httpResponse = await http.GetAsync($"{_ia_url}/split/{splitProcessId}/test");
        httpResponse.EnsureSuccessStatusCode();
        var content = await httpResponse.Content.ReadAsByteArrayAsync();
        using var stream = new MemoryStream(content);
        var formFile = new FormFile(stream, 0, stream.Length, "", $"Test{splitProcessId}.csv")
        {
          ContentType = httpResponse.Content.Headers.ContentType?.ToString() ?? "text/csv"
        };
        response.SetSuccessful(formFile);
      }
      return response;
    }
    catch (Exception exception)
    {
      response.AddError(exception.Message);
      return response;
    }
  }

  async private static Task<byte[]> FormatFileForPipeline(IFormFile file, Project project)
  {
    var config = new CsvConfiguration(CultureInfo.InvariantCulture)
    {
      HasHeaderRecord = true,
      DetectDelimiter = true
    };

    using var reader = new StreamReader(file.OpenReadStream());
    using var csvReader = new CsvReader(reader, config);
    var records = csvReader.GetRecords<dynamic>().ToList();

    using var memoryStream = new MemoryStream();
    using var writer = new StreamWriter(memoryStream);
    using var csvWriter = new CsvWriter(writer, config);

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
          .Where(pair => !project.RowDepedenciesLinked.Contains(pair.Key))
          .ToDictionary(pair => pair.Key, pair => pair.Value);
      csvWriter.WriteRecord(filteredRecord);
      csvWriter.NextRecord();
    }
    await writer.FlushAsync();
    return memoryStream.ToArray();
  }

  private async static Task<byte[]> IFormFileToByteArray(IFormFile file)
  {
    await using var memoryStream = new MemoryStream();
    await file.CopyToAsync(memoryStream);
    return memoryStream.ToArray();
  }
}
