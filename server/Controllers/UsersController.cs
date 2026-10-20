using Dapper;
using Hexa.Api.Models;
using Hexa.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Hexa.Api.Controllers;
[ApiController,Route("api/users"),Authorize(Roles="admin")]
public class UsersController(Database db, CurrentUser current) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(string? search=null,string? role=null,bool? active=null,int? departmentId=null)
    {
        await using var c=await db.Open();
        return Ok(await c.QueryAsync<UserRow>(Database.UsersSql+" "+"""
            WHERE (@search IS NULL OR u.FullName LIKE @search OR u.Email LIKE @search OR u.EmployeeCode LIKE @search)
            AND (@role IS NULL OR u.Role=@role) AND (@active IS NULL OR u.IsActive=@active)
            AND (@departmentId IS NULL OR u.DepartmentId=@departmentId) ORDER BY u.IsActive DESC,u.FullName
            """,new {search=string.IsNullOrWhiteSpace(search)?null:$"%{search.Trim()}%",role,active,departmentId}));
    }
    private async Task ValidateCompany(UserInput x)
    {
        Rules.Require(x.Role=="admin" || x.DepartmentId.HasValue,"Nhân viên phải được gán công ty.");
        if(x.DepartmentId.HasValue) {
            await using var c=await db.Open();
            Rules.Require(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Departments WHERE Id=@id",new {id=x.DepartmentId})==1,"Công ty không tồn tại.");
        }
    }
    [HttpPost]
    public async Task<IActionResult> Create(UserInput x)
    {
        Rules.Password(x.Password);await ValidateCompany(x);
        await using var c=await db.Open();
        var id=await c.ExecuteScalarAsync<int>("""
            INSERT dbo.Users(EmployeeCode,FullName,Email,PasswordHash,Role,DepartmentId,IsActive)
            OUTPUT INSERTED.Id VALUES(@EmployeeCode,@FullName,@Email,@hash,@Role,@DepartmentId,1)
            """,new {EmployeeCode=Rules.Text(x.EmployeeCode)?.ToUpperInvariant(),FullName=x.FullName.Trim(),Email=x.Email.Trim().ToLowerInvariant(),hash=BCrypt.Net.BCrypt.HashPassword(x.Password!,12),x.Role,x.DepartmentId});
        return StatusCode(201,await db.UserById(id));
    }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id,UserInput x)
    {
        await ValidateCompany(x);
        Rules.Require(id!=current.Id || (x.IsActive && x.Role=="admin"),"Bạn không thể tự khóa hoặc hạ quyền.");
        await using var c=await db.Open();
        var count=await c.ExecuteAsync("""
            UPDATE dbo.Users SET EmployeeCode=@EmployeeCode,FullName=@FullName,Email=@Email,Role=@Role,
            DepartmentId=@DepartmentId,IsActive=@IsActive,TokenVersion=TokenVersion+1,UpdatedAt=SYSDATETIME() WHERE Id=@id
            """,new {id,EmployeeCode=Rules.Text(x.EmployeeCode)?.ToUpperInvariant(),FullName=x.FullName.Trim(),Email=x.Email.Trim().ToLowerInvariant(),x.Role,x.DepartmentId,x.IsActive});
        Rules.Require(count>0,"Không tìm thấy người dùng.",404);return Ok(await db.UserById(id));
    }
    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> Reset(int id,PasswordInput x)
    {
        Rules.Password(x.Password);await using var c=await db.Open();
        var n=await c.ExecuteAsync("UPDATE dbo.Users SET PasswordHash=@hash,TokenVersion=TokenVersion+1,UpdatedAt=SYSDATETIME() WHERE Id=@id",
            new {id,hash=BCrypt.Net.BCrypt.HashPassword(x.Password,12)});
        Rules.Require(n>0,"Không tìm thấy người dùng.",404);return Ok(new {message="Đã đổi mật khẩu và kết thúc các phiên đăng nhập cũ."});
    }
}
