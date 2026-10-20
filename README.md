# Quản lý tài sản Hexa — ASP.NET Core

Bản chuyển đổi từ `QuanLyTaiSanHexa_V8-Deploy.zip`. Backend Node.js/Express được viết lại bằng C# trên ASP.NET Core .NET 10; giữ giao diện React/Vite và cấu trúc SQL Server V8. Đây là mã nguồn để chạy và kiểm thử trên máy bạn; chưa triển khai lên hệ thống đang dùng và chưa chạy với SQL Server của bạn.

## Chức năng đã viết

- Đăng nhập JWT; sử dụng lại mật khẩu bcrypt V8; quản lý tài khoản, khóa tài khoản, đổi mật khẩu.
- Danh sách tài sản, tìm kiếm, lọc, phân trang, thống kê, thêm/sửa/xóa.
- Công ty, loại tài sản; phân quyền admin/employee.
- Bàn giao cho tài khoản hoặc tên tự nhập, thu hồi, lịch sử.
- QR công khai theo `PublicId`, phần mềm cài đặt, tối đa 6 ảnh/tài sản, 5 MB/ảnh.
- Xuất Excel theo bộ lọc và quyền truy cập, file mẫu, nhập tối đa 5.000 dòng/lần, báo lỗi và rollback toàn bộ lần nhập.

## Công nghệ và vị trí code

| Thư mục/file | Vai trò |
|---|---|
| `client/src/App.jsx` | Màn hình quản lý tài sản React hiện có |
| `client/src/services/` | Gọi HTTP API ASP.NET Core |
| `server/Program.cs` | Cấu hình API, CORS, JWT, phân quyền, xử lý lỗi |
| `server/Models/Contracts.cs` | Các lớp C# cho tài sản, tài khoản, dữ liệu nhập và kiểm tra dữ liệu |
| `server/Controllers/` | Nhận yêu cầu từ giao diện; mỗi nhóm nghiệp vụ một controller |
| `server/Services/AssetService.cs` | Truy vấn tài sản, lọc theo công ty, transaction và audit |
| `server/Services/Database.cs` | Mở SQL Server, lấy người dùng hiện tại, quy tắc chung |
| `database/` | Tạo database thử nghiệm hoặc nâng cấp bản sao V8 |
| `tests/` | Kiểm tra logic, API không cần SQL và danh sách nghiệm thu |

Dùng Dapper và SQL có tham số để tương thích schema đã có. Không dùng Entity Framework migrations trong bản này. `Departments` chính là mục **Công ty** trên giao diện V8; không có bảng `Companies` mới. Giao diện vẫn viết bằng React/JavaScript, phần server đã chuyển sang C#.

## 1. Chuẩn bị

Cài .NET SDK 10, Node.js có npm, SQL Server/SQL Express, SSMS và `sqlcmd`. Mở terminal ở thư mục chứa README này. Kiểm tra:

```powershell
dotnet --version
node --version
sqlcmd -?
```

Nếu đang có database V8, dùng SSMS sao lưu và restore thành một database riêng để thử nghiệm. Không chạy lại `schema.sql` của bộ Node V8: file đó có lệnh xóa bảng.

## 2. Chuẩn bị database — chọn một cách

**Có dữ liệu V8:** restore bản sao, ví dụ `QuanLyTaiSanHexa_DotNet_Test`, rồi chạy:

```powershell
sqlcmd -S '.\SQLEXPRESS' -E -C -b -d QuanLyTaiSanHexa_DotNet_Test -i database/upgrade-v8-to-netcore.sql
```

Thay tên SQL Server và database đúng máy bạn. Script này kiểm tra các thành phần V8 chính và thêm cột `Users.TokenVersion` nếu thiếu. Không đổi ID, PublicId, mật khẩu, bảng hoặc dữ liệu tài sản. Nếu báo không đúng schema V8, dừng và đối chiếu schema thực tế.

**Chưa có dữ liệu, muốn chạy thử từ đầu:**

```powershell
sqlcmd -S '.\SQLEXPRESS' -E -C -b -i database/create-test-database.sql
```

Script tạo database mới `QuanLyTaiSanHexa_DotNet_Test`, không có tài khoản/mật khẩu mẫu. Dừng nếu database đã tồn tại. Cả hai script dùng `:On Error exit`: chạy bằng sqlcmd như trên, hoặc bật **Query → SQLCMD Mode** trong SSMS. `-C`/TrustServerCertificate chỉ phục vụ SQL cục bộ với chứng chỉ tự ký.

## 3. Cấu hình backend

```powershell
powershell -ExecutionPolicy Bypass -File scripts/setup-local.ps1
```

Nhấn Enter nếu dùng `.\SQLEXPRESS`, Windows Authentication và database thử nghiệm như trên. Nếu khác, nhập connection string đúng môi trường của bạn. Script tạo khóa JWT ngẫu nhiên và lưu cùng connection string vào user-secrets trên máy bạn.

Ví dụ connection string Windows Authentication:

```text
Server=.\SQLEXPRESS;Database=QuanLyTaiSanHexa_DotNet_Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=True
```

Nếu dùng SQL Authentication, thay bằng `User ID=...;Password=...` do bạn quản lý; không gửi mật khẩu qua chat và không commit vào source. User-secrets chỉ dành cho phát triển, không mã hóa dữ liệu lưu tại máy. Tham khảo [Microsoft: quản lý cấu hình bí mật khi phát triển](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0).

## 4. Tài khoản đăng nhập

Nếu dùng bản sao V8: đăng nhập bằng tài khoản đang có. Phiên/token của backend Node không dùng lại được, cần đăng nhập lại một lần.

Nếu database mới chưa có admin:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/create-admin.ps1
```

Nhập email, họ tên và mật khẩu tự chọn. Script không ghi đè email đã có; mật khẩu được hash bcrypt. Cấu hình tạo admin tạm được xóa sau khi script kết thúc. Với database mới, đăng nhập rồi tạo Công ty và Loại tài sản trước; mã loại LAPTOP/PC/DESKTOP bật chức năng phần mềm.

## 5. Chạy ứng dụng

Terminal thứ nhất:

```powershell
```
dotnet run --project server 
- Backend: `http://localhost:5000`
- Kiểm tra SQL: `http://localhost:5000/api/health` — phải trả success=true.
- Danh sách API: `http://localhost:5000/swagger/index.html` — chỉ bật Development.

Terminal thứ hai:

```powershell
cd client
Copy-Item .env.example .env
npm ci
npm run dev -- --port 5173 --strictPort
```

Mở `http://localhost:5173`. Nếu đổi cổng hoặc tên miền, cập nhật `VITE_API_URL`, `ClientOrigins`, `PublicAppUrl`, `PublicApiUrl`. Biến Vite được nhúng lúc build; đổi cấu hình production cần build lại.

## Các quy tắc được làm rõ khi chuyển

- Admin quản lý tất cả công ty. Employee chỉ đọc tài sản có `DepartmentId` trùng công ty hiện tại, bao gồm danh sách, thống kê, Excel, chi tiết và các API con. Employee chưa gán công ty không thấy tài sản. Không chỉ ẩn nút giao diện: server kiểm tra quyền.
- Role, công ty, trạng thái khóa và phiên được đọc lại từ SQL khi kiểm tra JWT. Đổi mật khẩu hoặc sửa tài khoản làm token cũ hết hiệu lực. Tham khảo [Microsoft: phân quyền theo role](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles?view=aspnetcore-10.0).
- QR giữ hành vi V8: ai có đường dẫn PublicId có thể xem trang công khai và ảnh, kể cả không đăng nhập. Trang này hiển thị tên người sử dụng, IP, serial, lịch sử cấp phát và phần mềm; không hiển thị giá mua hoặc ghi chú quản trị. Phân quyền employee không áp dụng cho đường dẫn QR công khai.
- Bàn giao cho tài khoản có công ty sẽ chuyển công ty của tài sản sang công ty của người nhận. Với lượt bàn giao đang hoạt động, phải thu hồi trước khi sửa người sử dụng/công ty/trạng thái. Xóa tài sản có lịch sử bàn giao bị chặn bởi khóa ngoại để giữ lịch sử.
- Danh mục và công ty trong Excel phải khớp mã/tên đã có; không tự tạo mới. Dùng file mẫu của API mới. Giá mua nên dùng ô số, ngày mua dùng ô ngày Excel hoặc yyyy-MM-dd/dd/MM/yyyy.
- Không có mật khẩu mặc định, không lưu mật khẩu rõ trong bảng Users. Mật khẩu mới cần 8 ký tự trở lên, hoa/thường/số/ký tự đặc biệt, tối đa 72 byte UTF-8.

## Kiểm tra trước khi thay backend thật

```powershell
dotnet build server
dotnet run --project tests
python tests/smoke.py
cd client
npm run build
```

`smoke.py` yêu cầu Python 3 và backend đã build Debug. Nó tự chạy API trên cổng ngẫu nhiên, không kết nối database. Kết quả kiểm tra đã thực hiện nằm trong `tests/VERIFICATION.md`. Dùng `tests/SQL-ACCEPTANCE.md` để nghiệm thu trên bản sao database; việc biên dịch thành công không xác nhận mọi truy vấn SQL chạy được trên schema thực tế của bạn.

## Chạy production

Có Dockerfile cho backend:

```text
docker build -t hexa-api ./server
```

Container lắng nghe 8080, chạy user không phải root. Cấu hình môi trường bằng hệ thống secrets của host:

| Biến | Giá trị |
|---|---|
| `ConnectionStrings__DefaultConnection` | Connection string SQL thật, quyền đủ cho nghiệp vụ |
| `Jwt__Key` | Khóa ngẫu nhiên ít nhất 32 byte |
| `ClientOrigins__0` | Origin HTTPS chính xác của giao diện, không có dấu / cuối |
| `PublicAppUrl` | URL gốc HTTPS của React |
| `PublicApiUrl` | URL gốc HTTPS của API, không thêm /api |
| `ASPNETCORE_ENVIRONMENT` | Production |

Đặt API sau HTTPS reverse proxy. Nếu có proxy, cấu hình forwarded headers chỉ tin cậy proxy thực tế để rate limiter phân biệt IP người dùng; mặc định limiter dùng IP kết nối và có thể áp dụng chung cho proxy. Không bật Swagger production.

Frontend build bằng `npm ci` và `npm run build`, thư mục đầu ra `client/dist`. Có thể giữ host React/Vercel hiện có rồi trỏ `VITE_API_URL` sang backend mới. Chưa có thao tác publish/deploy trong bản bàn giao này.

Chỉ chuyển địa chỉ API của giao diện thật sau khi nghiệm thu bản sao SQL. Dừng ghi ở backend cũ trong thời điểm chuyển, backup SQL rồi nâng cấp cột TokenVersion. Muốn quay lại: trỏ giao diện về backend Node cũ, đăng nhập lại; cột TokenVersion bổ sung không ảnh hưởng các truy vấn V8 hiện có. Nếu đã phát sinh dữ liệu mới, cần đối soát trước khi restore backup để không mất dữ liệu mới.
