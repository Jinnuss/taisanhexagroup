using Dapper;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using System.Text;
using Hexa.Api.Models;
namespace Hexa.Api.Services;

public sealed class ApiException(int status, string message) : Exception(message)
{ public int Status { get; } = status; }

public class Database(IConfiguration configuration)
{
    public async Task<SqlConnection> Open()
    {
        var cs = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(cs)) throw new ApiException(503, "Chưa cấu hình kết nối SQL Server.");
        var connection = new SqlConnection(cs);
        try { await connection.OpenAsync(); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }
    public const string UsersSql = """
        SELECT u.Id,u.EmployeeCode,u.FullName,u.Email,u.PasswordHash,u.Role,u.DepartmentId,
        u.IsActive,u.TokenVersion,u.CreatedAt,u.UpdatedAt,d.DepartmentCode,d.DepartmentName
        FROM dbo.Users u LEFT JOIN dbo.Departments d ON d.Id=u.DepartmentId
        """;
    public async Task<UserRow?> UserById(int id)
    { await using var c = await Open(); return await c.QuerySingleOrDefaultAsync<UserRow>(UsersSql+" WHERE u.Id=@id", new {id}); }
}

public class CurrentUser(IHttpContextAccessor accessor)
{
    private ClaimsPrincipal Principal => accessor.HttpContext!.User;
    public int Id => int.Parse(Principal.FindFirstValue("sub") ?? throw new ApiException(401,"Bạn chưa đăng nhập."));
    public bool IsAdmin => Principal.IsInRole("admin");
    public int? DepartmentId => int.TryParse(Principal.FindFirstValue("departmentId"),out var n) ? n : null;
    public static bool MayRead(bool admin, int? userDepartment, int? assetDepartment) => admin ||
        (userDepartment.HasValue && assetDepartment.HasValue && userDepartment == assetDepartment);
    public bool MayRead(int? department) => MayRead(IsAdmin, DepartmentId, department);
}
public static class Rules
{
    public static string? Text(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    public static void Require(bool condition,string message,int status=400)
    { if(!condition) throw new ApiException(status,message); }
    public static void Password(string? value)
    {
        Require(value is not null && value.Length >= 8 && Encoding.UTF8.GetByteCount(value) <= 72 &&
            value.Any(char.IsUpper) && value.Any(char.IsLower) && value.Any(char.IsDigit) && value.Any(x=>!char.IsLetterOrDigit(x)),
            "Mật khẩu cần ít nhất 8 ký tự, có hoa, thường, số, ký tự đặc biệt và tối đa 72 byte UTF-8.");
    }
    public static string Status(string status,string? assignee) => status is "maintenance" or "broken" or "liquidated"
        ? status : string.IsNullOrWhiteSpace(assignee) ? "available" : "using";
}
