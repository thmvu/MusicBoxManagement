# MusicBoxManagement

## Chạy trên máy phát triển hiện tại

Ứng dụng dùng ASP.NET MVC 5 / .NET Framework 4.7.2 và SQL Server. `Web.config`
trỏ tới database `MusicBoxManagementDev` trên SQL Server mặc định (`Data Source=.`)
bằng Windows Authentication. Mở solution trong Visual Studio rồi chạy bằng IIS Express.

Ngày 27/09/2026, database này được sao chép từ LocalDB để tránh lỗi LocalDB
khởi động thất thường. Bản `.mdf`/`.ldf` cũ trong `App_Data` vẫn được giữ,
nhưng web không dùng nữa. Dữ liệu mới khi chạy web sẽ nằm trong
`MusicBoxManagementDev`. Bản sao lưu trước khi chuyển nằm ở
`C:\Users\Public\Documents\MusicBoxManagementBackups`.

Khi chạy trên máy khác, hãy tạo/khôi phục database trên SQL Server của máy đó
và cập nhật `DefaultConnection` trong `Web.config` cho đúng instance và tên
database trước khi chạy web.
