using Microsoft.EntityFrameworkCore;
using Server.DAL.Db;
using Server.DTOs;
using Server.Models;

namespace Server.Repositories;
public class ModelRepository(MainDbContext dbContext)
{
    private readonly MainDbContext _dbContext = dbContext;

    public async Task<List<Model>> GetModels()
    {
        return await _dbContext.Models.ToListAsync();
    }

    public async Task<Model> CreateModel(long processId, Metrics metrics, GetJobBibliographyOutDTO bibliography)
    {
        Model model = new Model
        {
            Metrics = metrics,
            ProcessId = processId,
            Date = DateTime.Now,
            Bibliography = bibliography,
        };
        await _dbContext.Models.AddRangeAsync(model);
        await _dbContext.SaveChangesAsync();
        return model;
    }

    public async Task<ModelFile> CreateModelFile(Metrics model, string filename, string filePath, string mimeType)
    {
        ModelFile ModelFile = new ModelFile
        {
            Filename = filename,
            StorageFilename = filePath,
            ModelId = model.id,
            MimeType = mimeType
        };
        await _dbContext.ModelFiles.AddRangeAsync(ModelFile);
        await _dbContext.SaveChangesAsync();
        return ModelFile;
    }
}