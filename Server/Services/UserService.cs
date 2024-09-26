using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Server.Models;

namespace Server.Services;

public class UserService(UserManager<User> userManager) : ApplicationService
{
    private readonly UserManager<User> _userManager = userManager;

    public async Task<UserResponseGetAll> GetUsers()
    {
        UserResponseGetAll userResponseGetAll = new();

        var users = await _userManager.Users.ToListAsync();
        if (users.Count == 0)
        {
            userResponseGetAll.AddError("No users found");
            return userResponseGetAll;
        }
        userResponseGetAll.IsSuccess = true;
        userResponseGetAll.Users = users;
        return userResponseGetAll;
    }

    public class UserResponseGetAll : ResponseService
    {
        public List<User> Users { get; set; } = [];
    }
}