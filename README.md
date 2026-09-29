# Music Box Management

Đồ án quản lý phòng Music Box, xây dựng bằng ASP.NET MVC 5, .NET Framework 4.7.2, Entity Framework 6 và SQL Server.

## Chạy project

1. Mở `MusicBoxManagement.slnx` bằng Visual Studio 2022 có workload **ASP.NET and web development**.
2. Bảo đảm SQL Server đang chạy. Mặc định [Web.config](MusicBoxManagement/Web.config) dùng Windows Authentication và database `MusicBoxManagementDev` trên instance mặc định: `Data Source=.`.
3. Nếu SQL Server của máy có tên khác, sửa `DefaultConnection` trong `Web.config`, ví dụ `Data Source=.\SQLEXPRESS`.
4. Mở **Tools → NuGet Package Manager → Package Manager Console**, chọn project `MusicBoxManagement`, rồi chạy:

```powershell
Update-Database
```

5. Nhấn `F5` để chạy bằng IIS Express.

Database đang dùng là `MusicBoxManagementDev`, không phải file LocalDB cũ trong `App_Data`.

## Tạo Admin khi cài ở máy mới

Trước khi chạy `Update-Database` lần đầu, có thể đặt hai biến môi trường PowerShell sau để migration seed một Admin. Mật khẩu cần ít nhất 12 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.

```powershell
$env:MUSICBOX_ADMIN_USERNAME = "admin@musicbox.local"
$env:MUSICBOX_ADMIN_PASSWORD = "MatKhauDemo!2026"
Update-Database
```

Sau khi Admin đã tồn tại, seed không tự thay đổi role hoặc mật khẩu tài khoản đó.

## Tạo dữ liệu demo phát triển

Seed demo là tùy chọn để tránh tự thêm khách và phiên sử dụng vào database thật. Trước khi chạy `Update-Database`, đặt biến sau trong PowerShell:

```powershell
$env:MUSICBOX_SEED_DEMO = "1"
$env:MUSICBOX_DEMO_PASSWORD = "MatKhauDemo!2026"
Update-Database
```

Mật khẩu demo cần tối thiểu 6 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt. Seed tạo tài khoản `demo.staff` và `demo.manager`, hai phòng Standard/VIP có ảnh, một phòng tạm khóa, hai khách demo, một booking ngày mai và một phiên walk-in đang hoạt động. Chạy lại không tạo trùng hoặc ghi đè tài khoản có sẵn; tài khoản demo có sẵn nhưng sai role sẽ làm seed dừng để tránh đổi quyền âm thầm.

## Luồng demo gợi ý

1. Đăng nhập Admin hoặc tài khoản demo `demo.staff` / `demo.manager`.
2. Vào **Xem phòng** để xem catalog; tạo đặt phòng hoặc vào **Vận hành → Đặt phòng**.
3. Check-in từ chi tiết đặt phòng, hoặc tạo khách trực tiếp tại **Phiên sử dụng**.
4. Tạo và xác nhận món cho phiên đang hoạt động.
5. Mở **Trả phòng**, chọn tiền mặt/chuyển khoản, xác nhận rồi xem hoặc in hóa đơn.
6. Xem lịch Ngày/Tuần, lịch sử khách hàng, Dashboard và các báo cáo.
7. Với Admin, vào **Quản trị** để xem nhật ký hoặc điều chỉnh quyền Staff/Manager.

## Báo cáo và Excel

Manager có `Report.View` và `Report.Export` mới xem/tải báo cáo. Staff luôn bị chặn báo cáo. Báo cáo theo ngày/tháng/loại phòng dùng `Invoice.PaidAt`; lượt phòng dùng giờ trả thực tế; báo cáo món chỉ tính Order đã hoàn tất thuộc phiên đã có hóa đơn. File Excel dùng đúng báo cáo và bộ lọc đang xem.

## Kiểm thử

Build project trước, sau đó chạy từng file trong thư mục `Tests` bằng Windows PowerShell. Ví dụ:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tests\Reports.Integration.ps1
```

Các bài `*.Integration.ps1` tự tạo và tự xóa database SQL Server test riêng. Không chạy chúng cùng database đang demo. Chạy toàn bộ bộ test bằng `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tests\Run-All.ps1`.
