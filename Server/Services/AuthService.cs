using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Server.Models;
using Server.DTOs;
using JwtPayload = Server.DTOs.JwtPayload;
using Server.Repositories;

namespace Server.Services;

public class AuthService : ApplicationService
{
    private readonly UsersRepository _UsersRepository;
    private readonly IConfiguration _config;
    private readonly PasswordHasher<User> _passwordHasher;

    public AuthService(UsersRepository UsersRepository, IConfiguration config)
    {
        _UsersRepository = UsersRepository;
        _config = config;
        _passwordHasher = new PasswordHasher<User>();
    }

    public async Task<User> RegisterUser(RegisterUserDTO registerUserDTO)
    {
        var userToRegister = new User
        {
            Email = registerUserDTO.Email,
            FirstName = registerUserDTO.FirstName,
            LastName = registerUserDTO.LastName,
            HashedPassword = "",
            IsAdmin = registerUserDTO.IsAdmin,
        };
        var HashedPassword = _passwordHasher.HashPassword(userToRegister, registerUserDTO.Password);
        userToRegister.HashedPassword = HashedPassword;
        var user = await _UsersRepository.AddUserAsync(userToRegister);
        return user;
    }


    public async Task<LoginResponse> Login(string email, string password)
    {
        var user = await _UsersRepository.FindUserByEmailAsync(email);
        if (user == null) return new LoginResponse { IsLoggedIn = false, };
        var passwordMatch = _passwordHasher.VerifyHashedPassword(user, user.HashedPassword, password);
        // 0 means failed comparison here, https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.passwordverificationresult?view=aspnetcore-8.0
        if (passwordMatch == 0) return new LoginResponse { IsLoggedIn = false };
        var payload = BuildJwtPayload(user);
        var accessToken = GenerateTokenString(payload);
        return new LoginResponse { AccessToken = accessToken, IsLoggedIn = true };
    }

    public async Task<LoginResponse> RefreshAccessToken(string accessToken)
    {
        var decodedAccessToken = DecodeJwt(accessToken);
        var userEmailClaim = decodedAccessToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email) ?? throw new InvalidOperationException("User ID claim is missing from the token.");
        var userToRefresh = await _UsersRepository.FindUserByEmailAsync(userEmailClaim.Value) ?? throw new InvalidOperationException("User for this ID does not exist");
        var payload = BuildJwtPayload(userToRefresh);
        var refreshedAccessToken = GenerateTokenString(payload);
        return new LoginResponse { AccessToken = refreshedAccessToken, IsLoggedIn = true };
    }

    private string GenerateTokenString(JwtPayload jwtPayload)
    {
        var securityKey = GetSymmetricJwtSecurityKey(_config);
        var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature);

        var securityToken = new JwtSecurityToken(
            expires: GetJwtExpiration(),
            signingCredentials: signingCredentials
        );

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
            NameClaimType = "default"
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

