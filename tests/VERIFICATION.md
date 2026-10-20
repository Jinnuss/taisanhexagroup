# Kết quả kiểm tra bản chuyển đổi

Môi trường kiểm tra: Linux, .NET SDK 10.0.401, ASP.NET Core runtime 10.0.12. Mã nguồn gốc: QuanLyTaiSanHexa_V8-Deploy.zip.

- Backend build: thành công, 0 cảnh báo, 0 lỗi.
- React/Vite production build: thành công, 88 modules.
- `dotnet run --project tests`: 16 kiểm tra đạt, gồm quy tắc công ty/nhân viên chưa gán, trạng thái, mật khẩu, bcrypt 2b, chặn MIME giả, không serialize password hash, Excel chữ Việt/ngày/giá và giá trị giống công thức vẫn là text.
- `python tests/smoke.py`: 34 API cần đăng nhập đều trả 401 khi không có token; Swagger, validation, JWT sai, CORS, rate limit đăng nhập đạt. Khi không cấu hình SQL, health trả 503. Chi tiết ở smoke-results.json.
- Đã đối chiếu các URL nghiệp vụ React V8 với controller mới; giữ hợp đồng PascalCase cho các dòng dữ liệu và camelCase cho envelope đăng nhập/phân trang.

**Chưa kiểm chứng:** thực thi DDL/truy vấn/transaction trên SQL Server thật; đăng nhập bằng dữ liệu người dùng thật; CRUD, bàn giao, tải ảnh và import/export qua toàn bộ giao diện với SQL; triển khai HTTPS/hosting. Không có SQL Server của người dùng trong môi trường kiểm tra. Các kiểm tra thuần logic không thay thế kiểm tra scope bằng truy vấn thực. Xem SQL-ACCEPTANCE.md trước khi chuyển production.

Không có thay đổi nào được áp dụng lên database hay website đang chạy của người dùng.
