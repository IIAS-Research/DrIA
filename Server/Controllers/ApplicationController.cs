using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.Models;
using Server.Services;

namespace Server.Controllers;

[Controller]
public abstract class ApplicationController : ControllerBase
{
    protected UserManager<User> _userManager;
    protected IAuthorizationService _authorizationService;
    public ApplicationController(UserManager<User> userManager, IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
        _userManager = userManager;
    }

    protected bool IsCurrentUserAuth()
    {
        return User.Identity?.IsAuthenticated ?? false;
    }

    protected long? GetCurrentUserId()
    {
        bool tryParse = long.TryParse(User.Identity?.Name, out long currentUserId);
        if (tryParse == false)
        {
            return null;
        }
        return currentUserId;
    }

    async protected Task<User?> GetCurrentUser()
    {
        long? userId = GetCurrentUserId();
        if (userId is not long userIdLong)
        {
            return null;
        }
        return await _userManager.FindByIdAsync(userIdLong.ToString());
    }

}
