using System.Data;
using Dapper;
using Hexa.Api.Models;
using Hexa.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
namespace Hexa.Api.Controllers;
[ApiController,Route("api/assets"),Authorize]
public class AssignmentsController(Database db,CurrentUser user,AssetService assets,DetailService details):ControllerBase
{
    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> History(int id) {await assets.Get(id);return Ok(await details.History(id));}
    [HttpGet("assignee-suggestions"),Authorize(Roles="admin")]
    public async Task<IActionResult> Suggestions()
    {
        await using var c=await db.Open();
        return Ok(await c.QueryAsync("""
            SELECT DisplayName,UserId,EmployeeCode,FullName,DepartmentId,DepartmentCode FROM (
            SELECT CASE WHEN NULLIF(u.EmployeeCode,'') IS NULL THEN u.FullName ELSE u.EmployeeCode+N' - '+u.FullName END DisplayName,
            u.Id UserId,u.EmployeeCode,u.FullName,u.DepartmentId,d.DepartmentCode,1 SortGroup
            FROM dbo.Users u LEFT JOIN dbo.Departments d ON d.Id=u.DepartmentId WHERE u.IsActive=1
            UNION ALL SELECT DISTINCT a.AssignedTo,NULL,NULL,a.AssignedTo,NULL,NULL,2 FROM dbo.Assets a
            WHERE NULLIF(LTRIM(RTRIM(a.AssignedTo)),'') IS NOT NULL AND NOT EXISTS(
            SELECT 1 FROM dbo.Users u WHERE u.IsActive=1 AND (u.FullName=a.AssignedTo OR u.EmployeeCode+N' - '+u.FullName=a.AssignedTo))
            ) s ORDER BY SortGroup,DisplayName
            """));
    }
    [HttpPost("{id:int}/assign"),Authorize(Roles="admin")]
    public async Task<IActionResult> Assign(int id,AssignmentInput x)
    {
        await using var c=await db.Open();await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable);
        var a=await c.QuerySingleOrDefaultAsync<AssetRow>("SELECT * FROM dbo.Assets WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id",new {id},tx)
            ?? throw new ApiException(404,"Không tìm thấy tài sản.");
        Rules.Require(a.Status=="available","Tài sản phải ở trạng thái Có sẵn để bàn giao.",409);
        var target=x.UserId.HasValue?await c.QuerySingleOrDefaultAsync<UserRow>("SELECT * FROM dbo.Users WHERE Id=@id AND IsActive=1",new {id=x.UserId},tx):null;
        Rules.Require(!x.UserId.HasValue || target is not null,"Người nhận không tồn tại hoặc đã bị khóa.");
        var snapshot=target is null?Rules.Text(x.AssignedTo):string.IsNullOrEmpty(target.EmployeeCode)?target.FullName:$"{target.EmployeeCode} - {target.FullName}";
        Rules.Require(snapshot is not null && snapshot.Length<=255,"Tên người nhận bắt buộc và tối đa 255 ký tự.");
        var date=(x.AssignedDate ?? DateTime.Today).Date;
        await c.ExecuteAsync("""
            INSERT dbo.AssetAssignments(AssetId,UserId,AssignedToSnapshot,AssignedDate,AssignNote,AssignedBy,IsActive)
            VALUES(@id,@uid,@snapshot,@date,@note,@actor,1);
            UPDATE dbo.Assets SET AssignedUserId=@uid,AssignedTo=@snapshot,DepartmentId=COALESCE(@department,DepartmentId),
            Status='using',UpdatedAt=SYSDATETIME() WHERE Id=@id;
            """,new {id,uid=target?.Id,snapshot,date,note=Rules.Text(x.Note),actor=user.Id,department=target?.DepartmentId},tx);
        await AssetService.Audit(c,tx,id,a.AssetCode,"ASSIGN","Bàn giao tài sản cho "+snapshot,user.Id,null,x);
        await tx.CommitAsync();return Ok(new {message="Bàn giao tài sản thành công."});
    }
    [HttpPost("{id:int}/return"),Authorize(Roles="admin")]
    public async Task<IActionResult> Return(int id,AssignmentInput x)
    {
        await using var c=await db.Open();await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable);
        var a=await c.QuerySingleOrDefaultAsync<AssetRow>("SELECT * FROM dbo.Assets WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id",new {id},tx)
            ?? throw new ApiException(404,"Không tìm thấy tài sản.");
        var assignment=await c.QuerySingleOrDefaultAsync("SELECT Id,AssignedDate FROM dbo.AssetAssignments WITH(UPDLOCK,HOLDLOCK) WHERE AssetId=@id AND IsActive=1",new {id},tx);
        Rules.Require(assignment is not null || a.Status=="using","Tài sản chưa được bàn giao.",409);
        var date=(x.ReturnedDate ?? DateTime.Today).Date;
        if(assignment is not null) Rules.Require(date>=((DateTime)assignment.AssignedDate).Date,"Ngày thu hồi không được trước ngày bàn giao.");
        await c.ExecuteAsync("""
            UPDATE dbo.AssetAssignments SET ReturnedDate=@date,ReturnNote=@note,ReturnedBy=@actor,IsActive=0,UpdatedAt=SYSDATETIME()
            WHERE AssetId=@id AND IsActive=1;
            UPDATE dbo.Assets SET AssignedTo=NULL,AssignedUserId=NULL,Status='available',UpdatedAt=SYSDATETIME() WHERE Id=@id;
            """,new {id,date,note=Rules.Text(x.Note),actor=user.Id},tx);
        await AssetService.Audit(c,tx,id,a.AssetCode,"RETURN","Thu hồi tài sản",user.Id,a,x);
        await tx.CommitAsync();return Ok(new {message="Thu hồi tài sản thành công."});
    }
}
