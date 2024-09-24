using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Server.Models;

namespace Server.Controllers;

[Controller]
public abstract class ApplicationController : ControllerBase
{
    protected long? GetCurrentUserId()
    {
        return User.Identity;
    }
}
