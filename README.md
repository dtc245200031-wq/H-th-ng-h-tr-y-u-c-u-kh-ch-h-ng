# Hệ thống hỗ trợ yêu cầu khách hàng

Giải pháp .NET 10 gồm ASP.NET Core Minimal API và ứng dụng Console.

## Chạy API

```powershell
dotnet run --project .\SupportApi\SupportApi.csproj --urls http://localhost:5100
```

## Chạy Console

Chạy API trước, sau đó:

```powershell
dotnet run --project .\SupportConsole\SupportConsole.csproj -- http://localhost:5100
```

## Tạo yêu cầu

`POST /tickets` tạo yêu cầu mới. Dữ liệu bắt buộc: `requesterId`, `subject`,
`description`. Yêu cầu mới có trạng thái `New`.

## Tra cứu yêu cầu

- `GET /tickets` trả về danh sách yêu cầu.
- `GET /tickets/{id}` trả về chi tiết theo mã, hoặc `404` nếu không tồn tại.

## Đưa yêu cầu vào queue

`POST /tickets/{id}/queue` đưa yêu cầu hiện có vào hàng chờ xử lý.

## Gán nhân viên xử lý

`POST /tickets/{id}/assign` gán yêu cầu cho nhân viên theo `assigneeId` và cập nhật trạng thái thành `Assigned`.
