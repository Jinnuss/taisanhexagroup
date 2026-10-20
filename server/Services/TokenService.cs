using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Hexa.Api.Models;
using Microsoft.IdentityModel.Tokens;
namespace Hexa.Api.Services;
public class TokenService(IConfiguration config)
{
    public string Create(UserRow user)
    {
        var now=DateTime.UtcNow;
        var token=new JwtSecurityToken(config["Jwt:Issuer"],config["Jwt:Audience"],[
            new Claim("sub",user.Id.ToString()),new Claim("role",user.Role),
            new Claim("ver",user.TokenVersion.ToString()), new Claim("jti",Guid.NewGuid().ToString())
        ],now,now.AddHours(Math.Clamp(config.GetValue("Jwt:Hours",8),1,24)),
        new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)),SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
