using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Portfolio.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly ILogger<OrdersController> _logger;
    private readonly ActivitySource _activitySource;
    private static readonly Random _random = new();

    // Simulated in-memory store for demo
    private static readonly List<Order> _orders = new();

    public OrdersController(ILogger<OrdersController> logger, ActivitySource activitySource)
    {
        _logger = logger;
        _activitySource = activitySource;
    }

    /// <summary>
    /// Creates a new order. Generates traces + metrics + logs.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        using var activity = _activitySource.StartActivity("CreateOrder", ActivityKind.Internal);
        activity?.SetTag("order.customer_id", request.CustomerId);
        activity?.SetTag("order.amount", request.Amount);

        _logger.LogInformation("Creating order for customer {CustomerId} with amount {Amount}",
            request.CustomerId, request.Amount);

        // Simulate some business logic with child spans
        await ValidateCustomerAsync(request.CustomerId);
        await ProcessPaymentAsync(request.Amount);
        await SaveOrderAsync(request);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId,
            Amount = request.Amount,
            CreatedAt = DateTime.UtcNow,
            Status = "Created"
        };

        _orders.Add(order);

        activity?.SetTag("order.id", order.Id.ToString());
        activity?.SetStatus(ActivityStatusCode.Ok);

        _logger.LogInformation("Order {OrderId} created successfully", order.Id);

        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }

    /// <summary>
    /// Gets an order by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public IActionResult GetOrder(Guid id)
    {
        using var activity = _activitySource.StartActivity("GetOrder", ActivityKind.Internal);
        activity?.SetTag("order.id", id.ToString());

        var order = _orders.FirstOrDefault(o => o.Id == id);
        if (order is null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Order not found");
            _logger.LogWarning("Order {OrderId} not found", id);
            return NotFound(new { message = "Order not found" });
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
        return Ok(order);
    }

    /// <summary>
    /// Lists all orders (with optional simulated latency / error).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListOrders([FromQuery] bool slow = false, [FromQuery] bool fail = false)
    {
        using var activity = _activitySource.StartActivity("ListOrders", ActivityKind.Internal);

        if (fail)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Simulated failure");
            _logger.LogError("Simulated failure on ListOrders");
            return StatusCode(500, new { message = "Simulated internal error" });
        }

        if (slow)
        {
            // Simulate slow dependency
            using var slowSpan = _activitySource.StartActivity("SlowDatabaseQuery", ActivityKind.Client);
            await Task.Delay(_random.Next(800, 1500));
            slowSpan?.SetTag("db.system", "simulated");
        }

        activity?.SetTag("orders.count", _orders.Count);
        activity?.SetStatus(ActivityStatusCode.Ok);

        return Ok(_orders);
    }

    // ---------- Private helpers with child spans ----------

    private async Task ValidateCustomerAsync(string customerId)
    {
        using var activity = _activitySource.StartActivity("ValidateCustomer", ActivityKind.Internal);
        activity?.SetTag("customer.id", customerId);

        await Task.Delay(_random.Next(20, 80)); // simulate call

        if (string.IsNullOrWhiteSpace(customerId))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Invalid customer");
            throw new ArgumentException("CustomerId is required");
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    private async Task ProcessPaymentAsync(decimal amount)
    {
        using var activity = _activitySource.StartActivity("ProcessPayment", ActivityKind.Client);
        activity?.SetTag("payment.amount", amount);
        activity?.SetTag("payment.provider", "simulated-gateway");

        await Task.Delay(_random.Next(50, 200));

        // 5% chance of payment failure for demo purposes
        if (_random.NextDouble() < 0.05)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Payment declined");
            _logger.LogWarning("Payment of {Amount} was declined", amount);
            throw new InvalidOperationException("Payment declined by gateway");
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    private async Task SaveOrderAsync(CreateOrderRequest request)
    {
        using var activity = _activitySource.StartActivity("SaveOrder", ActivityKind.Internal);
        activity?.SetTag("db.system", "in-memory");
        activity?.SetTag("db.operation", "insert");

        await Task.Delay(_random.Next(10, 40));
        activity?.SetStatus(ActivityStatusCode.Ok);
    }
}

public record CreateOrderRequest(string CustomerId, decimal Amount);

public class Order
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}
