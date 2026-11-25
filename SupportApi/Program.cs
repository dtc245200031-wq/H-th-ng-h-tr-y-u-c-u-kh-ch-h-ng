using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSignalR();
builder.Services.AddSingleton<TicketStore>();

var app = builder.Build();
app.MapHub<TicketHub>("/hubs/tickets");

app.MapPost("/tickets", async (
    CreateTicketRequest request,
    TicketStore store,
    IHubContext<TicketHub> hub) =>
{
    if (string.IsNullOrWhiteSpace(request.RequesterId) ||
        string.IsNullOrWhiteSpace(request.Subject) ||
        string.IsNullOrWhiteSpace(request.Description))
    {
        return Results.BadRequest(new { message = "RequesterId, Subject và Description là bắt buộc." });
    }

    var ticket = store.Create(request);
    await hub.Clients.All.SendAsync("TicketUpdated", ticket);
    return Results.Created($"/tickets/{ticket.Id}", ticket);
});

app.MapGet("/tickets", (TicketStore store) => Results.Ok(store.GetAll()));

app.MapGet("/tickets/{id:int}", (int id, TicketStore store) =>
    store.TryGet(id, out var ticket)
        ? Results.Ok(ticket)
        : Results.NotFound(new { message = $"Không tìm thấy yêu cầu {id}." }));

app.MapPost("/tickets/{id:int}/queue", async (
    int id,
    TicketStore store,
    IHubContext<TicketHub> hub) =>
{
    var ticket = store.Queue(id);
    if (ticket is null)
        return Results.NotFound(new { message = $"Không tìm thấy yêu cầu {id}." });

    await hub.Clients.All.SendAsync("TicketUpdated", ticket);
    return Results.Accepted($"/tickets/{ticket.Id}", ticket);
});

app.MapPost("/tickets/{id:int}/assign", async (
    int id,
    AssignTicketRequest request,
    TicketStore store,
    IHubContext<TicketHub> hub) =>
{
    if (string.IsNullOrWhiteSpace(request.AssigneeId))
        return Results.BadRequest(new { message = "AssigneeId là bắt buộc." });

    var ticket = store.Assign(id, request.AssigneeId);
    if (ticket is null)
        return Results.NotFound(new { message = $"Không tìm thấy yêu cầu {id}." });

    await hub.Clients.All.SendAsync("TicketUpdated", ticket);
    return Results.Ok(ticket);
});

app.Run();

public sealed class TicketHub : Hub;

public sealed record CreateTicketRequest(string RequesterId, string Subject, string Description);
public sealed record AssignTicketRequest(string AssigneeId);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TicketStatus
{
    New,
    Assigned,
    InProgress,
    Resolved
}

public sealed class Ticket
{
    public int Id { get; init; }
    public required string RequesterId { get; init; }
    public required string Subject { get; init; }
    public required string Description { get; init; }
    public TicketStatus Status { get; set; } = TicketStatus.New;
    public string? AssigneeId { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<TicketHistoryEntry> History { get; } = [];
}

public sealed record TicketHistoryEntry(
    DateTimeOffset At,
    TicketStatus Status,
    string? AssigneeId,
    string Action);

public sealed class TicketStore
{
    private readonly ConcurrentDictionary<int, Ticket> _tickets = new();
    private readonly object _gate = new();
    private int _nextId;

    public Ticket Create(CreateTicketRequest request)
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            var ticket = new Ticket
            {
                Id = Interlocked.Increment(ref _nextId),
                RequesterId = request.RequesterId.Trim(),
                Subject = request.Subject.Trim(),
                Description = request.Description.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };
            ticket.History.Add(new TicketHistoryEntry(now, TicketStatus.New, null, "Created"));
            _tickets[ticket.Id] = ticket;
            return Clone(ticket);
        }
    }

    public IReadOnlyList<Ticket> GetAll()
    {
        lock (_gate)
            return _tickets.Values
                .OrderByDescending(ticket => ticket.CreatedAt)
                .Select(Clone)
                .ToList();
    }

    public bool TryGet(int id, out Ticket? ticket)
    {
        lock (_gate)
        {
            if (_tickets.TryGetValue(id, out var found))
            {
                ticket = Clone(found);
                return true;
            }

            ticket = null;
            return false;
        }
    }

    public Ticket? Queue(int id)
    {
        lock (_gate)
        {
            if (!_tickets.TryGetValue(id, out var ticket))
                return null;

            var now = DateTimeOffset.UtcNow;
            ticket.Status = TicketStatus.New;
            ticket.UpdatedAt = now;
            ticket.History.Add(new TicketHistoryEntry(
                now,
                TicketStatus.New,
                ticket.AssigneeId,
                "Added to support queue"));
            return Clone(ticket);
        }
    }

    public Ticket? Assign(int id, string assigneeId)
    {
        lock (_gate)
        {
            if (!_tickets.TryGetValue(id, out var ticket))
                return null;

            var now = DateTimeOffset.UtcNow;
            ticket.AssigneeId = assigneeId.Trim();
            ticket.Status = TicketStatus.Assigned;
            ticket.UpdatedAt = now;
            ticket.History.Add(new TicketHistoryEntry(
                now,
                TicketStatus.Assigned,
                ticket.AssigneeId,
                "Assigned"));
            return Clone(ticket);
        }
    }

    private static Ticket Clone(Ticket source)
    {
        var ticket = new Ticket
        {
            Id = source.Id,
            RequesterId = source.RequesterId,
            Subject = source.Subject,
            Description = source.Description,
            Status = source.Status,
            AssigneeId = source.AssigneeId,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt
        };
        ticket.History.AddRange(source.History);
        return ticket;
    }
}
