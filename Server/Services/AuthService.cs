using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Server.Models;
using Server.DTOs;
using JwtPayload = Server.DTOs.JwtPayload;
using System.Text;

namespace Server.Services;

public class AuthService(IConfiguration config, UserManager<User> userManager) : ApplicationService
{
    public static readonly string ClaimTypeToIdentifyUserOn = ClaimTypes.NameIdentifier;
    private readonly IConfiguration _config = config;
    private readonly UserManager<User> _userManager = userManager;

    public async Task<User?> RegisterUser(RegisterUserDTO registerUserDTO)
    {
        var userToRegister = new User
        {
            Email = registerUserDTO.Email,
            FirstName = registerUserDTO.FirstName,
            LastName = registerUserDTO.LastName,
            IsAdmin = registerUserDTO.IsAdmin,
            UserName = registerUserDTO.Email
        };
        var registeredUserResult = await _userManager.CreateAsync(userToRegister, registerUserDTO.Password);
        if (registeredUserResult.Succeeded) return await _userManager.FindByEmailAsync(registerUserDTO.Email);
        return null;
    }


    public async Task<LoginResponse> Login(string email, string password)
    {
        var identityUser = await _userManager.FindByEmailAsync(email);
        if (identityUser == null)
        {
            return new LoginResponse { IsLoggedIn = false };
        }
        var passwordMatch = await _userManager.CheckPasswordAsync(identityUser, password);
        if (passwordMatch == false)
        {
            return new LoginResponse { IsLoggedIn = false };
        }
        var encodedJwtToken = GenerateTokenString(identityUser);
        await _userManager.UpdateAsync(identityUser);
        return new LoginResponse { IsLoggedIn = true, AccessToken = encodedJwtToken };
    }

    public async Task<LoginResponse> RefreshAccessToken(string accessToken)
    {
        var decodedAccessToken = DecodeJwt(accessToken);
        var userEmailClaim = decodedAccessToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email) ?? throw new InvalidOperationException("User EMAIL claim is missing from the token.");
        var identityUser = await _userManager.FindByEmailAsync(userEmailClaim.Value);
        var refreshedAccessToken = GenerateTokenString(identityUser);
        return new LoginResponse { AccessToken = refreshedAccessToken, IsLoggedIn = true };
    }

    private string GenerateTokenString(User user)
    {
        var securityKey = GetSymmetricJwtSecurityKey(_config);
        var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature);
        var claims = new List<Claim>
        {
            new(ClaimTypeToIdentifyUserOn, user.Id.ToString())
        };

        var securityToken = new JwtSecurityToken(
            expires: GetJwtExpiration(),
            signingCredentials: signingCredentials,
            claims: claims
        );

        var jwtPayload = BuildJwtPayload(user);

        // Custom data
        foreach (var property in jwtPayload.GetType().GetProperties())
        {
            var propertyValue = property.GetValue(jwtPayload);
            if (propertyValue != null)
            {
                string camelCasePropertyName = ToCamelCase(property.Name);
                securityToken.Payload[camelCasePropertyName] = propertyValue;
            }
        }

        string tokenString = new JwtSecurityTokenHandler().WriteToken(securityToken);
        return tokenString;
    }
    private static JwtSecurityToken DecodeJwt(string accessToken)
    {
        return new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
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

    private static DateTime GetJwtExpiration()
    {
        return DateTime.Now.AddHours(4);
    }

    private static SymmetricSecurityKey GetSymmetricJwtSecurityKey(IConfiguration config)
    {
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetJwtPrivateKey(config)));
    }

    private static string GetJwtPrivateKey(IConfiguration config)
    {
        var jwtPrivateKey = Configuration.JwtPrivateKey;

        var key = config[$"{jwtPrivateKey}"];

        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException("JWT Private Key is missing or not configured.");
        }

        return key;
    }

    private static JwtPayload BuildJwtPayload(User user)
    {
        return new JwtPayload
        {
            Email = user.Email,
            Sub = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            IsAdmin = user.IsAdmin
        };
    }

    private static string ToCamelCase(string str)
    {
        if (string.IsNullOrEmpty(str) || char.IsLower(str[0]))
            return str;

        return char.ToLowerInvariant(str[0]) + str[1..];
    }

}

