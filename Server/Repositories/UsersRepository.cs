using Microsoft.EntityFrameworkCore;
using Server.DAL.Db;
using Server.Models;

namespace Server.Repositories;
public class UsersRepository(MainDbContext dbContext)
{
    private readonly MainDbContext _dbContext = dbContext;

    public async Task<User> AddUserAsync(User user)
    {
        var entry = await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();
        return entry.Entity;
    }

    public async Task<User?> FindUserByIdAsync(long id)
    {
        return await _dbContext.Users.FindAsync(id);
    }

    public async Task<User?> FindUserByEmailAsync(string email)
    {
        return await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

}