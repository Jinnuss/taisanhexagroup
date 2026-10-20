# Nghiệm thu trên SQL Server bản sao

Chưa chạy các bước dưới đây trong môi trường bàn giao vì không có SQL Server/database của bạn. Chỉ thực hiện trên database thử nghiệm và tài khoản test. Giữ số lượng bản ghi trước/sau, thông báo lỗi và log để đối chiếu.

| Bước | Thao tác | Kết quả cần đạt |
|---|---|---|
| 1 | Chạy upgrade-v8-to-netcore.sql hai lần trên bản sao | Cả hai lần thành công; số tài sản, người dùng, PublicId và hash mật khẩu không đổi |
| 2 | Mở /api/health | 200, success=true |
| 3 | Đăng nhập admin V8 | Thành công bằng mật khẩu cũ; JSON không có PasswordHash |
| 4 | Tạo hai công ty A/B, loại LAPTOP, hai employee A/B | Lưu đúng ID, email; user trùng email/mã trả lỗi rõ |
| 5 | Tạo tài sản X thuộc A, Y thuộc B và Z chưa có công ty | Admin thấy cả ba; ngày mua trống và giá trống lưu được |
| 6 | Đăng nhập employee A | Chỉ thấy X; thống kê, đếm danh mục và xuất Excel không chứa Y/Z |
| 7 | Employee A gọi /api/assets?departmentId=<B> | Danh sách rỗng, không vượt qua scope |
| 8 | Employee A gọi /api/assets/<Y>, /history, /software, /images, /qr | Mỗi endpoint trả 404 |
| 9 | Employee A gọi POST/PUT/DELETE tài sản, công ty, loại; /api/users | 403; dữ liệu không đổi |
| 10 | Admin sửa công ty user A thành B; dùng lại token A cũ | 401; đăng nhập lại chỉ thấy tài sản B |
| 11 | Admin khóa hoặc reset mật khẩu user B | Token B cũ trả 401; mật khẩu cũ không đăng nhập được sau reset |
| 12 | Admin sửa X với mã trùng Y, giá âm, tên trắng | Bị từ chối; dữ liệu trước đó không bị thay đổi |
| 13 | Bàn giao X đang có sẵn cho user B | Có assignment active, Status=using; công ty tài sản chuyển sang B |
| 14 | Hai yêu cầu bàn giao X đồng thời | Tối đa một lượt active; yêu cầu xung đột trả 409 |
| 15 | Sửa trực tiếp người sử dụng/công ty X khi còn lượt active | 409; cần thu hồi trước |
| 16 | Thu hồi X với ngày trước ngày bàn giao; sau đó ngày hợp lệ | Lần đầu bị từ chối; lần sau đóng assignment, xóa người sử dụng, Status=available |
| 17 | Xem lịch sử sau tạo/sửa/bàn giao/thu hồi | Actor, thời gian và nội dung đúng; xóa tài sản có assignment bị chặn để giữ lịch sử |
| 18 | Thêm phần mềm cho LAPTOP với ngày cài trống; xóa lại | Thành công; không thêm được vào loại không phải máy tính |
| 19 | Upload ảnh PNG/JPG/WebP; đặt ảnh chính; xóa ảnh chính | Gallery cập nhật, chọn ảnh chính khác; ảnh public hiển thị được |
| 20 | Upload quá 6 ảnh, ảnh >5 MB, file văn bản giả ảnh | Bị từ chối, không lưu một phần lần upload |
| 21 | Quét QR trong trình duyệt ẩn danh | Hiển thị tài sản/ảnh/lịch sử công khai, không yêu cầu đăng nhập; giá/ghi chú quản trị không có trong JSON |
| 22 | Đổi imageId sang ảnh tài sản khác trong URL QR | 404 |
| 23 | Export theo tìm kiếm và công ty | File mở được, số dòng/bộ lọc/quyền đúng, tiếng Việt đúng |
| 24 | Import mẫu có 2 dòng đúng | Hai tài sản cùng audit được lưu; kiểu ngày/giá đúng |
| 25 | Import một dòng đúng và một dòng trùng mã/loại sai/ngày sai | 400, có số dòng và lỗi; không lưu dòng nào của lần nhập |
| 26 | Import file văn bản đổi đuôi .xlsx | 400, không có dữ liệu được lưu |
| 27 | Khởi động lại API | Dữ liệu vẫn tồn tại trong SQL; token vẫn dùng nếu khóa JWT chưa đổi và phiên hợp lệ |
| 28 | Trên bản sao, chạy lại backend V8 đọc dữ liệu | Xác nhận phương án quay lại và các hành vi thay đổi đã được chấp nhận |

Có thể dùng tab Network của trình duyệt để lấy URL/mã HTTP hoặc một REST client với `Authorization: Bearer <token test>`. Không chia sẻ token trong ảnh chụp/log. Kiểm tra API trực tiếp ở bước 7–11 vì ẩn nút trên giao diện không chứng minh phân quyền server.
