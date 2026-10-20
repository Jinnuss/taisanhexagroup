using System.ComponentModel.DataAnnotations;
using System.Globalization;
using ClosedXML.Excel;
using Dapper;
using Hexa.Api.Models;
using Hexa.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
namespace Hexa.Api.Controllers;
[ApiController,Route("api/assets/excel"),Authorize]
public class ExcelController(Database db,AssetService assets,CurrentUser user):ControllerBase
{
    public static readonly string[] Headers=["Mã tài sản","Loại","Tên tài sản","Công ty","Người sử dụng","Địa chỉ IP","Serial","Ngày mua","Giá mua","Trạng thái","Ghi chú"];
    public static readonly Dictionary<string,string> Labels=new() {
        ["available"]="Có sẵn",["using"]="Đang cấp phát",["maintenance"]="Bảo trì",["broken"]="Đã hỏng",["liquidated"]="Đã thanh lý"
    };
    public static XLWorkbook Workbook(IEnumerable<AssetRow> rows,string title)
    {
        var wb=new XLWorkbook();var ws=wb.AddWorksheet(title);
        for(var i=0;i<Headers.Length;i++) ws.Cell(1,i+1).Value=Headers[i];
        int row=2;
        foreach(var a in rows) {
            ws.Cell(row,1).Value=a.AssetCode;ws.Cell(row,2).Value=a.CategoryName??"";
            ws.Cell(row,3).Value=a.AssetName;ws.Cell(row,4).Value=a.DepartmentName??"";
            ws.Cell(row,5).Value=a.AssignedTo??"";ws.Cell(row,6).Value=a.IpAddress??"";
            ws.Cell(row,7).Value=a.SerialNumber??"";
            if(a.PurchaseDate.HasValue)ws.Cell(row,8).Value=a.PurchaseDate.Value;
            if(a.PurchasePrice.HasValue)ws.Cell(row,9).Value=(double)a.PurchasePrice.Value;
            ws.Cell(row,10).Value=Labels.GetValueOrDefault(a.Status,a.Status);ws.Cell(row,11).Value=a.Description??"";row++;
        }
        ws.Row(1).Style.Font.Bold=true;ws.Row(1).Style.Font.FontColor=XLColor.White;
        ws.Row(1).Style.Fill.BackgroundColor=XLColor.FromHtml("124E78");
        ws.Columns(1,11).Width=22;ws.Column(3).Width=35;ws.Column(11).Width=35;
        ws.Column(8).Style.DateFormat.Format="dd/mm/yyyy";ws.Column(9).Style.NumberFormat.Format="#,##0";
        ws.SheetView.FreezeRows(1);ws.Range(1,1,Math.Max(1,row-1),11).SetAutoFilter();return wb;
    }
    private IActionResult Download(XLWorkbook wb,string name)
    {using(wb){using var ms=new MemoryStream();wb.SaveAs(ms);return File(ms.ToArray(),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",name);}}
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] AssetFilter f)=>Download(Workbook(await assets.Export(f),"Danh sách tài sản"),"tai-san-hexa.xlsx");
    [HttpGet("template")]
    public IActionResult Template()
    {
        var wb=Workbook([],"Mẫu import");var guide=wb.AddWorksheet("Hướng dẫn");
        guide.Cell(1,1).Value="Nhập dữ liệu ở sheet Mẫu import; giữ nguyên 11 cột.";
        guide.Cell(2,1).Value="Loại và Công ty phải khớp mã hoặc tên đã có. Ngày mua dùng ô ngày Excel hoặc yyyy-MM-dd / dd/MM/yyyy.";
        guide.Cell(3,1).Value="Trạng thái: Có sẵn, Đang cấp phát, Bảo trì, Đã hỏng, Đã thanh lý. Có người sử dụng sẽ là Đang cấp phát.";
        guide.Cell(4,1).Value="Nếu một dòng lỗi, toàn bộ lần nhập không được lưu. Tối đa 5000 dòng mỗi lần.";
        guide.Column(1).Width=100;return Download(wb,"mau-import-tai-san.xlsx");
    }
    private static XLWorkbook ReadWorkbook(Stream stream)
    {
        try { return new XLWorkbook(stream); }
        catch(Exception) { throw new ApiException(400,"Không đọc được workbook. Hãy dùng file .xlsx theo mẫu."); }
    }
    private static DateTime? Date(IXLCell cell)
    {
        if(cell.IsEmpty())return null;
        if(cell.TryGetValue<DateTime>(out var date))return date.Date;
        if(DateTime.TryParseExact(cell.GetString().Trim(),["yyyy-MM-dd","dd/MM/yyyy"],CultureInfo.InvariantCulture,DateTimeStyles.None,out date))return date;
        throw new FormatException("Ngày mua không hợp lệ.");
    }
    [HttpPost("import"),Authorize(Roles="admin"),RequestSizeLimit(11*1024*1024),RequestFormLimits(MultipartBodyLengthLimit=11*1024*1024)]
    public async Task<IActionResult> Import([FromForm] IFormFile file)
    {
        Rules.Require(file.Length is >0 and <=10485760 && Path.GetExtension(file.FileName).Equals(".xlsx",StringComparison.OrdinalIgnoreCase),"Chọn file .xlsx tối đa 10 MB.");
        using var ms=new MemoryStream();await file.CopyToAsync(ms);ms.Position=0;
        // Bound decompressed input before opening the workbook.
        try {
            using var zip=new System.IO.Compression.ZipArchive(ms,System.IO.Compression.ZipArchiveMode.Read,true);
            Rules.Require(zip.Entries.Count<=1000 && zip.Entries.Sum(e=>e.Length)<=100*1024*1024,"File Excel quá lớn sau giải nén.");
        } catch(System.IO.InvalidDataException) { throw new ApiException(400,"File không phải .xlsx hợp lệ."); }
        ms.Position=0;using var wb=ReadWorkbook(ms);var sheet=wb.Worksheet(1);
        for(int i=0;i<Headers.Length;i++) Rules.Require(sheet.Cell(1,i+1).GetString().Trim()==Headers[i],"Tên hoặc thứ tự cột không đúng. Hãy tải file mẫu.");
        int last=sheet.LastRowUsed()?.RowNumber()??1;Rules.Require(last<=5001,"Tối đa 5000 dòng mỗi lần.");
        await using var c=await db.Open();
        var categories=await c.QueryAsync("SELECT Id,CategoryCode,CategoryName FROM dbo.AssetCategories");
        var departments=await c.QueryAsync("SELECT Id,DepartmentCode,DepartmentName FROM dbo.Departments");
        var catMap=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);var depMap=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        foreach(var r in categories){catMap[(string)r.CategoryCode]=(int)r.Id;catMap[(string)r.CategoryName]=(int)r.Id;}
        foreach(var r in departments){depMap[(string)r.DepartmentCode]=(int)r.Id;depMap[(string)r.DepartmentName]=(int)r.Id;}
        var codes=new HashSet<string>(await c.QueryAsync<string>("SELECT AssetCode FROM dbo.Assets"),StringComparer.OrdinalIgnoreCase);
        var records=new List<AssetInput>();var errors=new List<object>();
        for(int row=2;row<=last;row++) {
            string S(int col)=>sheet.Cell(row,col).GetString().Trim();
            if(string.IsNullOrEmpty(S(1)) && string.IsNullOrEmpty(S(3)))continue;
            try {
                var status=S(10);if(string.IsNullOrEmpty(status))status="available";
                status=Labels.FirstOrDefault(x=>x.Value.Equals(status,StringComparison.OrdinalIgnoreCase)).Key ?? status.ToLowerInvariant();
                if(status is "đang sử dụng" or "đã sử dụng")status="using";
                int? category=S(2)==""?null:catMap.TryGetValue(S(2),out var ci)?ci:throw new FormatException("Không tìm thấy loại.");
                int? department=S(4)==""?null:depMap.TryGetValue(S(4),out var di)?di:throw new FormatException("Không tìm thấy công ty.");
                decimal? price=null;if(!sheet.Cell(row,9).IsEmpty()) {
                    if(sheet.Cell(row,9).TryGetValue<decimal>(out var amount)) price=amount;
                    else throw new FormatException("Giá mua không hợp lệ.");
                }
                var a=new AssetInput {AssetCode=S(1),AssetName=S(3),CategoryId=category,DepartmentId=department,
                    AssignedTo=Rules.Text(S(5)),IpAddress=Rules.Text(S(6)),SerialNumber=Rules.Text(S(7)),PurchaseDate=Date(sheet.Cell(row,8)),PurchasePrice=price,Status=status,Description=Rules.Text(S(11))};
                var validation=new List<ValidationResult>();
                if(!Validator.TryValidateObject(a,new ValidationContext(a),validation,true))throw new FormatException(string.Join("; ",validation.Select(v=>v.ErrorMessage)));
                if(!codes.Add(a.AssetCode))throw new FormatException("Mã tài sản trùng trong file hoặc đã tồn tại.");
                records.Add(a);
            } catch(FormatException ex){errors.Add(new {row,errors=new[]{ex.Message}});}
        }
        if(errors.Count>0)return BadRequest(new {message=$"Có {errors.Count} dòng lỗi. Chưa nhập dữ liệu.",errors=errors.Take(100)});
        Rules.Require(records.Count>0,"Không có dòng dữ liệu để nhập.");
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync();
        foreach(var a in records) {
            var id=await AssetService.Insert(c,tx,a);
            await AssetService.Audit(c,tx,id,a.AssetCode,"CREATE","Nhập từ Excel",user.Id,null,a);
        }
        await tx.CommitAsync();return Ok(new {message=$"Import thành công {records.Count} tài sản.",imported=records.Count});
    }
}
