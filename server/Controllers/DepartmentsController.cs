using Dapper;
using Hexa.Api.Models;
using Hexa.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Hexa.Api.Controllers;
[ApiController,Route("api/departments"),Authorize]
public class DepartmentsController(Database db,CurrentUser user):ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        await using var c=await db.Open();
        return Ok(await c.QueryAsync("""
            SELECT r.Id,r.DepartmentCode,r.DepartmentName,r.Description,r.CreatedAt,
            (SELECT COUNT(*) FROM dbo.Assets a WHERE a.DepartmentId=r.Id AND
            (@admin=1 OR (@scope IS NOT NULL AND a.DepartmentId=@scope))) AssetCount
            FROM dbo.Departments r WHERE 1=1  AND (@admin=1 OR (@scope IS NOT NULL AND r.Id=@scope)) ORDER BY r.DepartmentName
            """,new {admin=user.IsAdmin,scope=user.DepartmentId}));
    }
    [HttpPost,Authorize(Roles="admin")]
    public async Task<IActionResult> Create(DepartmentInput x)
    {
        await using var c=await db.Open();
        return StatusCode(201,await c.QuerySingleAsync("INSERT dbo.Departments(DepartmentCode,DepartmentName,Description) OUTPUT INSERTED.* VALUES(@code,@name,@description)",
            new {code=x.DepartmentCode.Trim().ToUpperInvariant(),name=x.DepartmentName.Trim(),description=Rules.Text(x.Description)}));
    }
    [HttpPut("{id:int}"),Authorize(Roles="admin")]
    public async Task<IActionResult> Update(int id,DepartmentInput x)
    {
        await using var c=await db.Open();
        var row=await c.QuerySingleOrDefaultAsync("UPDATE dbo.Departments SET DepartmentCode=@code,DepartmentName=@name,Description=@description OUTPUT INSERTED.* WHERE Id=@id",
            new {id,code=x.DepartmentCode.Trim().ToUpperInvariant(),name=x.DepartmentName.Trim(),description=Rules.Text(x.Description)});
        if(row is null) throw new ApiException(404,"Không tìm thấy dữ liệu.");return Ok(row);
    }
    [HttpDelete("{id:int}"),Authorize(Roles="admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await using var c=await db.Open();
        var n=await c.ExecuteAsync("DELETE dbo.Departments WHERE Id=@id",new {id});
        Rules.Require(n>0,"Không tìm thấy dữ liệu.",404);return Ok(new {message="Đã xóa."});
    }
}
