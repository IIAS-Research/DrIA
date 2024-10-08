using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Server.DAL.Db;
using Server.DTOs;
using Server.Models;
using static Server.Models.Model;

namespace Server.Repositories;
public class ModelRepository(MainDbContext dbContext, IServiceScopeFactory serviceScopeFactory)
{
    private readonly MainDbContext _dbContext = dbContext;
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;


    public async Task<List<Model>> GetModels()
    {
        return await _dbContext.Models.ToListAsync();
    }

    public async Task<Model> CreateModel(long processId, Metrics metrics, ModelBibliography bibliography)
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

    public async Task<Model> CreateModelOutsideOfRequestContext(long processId, Metrics metrics, ModelBibliography bibliography)
    {
        using (var scope = _serviceScopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MainDbContext>();

            Model model = new Model
            {
                MetricsJson = JsonSerializer.Serialize(metrics),
                ProcessId = processId,
                Date = DateTime.Now,
                BibliographyJson = JsonSerializer.Serialize(bibliography),
            };
            await dbContext.Models.AddRangeAsync(model);
            await dbContext.SaveChangesAsync();
            return model;
        }
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

    public async Task<ModelFile> CreateModelFileOutsideOfRequestContext(string filename, string filePath, string mimeType, long localDbModelId)
    {
        using (var scope = _serviceScopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MainDbContext>();

            ModelFile ModelFile = new ModelFile
            {
                Filename = filename,
                StorageFilename = filePath,
                ModelId = localDbModelId,
                MimeType = mimeType
            };
            await dbContext.ModelFiles.AddRangeAsync(ModelFile);
            await dbContext.SaveChangesAsync();
            return ModelFile;
        }
    }

}