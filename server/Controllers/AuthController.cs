using Dapper;
using Hexa.Api.Models;
using Hexa.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Hexa.Api.Controllers;
[ApiController,Route("api/auth")]
public class AuthController(Database db,CurrentUser current,TokenService tokens) : ControllerBase
{
    private static readonly string DummyHash=BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString(),12);
    private static object Profile(UserRow u)=>new {id=u.Id,fullName=u.FullName,email=u.Email,role=u.Role,departmentId=u.DepartmentId,departmentName=u.DepartmentName};
    [HttpPost("login"),AllowAnonymous,EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginInput input)
    {
        await using var c=await db.Open();
        var user=await c.QuerySingleOrDefaultAsync<UserRow>(Database.UsersSql+" WHERE u.Email=@email",new {email=input.Email.Trim().ToLowerInvariant()});
        bool valid=false;
        try { valid=BCrypt.Net.BCrypt.Verify(input.Password,user?.PasswordHash ?? DummyHash); } catch(BCrypt.Net.SaltParseException) { }
        if(user is null || !valid || !user.IsActive) return Unauthorized(new {message="Email hoặc mật khẩu không chính xác, hoặc tài khoản đã bị khóa."});
        return Ok(new {message="Đăng nhập thành công.",token=tokens.Create(user),user=Profile(user)});
    }
    [HttpGet("me")]
    public async Task<IActionResult> Me()=>Ok(Profile((await db.UserById(current.Id))!));
}
