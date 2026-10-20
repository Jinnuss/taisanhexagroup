using System.Data;
using System.Text.Json;
using Dapper;
using Hexa.Api.Models;
using Microsoft.Data.SqlClient;
namespace Hexa.Api.Services;
public class AssetService(Database db,CurrentUser user)
{
    public const string SelectSql="""
        SELECT a.Id,a.PublicId,a.AssetCode,a.AssetName,a.CategoryId,c.CategoryCode,c.CategoryName,
        a.DepartmentId,d.DepartmentCode,d.DepartmentName,a.AssignedUserId,a.AssignedTo,a.IpAddress,
        a.SerialNumber,a.PurchaseDate,a.PurchasePrice,a.Status,a.Description,a.ImageUrl,a.CreatedAt,a.UpdatedAt
        FROM dbo.Assets a LEFT JOIN dbo.AssetCategories c ON c.Id=a.CategoryId
        LEFT JOIN dbo.Departments d ON d.Id=a.DepartmentId
        """;
    public const string FilterSql="""
         WHERE (@admin=1 OR (@scope IS NOT NULL AND a.DepartmentId=@scope))
         AND (@departmentId IS NULL OR a.DepartmentId=@departmentId)
         AND (@categoryId IS NULL OR a.CategoryId=@categoryId)
         AND (@status IS NULL OR a.Status=@status)
         AND (@search IS NULL OR a.AssetCode LIKE @search OR a.AssetName LIKE @search
         OR a.SerialNumber LIKE @search OR a.AssignedTo LIKE @search OR a.IpAddress LIKE @search OR a.Description LIKE @search)
        """;
    public object Parameters(AssetFilter f)=>new {admin=user.IsAdmin,scope=user.DepartmentId,
        f.DepartmentId,f.CategoryId,status=Rules.Text(f.Status),search=Rules.Text(f.Search) is string s?$"%{s}%":null,
        offset=(f.Page-1)*f.PageSize,f.PageSize};
    public async Task<object> List(AssetFilter f)
    {
        await using var c=await db.Open();var p=Parameters(f);
        var items=(await c.QueryAsync<AssetRow>(SelectSql+FilterSql+" ORDER BY a.Id DESC OFFSET @offset ROWS FETCH NEXT @PageSize ROWS ONLY",p)).ToList();
        var total=await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Assets a"+FilterSql,p);
        return new {items,pagination=new {page=f.Page,pageSize=f.PageSize,total,totalPages=Math.Max(1,(int)Math.Ceiling(total/(double)f.PageSize))}};
    }
    public async Task<List<AssetRow>> Export(AssetFilter f)
    { await using var c=await db.Open();return (await c.QueryAsync<AssetRow>(SelectSql+FilterSql+" ORDER BY a.Id DESC",Parameters(f))).ToList(); }
    public async Task<AssetRow> Get(int id)
    {
        await using var c=await db.Open();
        var a=await c.QuerySingleOrDefaultAsync<AssetRow>(SelectSql+" WHERE a.Id=@id AND (@admin=1 OR (@scope IS NOT NULL AND a.DepartmentId=@scope))",new {id,admin=user.IsAdmin,scope=user.DepartmentId});
        return a ?? throw new ApiException(404,"Không tìm thấy tài sản.");
    }
    public async Task<object> Stats()
    {
        await using var c=await db.Open();var p=new {admin=user.IsAdmin,scope=user.DepartmentId};
        const string scope=" WHERE (@admin=1 OR (@scope IS NOT NULL AND a.DepartmentId=@scope)) ";
        var departments=await c.QueryAsync("""
            SELECT d.Id DepartmentId,COALESCE(d.DepartmentCode,N'CHUA_GAN') DepartmentCode,
            COALESCE(d.DepartmentName,N'Chưa gán') DepartmentName,COUNT(*) Total,
            SUM(CASE WHEN a.Status='using' THEN 1 ELSE 0 END) InUse,
            SUM(CASE WHEN a.Status<>'using' THEN 1 ELSE 0 END) OtherCount
            FROM dbo.Assets a LEFT JOIN dbo.Departments d ON d.Id=a.DepartmentId
            """+scope+" GROUP BY d.Id,d.DepartmentCode,d.DepartmentName ORDER BY COUNT(*) DESC",p);
        var overall=await c.QuerySingleAsync("""
            SELECT COUNT(*) Total,COALESCE(SUM(CASE WHEN a.Status='using' THEN 1 ELSE 0 END),0) InUse,
            COALESCE(SUM(CASE WHEN a.Status<>'using' THEN 1 ELSE 0 END),0) OtherCount FROM dbo.Assets a
            """+scope,p);
        return new {overall,departments};
    }
    public static void Normalize(AssetInput a)
    {
        a.AssetCode=a.AssetCode.Trim();a.AssetName=a.AssetName.Trim();a.AssignedTo=Rules.Text(a.AssignedTo);
        a.IpAddress=Rules.Text(a.IpAddress);a.SerialNumber=Rules.Text(a.SerialNumber);a.Description=Rules.Text(a.Description);
        a.Status=Rules.Status(a.Status,a.AssignedTo);
    }
    public static async Task Audit(SqlConnection c,SqlTransaction tx,int id,string code,string action,string description,int actor,object? oldData=null,object? newData=null)
    {
        await c.ExecuteAsync("""
            INSERT dbo.AssetHistory(AssetId,AssetCode,ActionType,Description,OldData,NewData,PerformedBy)
            VALUES(@id,@code,@action,@description,@oldJson,@newJson,@actor)
            """,new {id,code,action,description,oldJson=oldData is null?null:JsonSerializer.Serialize(oldData),newJson=newData is null?null:JsonSerializer.Serialize(newData),actor},tx);
    }
    public static async Task<int> Insert(SqlConnection c,SqlTransaction tx,AssetInput a)
    {
        Normalize(a);
        return await c.ExecuteScalarAsync<int>("""
            INSERT dbo.Assets(AssetCode,AssetName,CategoryId,DepartmentId,AssignedTo,IpAddress,SerialNumber,
            PurchaseDate,PurchasePrice,Status,Description) OUTPUT INSERTED.Id
            VALUES(@AssetCode,@AssetName,@CategoryId,@DepartmentId,@AssignedTo,@IpAddress,@SerialNumber,
            @PurchaseDate,@PurchasePrice,@Status,@Description)
            """,a,tx);
    }
    public async Task<AssetRow> Create(AssetInput a)
    {
        await using var c=await db.Open();await using var tx=(SqlTransaction)await c.BeginTransactionAsync();
        int id=await Insert(c,tx,a);await Audit(c,tx,id,a.AssetCode,"CREATE","Tạo tài sản",user.Id,null,a);
        await tx.CommitAsync();return await Get(id);
    }
    public async Task<AssetRow> Update(int id,AssetInput a)
    {
        Normalize(a);await using var c=await db.Open();
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable);
        var old=await c.QuerySingleOrDefaultAsync<AssetRow>("SELECT * FROM dbo.Assets WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id",new {id},tx)
            ?? throw new ApiException(404,"Không tìm thấy tài sản.");
        var active=await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AssetAssignments WHERE AssetId=@id AND IsActive=1",new {id},tx);
        Rules.Require(active==0 || (old.AssignedTo==a.AssignedTo && old.DepartmentId==a.DepartmentId && a.Status=="using"),
            "Hãy thu hồi tài sản trước khi đổi người dùng, công ty hoặc trạng thái của lượt bàn giao đang hoạt động.",409);
        var p=new DynamicParameters(a);p.Add("id",id);
        await c.ExecuteAsync("""
            UPDATE dbo.Assets SET AssetCode=@AssetCode,AssetName=@AssetName,CategoryId=@CategoryId,
            DepartmentId=@DepartmentId,AssignedUserId=CASE WHEN AssignedTo=@AssignedTo THEN AssignedUserId ELSE NULL END,
            AssignedTo=@AssignedTo,IpAddress=@IpAddress,SerialNumber=@SerialNumber,PurchaseDate=@PurchaseDate,
            PurchasePrice=@PurchasePrice,Status=@Status,Description=@Description,UpdatedAt=SYSDATETIME() WHERE Id=@id
            """,p,tx);
        await Audit(c,tx,id,a.AssetCode,"UPDATE","Cập nhật tài sản",user.Id,old,a);await tx.CommitAsync();return await Get(id);
    }
    public async Task Delete(int id)
    {
        await using var c=await db.Open();await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable);
        var a=await c.QuerySingleOrDefaultAsync<AssetRow>("SELECT * FROM dbo.Assets WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id",new {id},tx)
            ?? throw new ApiException(404,"Không tìm thấy tài sản.");
        await Audit(c,tx,id,a.AssetCode,"DELETE","Xóa tài sản",user.Id,a);
        await c.ExecuteAsync("DELETE dbo.Assets WHERE Id=@id",new {id},tx);await tx.CommitAsync();
    }
}
