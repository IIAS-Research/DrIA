using Microsoft.EntityFrameworkCore;
using Server.DAL.Db;
using Server.DTOs;
using Server.Models;

namespace Server.Repositories;
public class ProcessRepository(MainDbContext dbContext)
{
    private readonly MainDbContext _dbContext = dbContext;

    // public async Task<Process> CreateProcess(StartProjectInDTO projectData, Project project)
    // {
    //   Process process = new Process{
    //       Date = DateTime.Now,
    //       Duration: projectData.
    //       ProjectId = project.Id
    //     };
    //     await _dbContext.Processes.AddRangeAsync(process);
    //     await _dbContext.SaveChangesAsync();
    //     return process;
    // }
}