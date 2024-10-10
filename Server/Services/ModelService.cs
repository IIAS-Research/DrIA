
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

  public ModelService(ModelRepository modelRepository, FileStorageService fileStorageService, IaWebService iaWebService)
  {
    _modelRepository = modelRepository;
    _fileStorageService = fileStorageService;
    _iaWebService = iaWebService;
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

  async public Task<ResponseService<FileStorageService.FileStorageResponseWrite>> WriteModelFile(IFormFile file)
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
      response.EnsureOtherServiceIsSuccess(modelFile);
      response.SetSuccessful(modelFile.Data);
    }
    return response;
  }

}