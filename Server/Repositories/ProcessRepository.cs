using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;
using Server.DAL.Db;
using Server.DTOs;
using Server.Models;
using static Server.Models.Process;

namespace Server.Repositories;
public class ProcessRepository(MainDbContext dbContext)
{
  private readonly MainDbContext _dbContext = dbContext;

  public async Task<Process> CreateProcess(RunPipelineOutDTO pipelineResponse, string pipelineData, Project project)
  {
    Process process = new Process
    {
      Date = DateTime.Now,
      Duration = 0L,
      Status = pipelineResponse.Status,
      PipelineJson = pipelineResponse.ConfigurationJson ?? pipelineData,
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
      var snapshot = JsonNode.Parse(process.StatusSnapshotJson ?? "{}")!.AsObject();
      snapshot["id"] = processRemoteId;
      snapshot["status"] = new JsonObject { ["current"] = statut.ToString() };
      process.StatusSnapshotJson = snapshot.ToJsonString();
      await _dbContext.SaveChangesAsync();
    }
  }

  public async Task<Process?> GetProcessByIaRemoteId(string processRemoteId)
  {
    return await _dbContext.Processes.FirstOrDefaultAsync(p => p.IaRemoteId == processRemoteId);
  }

  public Task<Process?> GetProcessById(long id) => _dbContext.Processes.Include(p => p.Project).FirstOrDefaultAsync(p => p.Id == id);

  public async Task<ProcessPipeline?> GetProcessPipeline(long processId, long projectId)
  {
    var process = await _dbContext.Processes
        .Where(p => p.ProjectId == projectId && p.Id == processId)
        .FirstOrDefaultAsync();

    return process?.Pipeline;
  }



}
