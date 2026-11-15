using System.Data;
using B2BWholesale.Core.Data;
using B2BWholesale.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace B2BWholesale.Core.Services;

public sealed record OrderLineRequest(int ItemId, int Quantity);

public sealed record StockShortage(int ItemId, string ItemName, int Requested, int Available);

public class InsufficientStockException : Exception
{
    public IReadOnlyList<StockShortage> Shortages { get; }

    public InsufficientStockException(IReadOnlyList<StockShortage> shortages)
        : base("Insufficient stock: " + string.Join("; ", shortages.Select(s =>
              $"{s.ItemName} (requested {s.Requested}, available {s.Available})")))
        => Shortages = shortages;
}

public class OrderValidationException : Exception
{
    public OrderValidationException(string message) : base(message) { }
}

/// <summary>
/// Order business logic shared by the web portal and the WinForms back office.
/// </summary>
public class OrderService
{
    public const int MaxQuantityPerLine = 10_000;
    private readonly WholesaleDbContext _db;

    public OrderService(WholesaleDbContext db) => _db = db;

    /// <summary>Merges duplicate items and validates quantities. Pure logic, no database.</summary>
    public static List<OrderLineRequest> NormalizeLines(IEnumerable<OrderLineRequest>? lines)
    {
        var merged = (lines ?? Enumerable.Empty<OrderLineRequest>())
            .Where(l => l.ItemId > 0)
            .GroupBy(l => l.ItemId)
            .Select(g => new OrderLineRequest(g.Key, g.Sum(l => l.Quantity)))
            .ToList();

        if (merged.Count == 0)
            throw new OrderValidationException("The order must contain at least one item.");
        if (merged.Any(l => l.Quantity <= 0))
            throw new OrderValidationException("Every order line must have a quantity greater than zero.");
        if (merged.Any(l => l.Quantity > MaxQuantityPerLine))
            throw new OrderValidationException($"A single line cannot exceed {MaxQuantityPerLine:N0} units.");
        return merged;
    }

    /// <summary>Pure stock check used before touching the database (and handy for unit tests).</summary>
    public static List<StockShortage> FindShortages(IEnumerable<OrderLineRequest> lines, IReadOnlyDictionary<int, Item> items) =>
        lines.Where(l => !items.ContainsKey(l.ItemId) || items[l.ItemId].StockQuantity < l.Quantity)
             .Select(l => items.TryGetValue(l.ItemId, out var i)
                 ? new StockShortage(l.ItemId, i.ItemName, l.Quantity, i.StockQuantity)
                 : new StockShortage(l.ItemId, $"Item #{l.ItemId} (not found)", l.Quantity, 0))
             .ToList();

    public static decimal CalculateTotal(IEnumerable<OrderDetail> details) => details.Sum(d => d.Quantity * d.UnitAmount);

    /// <summary>
    /// Places a master-detail order inside ONE database transaction:
    ///   1. insert the Order header,
    ///   2. insert all OrderDetail lines (price captured from Item.Price),
    ///   3. deduct Item.StockQuantity with an atomic conditional UPDATE
    ///      (WHERE StockQuantity >= @qty) so two agents can never oversell the same stock.
    /// If any line has insufficient stock, everything is rolled back and
    /// <see cref="InsufficientStockException"/> is thrown.
    /// </summary>
    public async Task<Order> PlaceOrderAsync(int agentId, IEnumerable<OrderLineRequest> requestedLines)
    {
        var lines = NormalizeLines(requestedLines);

        if (!await _db.Agents.AnyAsync(a => a.AgentID == agentId))
            throw new OrderValidationException("Agent account not found.");

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            var ids = lines.Select(l => l.ItemId).ToList();
            var items = await _db.Items.Where(i => ids.Contains(i.ItemID)).ToDictionaryAsync(i => i.ItemID);

            var shortages = FindShortages(lines, items);
            if (shortages.Count > 0)
                throw new InsufficientStockException(shortages);

            var order = new Order
            {
                AgentID = agentId,
                OrderDate = DateTime.Now,
                OrderStatus = OrderStatuses.Pending
            };
            foreach (var l in lines)
            {
                order.OrderDetails.Add(new OrderDetail
                {
                    ItemID = l.ItemId,
                    Quantity = l.Quantity,
                    UnitAmount = items[l.ItemId].Price
                });
            }
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();                       // steps 1 + 2

            foreach (var l in lines)                            // step 3
            {
                var affected = await _db.Items
                    .Where(i => i.ItemID == l.ItemId && i.StockQuantity >= l.Quantity)
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.StockQuantity, i => i.StockQuantity - l.Quantity));

                if (affected == 0)   // someone else bought the stock between our check and now
                {
                    var current = await _db.Items.AsNoTracking().Where(i => i.ItemID == l.ItemId)
                                                 .Select(i => i.StockQuantity).FirstOrDefaultAsync();
                    throw new InsufficientStockException(new[]
                        { new StockShortage(l.ItemId, items[l.ItemId].ItemName, l.Quantity, current) });
                }
            }

            await tx.CommitAsync();
            return order;
        }
        catch
        {
            await tx.RollbackAsync();
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    /// <summary>
    /// Moves an order along the workflow. Cancelling returns the stock to inventory
    /// in the same transaction.
    /// </summary>
    public async Task UpdateStatusAsync(int orderId, string newStatus, int? restrictToAgentId = null)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var order = await _db.Orders.Include(o => o.OrderDetails)
                                        .FirstOrDefaultAsync(o => o.OrderID == orderId)
                        ?? throw new OrderValidationException($"Order #{orderId} not found.");

            if (restrictToAgentId.HasValue && order.AgentID != restrictToAgentId.Value)
                throw new OrderValidationException($"Order #{orderId} not found.");

            if (!OrderStatuses.CanMove(order.OrderStatus, newStatus))
                throw new OrderValidationException(
                    $"Order #{orderId} cannot change from {order.OrderStatus} to {newStatus}.");

            if (newStatus == OrderStatuses.Cancelled)
            {
                foreach (var d in order.OrderDetails)
                {
                    await _db.Items.Where(i => i.ItemID == d.ItemID)
                        .ExecuteUpdateAsync(s => s.SetProperty(i => i.StockQuantity, i => i.StockQuantity + d.Quantity));
                }
            }

            order.OrderStatus = newStatus;
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    public Task<Order?> GetOrderAsync(int orderId, int? restrictToAgentId = null) =>
        _db.Orders.AsNoTracking()
            .Include(o => o.Agent)
            .Include(o => o.OrderDetails).ThenInclude(d => d.Item)
            .Where(o => o.OrderID == orderId && (restrictToAgentId == null || o.AgentID == restrictToAgentId))
            .FirstOrDefaultAsync();
}
