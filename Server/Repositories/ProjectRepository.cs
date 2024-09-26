using Microsoft.EntityFrameworkCore;
using Server.DAL.Db;
using Server.Models;

namespace Server.Repositories;
public class ProjectRepository(MainDbContext dbContext)
{
    private readonly MainDbContext _dbContext = dbContext;

    public async Task<List<Project>> GetProjectByUser(User user)
    {
        var projects = await _dbContext.Projects
            .Where(p => p.UserId == user.Id)
            .ToListAsync();
        return projects;
    }

    public async Task<List<Project>> GetProjects()
    {
        return await _dbContext.Projects.ToListAsync();
    }

    public async Task<Project?> GetProjectById(long id)
    {
        return await _dbContext.Projects.FindAsync(id);
    }

}