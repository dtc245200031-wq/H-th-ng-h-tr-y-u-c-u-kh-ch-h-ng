using System.Net.Http.Json;

var baseUrl = args.FirstOrDefault() ?? "http://localhost:5100";
using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };

while (true)
{
    Console.WriteLine();
    Console.WriteLine("=== HỆ THỐNG HỖ TRỢ YÊU CẦU KHÁCH HÀNG ===");
    Console.WriteLine("1. Tạo yêu cầu hỗ trợ");
    Console.WriteLine("2. Tra cứu danh sách/chi tiết yêu cầu");
    Console.WriteLine("0. Thoát");
    Console.Write("Chọn chức năng: ");

    switch (Console.ReadLine()?.Trim())
    {
        case "1":
            await CreateTicketAsync();
            break;
        case "2":
            await QueryTicketsAsync();
            break;
        case "0":
            return;
        default:
            Console.WriteLine("Lựa chọn không hợp lệ.");
            break;
    }
}

async Task CreateTicketAsync()
{
    Console.Write("RequesterId: ");
    var requesterId = Console.ReadLine() ?? "";
    Console.Write("Tiêu đề: ");
    var subject = Console.ReadLine() ?? "";
    Console.Write("Nội dung: ");
    var description = Console.ReadLine() ?? "";

    using var response = await client.PostAsJsonAsync("/tickets",
        new { requesterId, subject, description });
    var body = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"{(int)response.StatusCode} {response.ReasonPhrase}");
    Console.WriteLine(body);
}

async Task QueryTicketsAsync()
{
    Console.Write("Nhập mã yêu cầu (bỏ trống để xem danh sách): ");
    var input = Console.ReadLine()?.Trim();

    if (int.TryParse(input, out var id))
    {
        using var response = await client.GetAsync($"/tickets/{id}");
        Console.WriteLine(await response.Content.ReadAsStringAsync());
        return;
    }

    using var listResponse = await client.GetAsync("/tickets");
    Console.WriteLine(await listResponse.Content.ReadAsStringAsync());
}
