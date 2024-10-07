
using Server.Repositories;

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

    // public async Task SaveModelAndItsFile(CreateModelDTO modelId,  ResponseService<IFormFile> modelFile)
    // {
    //     var saveModelResponse = await _modelRepository.CreateModel()
    //     var writeModelFileResponse = await _fileStorageService.WriteModelImageToStorageAsync(modelId, modelFile);
    // }

    // async public Task<ResponseService<FileStorageService.FileStorageResponseWrite>> WriteModelFile(string modelId, IFormFile file)
    // {
    //     return await _fileStorageService.WriteModelImageToStorageAsync(modelId, file);
    // }
}

// Service responses

