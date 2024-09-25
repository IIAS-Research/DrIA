using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.Models;

namespace Server.Controllers;

[Controller]
public abstract class ApplicationController : ControllerBase
{
    protected UserManager<User> _userManager;
    public ApplicationController(UserManager<User> userManager)
    {
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

    protected Task<User?>? GetCurrentUser()
    {
        long? userId = GetCurrentUserId();
        if (userId is not long userIdLong)
        {
            return null;
        }
        return _userManager.FindByIdAsync(userIdLong.ToString());
    }
}
