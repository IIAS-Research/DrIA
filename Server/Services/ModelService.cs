
using Server.DTOs;
using Server.Models;
using Server.Repositories;
using static Server.Models.Model;

namespace Server.Services;

public class ModelService : ApplicationService
{
    private readonly ModelRepository _modelRepository;
    private readonly FileStorageService _fileStorageService;

    public ModelService(ModelRepository modelRepository, FileStorageService fileStorageService)
    {
        _modelRepository = modelRepository;
        _fileStorageService = fileStorageService;
    }

    // public async Task SaveModelAndItsFile(CreateModelDTO modelId, ResponseService<IFormFile> modelFile)
    // {
    //     var saveModelResponse = await _modelRepository.CreateModel()
    //     var writeModelFileResponse = await _fileStorageService.WriteModelImageToStorageAsync(modelId, modelFile);
    // }


    async public Task<ResponseService<Model>> CreateModel(long processId, Metrics metrics, ModelBibliography bibliography)
    {
        var response = new ResponseService<Model>();
        var model = await _modelRepository.CreateModel(processId, metrics, bibliography);
        if (model != null) response.SetSuccessful(model);
        return response;
    }


    async public Task<ResponseService<Model>> CreateModelOutsideOfRequestContext(long processId, Metrics metrics, ModelBibliography bibliography)
    {
        var response = new ResponseService<Model>();
        var model = await _modelRepository.CreateModelOutsideOfRequestContext(processId, metrics, bibliography);
        if (model != null) response.SetSuccessful(model);
        return response;
    }

    async public Task<ResponseService<ModelFile>> CreateModelFile(IFormFile file, Metrics model)
    {
        var response = new ResponseService<ModelFile>();

        var responseWriteFile = await WriteModelFile(file);
        if (!response.EnsureOtherServiceIsSuccess(responseWriteFile)) return response;

        var ModelFile = await _modelRepository.CreateModelFile(model, responseWriteFile.Data.OriginalFilename, responseWriteFile.Data.FilePath, responseWriteFile.Data.MimeType);
        response.SetSuccessful(ModelFile);
        return response;
    }

    async public Task<ResponseService<ModelFile>> CreateModelFileOutsideOfRequestContext(IFormFile file, long localDbModelId)
    {
        var response = new ResponseService<ModelFile>();

        var responseWriteFile = await WriteModelFile(file);
        if (!response.EnsureOtherServiceIsSuccess(responseWriteFile)) return response;

        var ModelFile = await _modelRepository.CreateModelFileOutsideOfRequestContext(responseWriteFile.Data.OriginalFilename, responseWriteFile.Data.FilePath, responseWriteFile.Data.MimeType, localDbModelId);
        response.SetSuccessful(ModelFile);
        return response;
    }

    async public Task<ResponseService<FileStorageService.FileStorageResponseWrite>> WriteModelFile(IFormFile file)
    {
        return await _fileStorageService.WriteModelBinaryToStorageAsync(file);
    }
}

// Service responses

