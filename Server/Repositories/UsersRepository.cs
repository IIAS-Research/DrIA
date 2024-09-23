using Microsoft.EntityFrameworkCore;
using Server.DAL.Db;
using Server.Models;

namespace Server.Repositories;
public class UsersRepository(MainDbContext dbContext)
{
    private readonly MainDbContext _dbContext = dbContext;

    public async Task<bool> AddUserAsync(User user)
    {
        await _dbContext.Users.AddAsync(user);
        return await _dbContext.SaveChangesAsync() > 0;
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