using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace Hexa.Api.Models;

public class AssetInput
{
    [Required, StringLength(100)] public string AssetCode { get; set; } = "";
    [Required, StringLength(255)] public string AssetName { get; set; } = "";
    [Range(1, int.MaxValue)] public int? CategoryId { get; set; }
    [Range(1, int.MaxValue)] public int? DepartmentId { get; set; }
    [StringLength(255)] public string? AssignedTo { get; set; }
    [StringLength(50)] public string? IpAddress { get; set; }
    [StringLength(255)] public string? SerialNumber { get; set; }
    public DateTime? PurchaseDate { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999.99")] public decimal? PurchasePrice { get; set; }
    [Required, RegularExpression("^(available|using|maintenance|broken|liquidated)$")]
    public string Status { get; set; } = "available";
    [StringLength(1000)] public string? Description { get; set; }
}
public class AssetRow : AssetInput
{
    public int Id { get; set; }
    public Guid PublicId { get; set; }
    public int? AssignedUserId { get; set; }
    public string? CategoryCode { get; set; }
    public string? CategoryName { get; set; }
    public string? DepartmentCode { get; set; }
    public string? DepartmentName { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
public class UserRow
{
    public int Id { get; set; }
    public string? EmployeeCode { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    [JsonIgnore] public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "employee";
    public int? DepartmentId { get; set; }
    public string? DepartmentCode { get; set; }
    public string? DepartmentName { get; set; }
    public bool IsActive { get; set; }
    [JsonIgnore] public int TokenVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
public class LoginInput
{
    [Required, EmailAddress, StringLength(255)] public string Email { get; set; } = "";
    [Required, StringLength(256)] public string Password { get; set; } = "";
}
public class UserInput
{
    [StringLength(50)] public string? EmployeeCode { get; set; }
    [Required, StringLength(255)] public string FullName { get; set; } = "";
    [Required, EmailAddress, StringLength(255)] public string Email { get; set; } = "";
    public string? Password { get; set; }
    [Required, RegularExpression("^(admin|employee)$")] public string Role { get; set; } = "employee";
    [Range(1, int.MaxValue)] public int? DepartmentId { get; set; }
    public bool IsActive { get; set; } = true;
}
public class PasswordInput { [Required] public string Password { get; set; } = ""; }
public class DepartmentInput
{
    [Required, StringLength(50)] public string DepartmentCode { get; set; } = "";
    [Required, StringLength(255)] public string DepartmentName { get; set; } = "";
    [StringLength(500)] public string? Description { get; set; }
}
public class CategoryInput
{
    [Required, StringLength(50)] public string CategoryCode { get; set; } = "";
    [Required, StringLength(255)] public string CategoryName { get; set; } = "";
    [StringLength(500)] public string? Description { get; set; }
}
public class AssignmentInput
{
    [Range(1, int.MaxValue)] public int? UserId { get; set; }
    [StringLength(255)] public string? AssignedTo { get; set; }
    public DateTime? AssignedDate { get; set; }
    public DateTime? ReturnedDate { get; set; }
    [StringLength(1000)] public string? Note { get; set; }
}
public class SoftwareInput
{
    [Required, StringLength(255)] public string SoftwareName { get; set; } = "";
    [StringLength(100)] public string? Version { get; set; }
    [StringLength(255)] public string? Publisher { get; set; }
    public DateTime? InstalledDate { get; set; }
    [StringLength(500)] public string? Notes { get; set; }
}
public class AssetFilter
{
    [StringLength(255)] public string? Search { get; set; }
    [Range(1, int.MaxValue)] public int? DepartmentId { get; set; }
    [Range(1, int.MaxValue)] public int? CategoryId { get; set; }
    public string? Status { get; set; }
    [Range(1, 1000000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}
public class ImageRow
{
    public int Id { get; set; }
    public string FileName { get; set; } = "";
    public string MimeType { get; set; } = "";
    public int FileSize { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Url { get; set; } = "";
}
