using System.Text.Json;
using Server.DAL.Db;
using Server.DTOs;
using Server.Models;

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
      Status = pipelineResponse.status,
      PipelineJson = pipelineData,
      ProjectId = project.Id,
      IaRemoteId = pipelineResponse.id
    };
    await _dbContext.Processes.AddRangeAsync(process);
    await _dbContext.SaveChangesAsync();
    return process;
  }

  public async Task UpdateProcessStatus(string processRemoteId, Process.EProcessStatut statut)
  {
    
  }
}