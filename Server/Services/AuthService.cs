using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Server2.Models;

namespace Server.Services;

public class AuthService : ApplicationService
{
    public static readonly string ClaimTypeToIdentifyUserOn = ClaimTypes.PrimarySid;

    private readonly UserManager<User> _userManager;
    private readonly IConfiguration _config;

    public AuthService(UserManager<User> userManager, IConfiguration config)
    {
        _userManager = userManager;
        _config = config;
    }

    public async Task<bool> RegisterUser(string username, string password)
    {
        var identityUser = new User
        {
            UserName = username,
            Email = username,
        };
        var registeredUserResult = await _userManager.CreateAsync(identityUser, password);
        return registeredUserResult.Succeeded;
    }

    public async Task<AuthServiceResponse> Login(string username, string password)
    {
        var loginResponse = new AuthServiceResponse();
        var identityUser = await _userManager.FindByNameAsync(username);
        if (identityUser == null)
        {
            return loginResponse;
        }
        var passwordMatch = await _userManager.CheckPasswordAsync(identityUser, password);
        if (passwordMatch == false)
        {
            return loginResponse;
        }
        loginResponse.IsLogedIn = true;
        loginResponse.EncodedJwtToken = GenerateTokenString(identityUser);
        loginResponse.RefreshToken = GenerateRefreshTokenString();

        identityUser.RefreshToken = loginResponse.RefreshToken;
        identityUser.RefreshTokenExpiry = GetJwtExpiration();

        await _userManager.UpdateAsync(identityUser);

        return loginResponse;
    }

    public async Task<AuthServiceResponseJwt> GenerateUserJwt(User user)
    {
        var loginResponse = new AuthServiceResponseJwt
        {
            EncodedJwtToken = GenerateTokenString(user),
            RefreshToken = GenerateRefreshTokenString()
        };
        user.RefreshToken = loginResponse.RefreshToken;
        user.RefreshTokenExpiry = GetJwtExpiration();
        await _userManager.UpdateAsync(user);
        return loginResponse;
    }

    public async Task<AuthServiceResponseUser> CreateOrUpdateUserFromNoyauSih(NoyauSihUser noyauUser)
    {
        var loginResponse = new AuthServiceResponseUser();
        var userProperties = new User()
        {
            IdRes = noyauUser.id_res,
            Email = noyauUser.courriel,
            // Roles = noyauUser.profils
        };
        var user = await _userManager.FindByIdAsync(userProperties.IdRes);
        if (user == null)
        {
            await _userManager.CreateAsync(userProperties);
            loginResponse.User = userProperties;
        }
        else
        {
            userProperties.Id = user.Id;
            await _userManager.UpdateAsync(userProperties);
            loginResponse.User = user;
        }
        loginResponse.IsSuccess = true;
        return loginResponse;
    }

    public async Task<AuthServiceResponse> RefreshToken(string jwt, string refreshToken)
    {
        var response = new AuthServiceResponse();

        var claimsPrincipal = DecodeJwt(jwt);

        if (claimsPrincipal is null)
        {
            return response;
        }
        var userIdFromClaimsPrincipal = claimsPrincipal.Identity?.Name;
        if (userIdFromClaimsPrincipal is null)
        {
            return response;
        }

        var identityUser = await _userManager.FindByIdAsync(userIdFromClaimsPrincipal);

        if (identityUser == null)
        {
            return response;
        }
        if (identityUser.RefreshToken != refreshToken || identityUser.RefreshTokenExpiry < DateTime.Now)
        {
            return response;
        }
        response.IsLogedIn = true;
        response.RefreshToken = GenerateRefreshTokenString();
        response.EncodedJwtToken = GenerateTokenString(identityUser);

        identityUser.RefreshToken = response.RefreshToken;
        identityUser.RefreshTokenExpiry = GetJwtExpiration();

        await _userManager.UpdateAsync(identityUser);

        return response;
    }

    private string GenerateTokenString(User user)
    {
        // Claims for Entity to retrieve when authorizing
        var claims = new List<Claim>
        {
            new (ClaimTypeToIdentifyUserOn, user.Id)
        };

        var securityKey = GetSymmetricJwtSecurityKey(_config);
        var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature);

        var securityToken = new JwtSecurityToken(
            expires: GetJwtExpiration(),
            signingCredentials: signingCredentials,
            claims: claims
        );

        // Custom data
        securityToken.Payload["UserId"] = user.Id;
        securityToken.Payload["UserFullname"] = user.Fullname;
        securityToken.Payload["UserRoles"] = user.Roles;

        string tokenString = new JwtSecurityTokenHandler().WriteToken(securityToken);
        return tokenString;
    }

    private ClaimsPrincipal DecodeJwt(string jwt)
    {
        try
        {
            return new JwtSecurityTokenHandler().ValidateToken(jwt, GetAuthServiceTokenValidationParametersForRefreshToken(), out _);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public TokenValidationParameters GetAuthServiceTokenValidationParametersForRefreshToken()
    {
        var securityKey = GetSymmetricJwtSecurityKey(_config);
        return new TokenValidationParameters
        {
            IssuerSigningKey = securityKey,
            ValidateLifetime = false,
            ValidateActor = false,
            ValidateIssuer = false,
            ValidateAudience = false,
            NameClaimType = ClaimTypeToIdentifyUserOn
        };
    }

    public static TokenValidationParameters GetAuthServiceTokenValidationParameters(IConfiguration config)
    {
        var securityKey = GetSymmetricJwtSecurityKey(config);
        return new TokenValidationParameters
        {
            IssuerSigningKey = securityKey,
            ValidateActor = false,
            ValidateIssuer = false,
            ValidateAudience = false,
            RequireExpirationTime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = ClaimTypeToIdentifyUserOn
        };
    }

    private static string GenerateRefreshTokenString()
    {
        var randomNumber = new byte[64];
        using (var numberGenerator = RandomNumberGenerator.Create())
        {
            numberGenerator.GetBytes(randomNumber);
        }
        return Convert.ToBase64String(randomNumber);
    }

    private static DateTime GetJwtExpiration()
    {
        return DateTime.Now.AddHours(8);
    }

    private static SymmetricSecurityKey GetSymmetricJwtSecurityKey(IConfiguration config)
    {
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetJwtPrivateKey(config)));
    }

    private static string GetJwtPrivateKey(IConfiguration config)
    {
        return config["MissionDevJwtPrivateKey"];
    }
}
