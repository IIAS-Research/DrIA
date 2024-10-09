using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Server.DAL.Db;
using Server.DTOs;
using Server.Models;
using static Server.Models.Process;

namespace Server.Repositories;
public class ProcessRepository(MainDbContext dbContext, IServiceScopeFactory serviceScopeFactory)
{
  private readonly MainDbContext _dbContext = dbContext;
  private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;

  public async Task<Process> CreateProcess(RunPipelineOutDTO pipelineResponse, string pipelineData, Project project)
  {
    Process process = new Process
    {
      Date = DateTime.Now,
      Duration = 0L,
      Status = pipelineResponse.Status,
      PipelineJson = pipelineData,
      ProjectId = project.Id,
      IaRemoteId = pipelineResponse.Id,
      MaxDuration = pipelineResponse.MaxDuration
    };
    await _dbContext.Processes.AddRangeAsync(process);
    await _dbContext.SaveChangesAsync();
    return process;
  }

  public async Task<Process.EProcessStatut> GetProcessStatusById(string processRemoteId)
  {
    return await _dbContext.Processes
    .Where(p => p.IaRemoteId == processRemoteId)
    .Select(p => p.Status)
    .FirstOrDefaultAsync();
  }

  public async Task UpdateProcessStatus(string processRemoteId, Process.EProcessStatut statut)
  {
    var process = await _dbContext.Processes.FirstOrDefaultAsync(p => p.IaRemoteId == processRemoteId);
    if (process != null)
    {
      process.Status = statut;
      await _dbContext.SaveChangesAsync();
    }
  }

  public async Task<Process?> GetProcessByIaRemoteId(string processRemoteId)
  {
    return await _dbContext.Processes.FirstOrDefaultAsync(p => p.IaRemoteId == processRemoteId);
  }

  public async Task<Process?> GetProcessByIaRemoteIdOutsideOfRequestContext(string processRemoteId)
  {
    using (var scope = _serviceScopeFactory.CreateScope())
    {
      var dbContext = scope.ServiceProvider.GetRequiredService<MainDbContext>();
      return await dbContext.Processes.FirstOrDefaultAsync(p => p.IaRemoteId == processRemoteId);
    }
  }

  public async Task<ProcessPipeline?> GetProcessPipeline(long processId, long projectId)
  {
    var process = await _dbContext.Processes
        .Where(p => p.ProjectId == projectId && p.Id == processId)
        .FirstOrDefaultAsync();

    return process?.Pipeline;
  }



}