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


  public async Task<Model?> GetModelById(long id)
  {
    return await _dbContext.Models.FindAsync(id);
  }

  public async Task<Model?> GetModeleWithProcessById(long id)
  {
    return await _dbContext.Models
      .Include(m => m.Process)
      .ThenInclude(p => p!.Project)
      .FirstOrDefaultAsync(m => m.Id == id);
  }

  public async Task<List<Model>> GetModels()
  {
    return await _dbContext.Models.ToListAsync();
  }

  public async Task<Model> CreateModel(long processId, Metrics metrics, ModelBibliography bibliography, int iaRemoteId)
  {
    var existing = await _dbContext.Models.FirstOrDefaultAsync(m => m.ProcessId == processId && m.IaRemoteId == iaRemoteId);
    if (existing != null) return existing;
    Model model = new Model
    {
      MetricsJson = JsonSerializer.Serialize(metrics),
      ProcessId = processId,
      Date = DateTime.Now,
      BibliographyJson = JsonSerializer.Serialize(bibliography),
      IaRemoteId = iaRemoteId
    };
    await _dbContext.Models.AddRangeAsync(model);
    await _dbContext.SaveChangesAsync();
    return model;
  }

  public async Task<ModelFile> CreateModelFile(string filename, string filePath, string mimeType, long modelId)
  {
    ModelFile ModelFile = new ModelFile
    {
      Filename = filename,
      StorageFilename = filePath,
      ModelId = modelId,
      MimeType = mimeType
    };
    await _dbContext.ModelFiles.AddRangeAsync(ModelFile);
    await _dbContext.SaveChangesAsync();
    return ModelFile;
  }

  public async Task<Model> SaveTextExplanation(Model model, string[] explanation)
  {
    model.TextExplanation = explanation; 
    await _dbContext.SaveChangesAsync();
    return model;
  }

  public async Task<Model> SaveExplanation(Model model, ModelExplanation explanation)
  {
    model.Explanation = JsonSerializer.Serialize(explanation); 
    await _dbContext.SaveChangesAsync();
    return model;
  }

  public async Task<bool> SaveExplanationIfUnchanged(long modelId, string expectedExplanation, ModelExplanation explanation)
  {
    var json = JsonSerializer.Serialize(explanation);
    return await _dbContext.Models.Where(m => m.Id == modelId && m.Explanation == expectedExplanation)
      .ExecuteUpdateAsync(update => update.SetProperty(m => m.Explanation, json)) == 1;
  }

  public async Task SaveReports(Model model, JsonElement? report, JsonElement? audit, string? markdown)
  {
    if (report.HasValue) model.ReportJson = report.Value.GetRawText();
    if (audit.HasValue) model.AuditJson = audit.Value.GetRawText();
    if (markdown != null) model.ReportMarkdown = markdown;
    await _dbContext.SaveChangesAsync();
  }

  public async Task<ModelFile?> GetModelFileByModelId(long modelId)
  {
    var modelFile = await _dbContext.ModelFiles.FirstOrDefaultAsync(mf => mf.ModelId == modelId);
    return modelFile;
  }

  public async Task<ModelFile?> GetModelFileById(long id)
  {
    return await _dbContext.ModelFiles.FirstOrDefaultAsync(mf => mf.ModelId == id); 
  }
  // Policies
  async public Task<bool> UserCanAccessModel(Model model, long userId) // TODO put the > 0 check in service
  {
    var modelProcess = await _dbContext.Projects
      .Include(proj => proj.Processes)
      .ThenInclude(proc => proc.Models)
      .Where(proj => proj.UserId == userId)
      .Where(proj => proj.Processes.Any(proc => proc.Models.Any(mod => mod.Id == model.Id)))
      .Select(p => p.Id)
      .ToListAsync();
    return modelProcess.Count > 0;
  }

}
