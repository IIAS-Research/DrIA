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
}