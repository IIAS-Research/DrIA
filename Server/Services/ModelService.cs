
using System.Text.Json;
using Server.DTOs;
using Server.Models;
using Server.Repositories;
using static Server.Models.Model;
using static Server.Services.FileStorageService;

namespace Server.Services;

public class ModelService : ApplicationService
{
  private readonly ModelRepository _modelRepository;
  private readonly FileStorageService _fileStorageService;
  private readonly IaWebService _iaWebService;
  private readonly IaPullingExplainService _pullingService;

  public ModelService(ModelRepository modelRepository, FileStorageService fileStorageService, IaWebService iaWebService, IaPullingExplainService pullingService)
  {
    _modelRepository = modelRepository;
    _fileStorageService = fileStorageService;
    _iaWebService = iaWebService;
    _pullingService = pullingService;
  }

  async public Task<ResponseService<Model>> CreateModel(long processId, Metrics metrics, ModelBibliography bibliography, int iaRemoteId)
  {
    var response = new ResponseService<Model>();
    var model = await _modelRepository.CreateModel(processId, metrics, bibliography, iaRemoteId);
    if (model != null) response.SetSuccessful(model);
    return response;
  }

  async public Task<ResponseService<ModelFile>> CreateModelFile(IFormFile file, long modelId)
  {
    var response = new ResponseService<ModelFile>();

    var responseWriteFile = await WriteModelFile(file);
    if (!response.EnsureOtherServiceIsSuccess(responseWriteFile)) return response;

    var ModelFile = await _modelRepository.CreateModelFile(responseWriteFile.Data.OriginalFilename, responseWriteFile.Data.FilePath, responseWriteFile.Data.MimeType, modelId);
    response.SetSuccessful(ModelFile);
    return response;
  }

  async public Task<ResponseService<FileStorageResponseWrite>> WriteModelFile(IFormFile file)
  {
    return await _fileStorageService.WriteModelBinaryToStorageAsync(file);
  }

  async public Task<ResponseService<Model>> GetModelById(long id)
  {
    var response = new ResponseService<Model>();
    var model = await _modelRepository.GetModelById(id);
    if (model == null)
    {
      response.AddError("Model not found");
      return response;
    }
    response.SetSuccessful(model);
    return response;
  }

  async public Task<ResponseService<Model>> GetModeleWithProcessById(long id)
  {
    var response = new ResponseService<Model>();
    var model = await _modelRepository.GetModeleWithProcessById(id);
    if (model == null)
    {
      response.AddError("Model not found");
      return response;
    }
    response.SetSuccessful(model);
    return response;
  }

  async public Task<ResponseService<ExplainModelOutDTO>> StartModelExplain(Model modelWithProcess, byte[] projectTestFile, JsonElement? options = null)
  {
    var response = new ResponseService<ExplainModelOutDTO>();

    var process = modelWithProcess.Process!;
    var responseExplanation = await _iaWebService.ExplainCandidate(process.Project!, projectTestFile, process.IaRemoteId, modelWithProcess.IaRemoteId, options);
    if (!responseExplanation.IsSuccess && responseExplanation.HttpStatusCode == 404)
    {
      var file = await GetModelFileById(modelWithProcess.Id);
      if (!response.EnsureOtherServiceIsSuccess(file)) return response;
      responseExplanation = await _iaWebService.ExplainModel(projectTestFile, file.Data!.FileBytes!, process.Project!, options);
    }
    if (!response.EnsureOtherServiceIsSuccess(responseExplanation)) return response;
    await _modelRepository.SaveExplanation(modelWithProcess, new ModelExplanation { id = responseExplanation.Data.id, status = responseExplanation.Data.status });
    await _pullingService.CreateIaConnection(responseExplanation.Data.id, modelWithProcess.Id).StartPulling();
    response.SetSuccessful(responseExplanation.Data);
    return response;
  }

  async public Task<ResponseService> SaveModelTextExplanation(Model model, string[] explanations)
  {
    var response = new ResponseService();
    await _modelRepository.SaveTextExplanation(model, explanations);
    response.SetSuccessful();
    return response;
  }

  async public Task<ResponseService> SaveModelExplanation(Model model, ModelExplanation explanation)
  {
    var response = new ResponseService();
    await _modelRepository.SaveExplanation(model, explanation);
    response.SetSuccessful();
    return response;
  }

  public Task<bool> SaveModelExplanationIfCurrent(Model model, ModelExplanation explanation, string explainerId)
  {
    if (string.IsNullOrWhiteSpace(model.Explanation) || JsonSerializer.Deserialize<ModelExplanation>(model.Explanation)?.id != explainerId)
      return Task.FromResult(false);
    return _modelRepository.SaveExplanationIfUnchanged(model.Id, model.Explanation, explanation);
  }

  async public Task<ResponseService<ModelFile>> GetModelFileByModelId(long modelId)
  {
    var response = new ResponseService<ModelFile>();
    var modelFile = await _modelRepository.GetModelFileByModelId(modelId);
    if (modelFile == null)
    {
      response.AddError("ModelFile not found.");
      return response;
    }
    response.SetSuccessful(modelFile);
    return response;
  }

  async public Task<ResponseService<Model>> GetModeleExplanationById(long id)
  {
    var response = new ResponseService<Model>();

    var responseModele = await GetModeleWithProcessById(id);
    if (!response.EnsureOtherServiceIsSuccess(responseModele)) return response;

    var responseExplanation = await _iaWebService.GetTextExplanation(responseModele.Data.Process!.IaRemoteId, responseModele.Data.IaRemoteId);
    if (!response.EnsureOtherServiceIsSuccess(responseExplanation)) return response;

    var model = await _modelRepository.SaveTextExplanation(responseModele.Data, responseExplanation.Data.explanations);

    response.SetSuccessful(model);
    return response;
  }

  async public Task<bool> UserCanAccessModel(Model model, long userId)
  {
    return await _modelRepository.UserCanAccessModel(model, userId);
  }

  async public Task<ResponseService<FileStorageResponseGet?>> GetModelFileById(long id)
  {
    var response = new ResponseService<FileStorageResponseGet?>();
    var modelFileEntity = await _modelRepository.GetModelFileById(id);
    if (modelFileEntity == null)
    {
      response.AddError("Model file not found.");
    }
    else
    {
      var modelFile = await _fileStorageService.GetFileFromStorageAsync(modelFileEntity.StorageFilename);
      if (!response.EnsureOtherServiceIsSuccess(modelFile)) return response;
      response.SetSuccessful(modelFile.Data);
    }
    return response;
  }

  public async Task SaveReports(Model model, JsonElement? report, JsonElement? audit, string? markdown)
  {
    await _modelRepository.SaveReports(model, report, audit, markdown);
  }

}
