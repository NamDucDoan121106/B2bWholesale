using B2BWholesale.Core.Entities;
using B2BWholesale.Core.Services;
using Xunit;

namespace B2BWholesale.Tests;

public class OrderBusinessTests
{
    [Fact]
    public void NormalizeLines_DuplicateItemIds_MergesQuantities()
    {
        var lines = OrderService.NormalizeLines(new[]
        {
            new OrderLineRequest(10, 2), new OrderLineRequest(10, 3),
            new OrderLineRequest(20, 1)
        });
        Assert.Equal(2, lines.Count);
        Assert.Equal(5, Assert.Single(lines, x => x.ItemId == 10).Quantity);
        Assert.Equal(1, Assert.Single(lines, x => x.ItemId == 20).Quantity);
    }

    [Fact]
    public void NormalizeLines_NoItems_ThrowsValidationError()
    {
        Assert.Throws<OrderValidationException>(() => OrderService.NormalizeLines(Array.Empty<OrderLineRequest>()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void NormalizeLines_NonPositiveQuantity_ThrowsValidationError(int quantity)
    {
        Assert.Throws<OrderValidationException>(() => OrderService.NormalizeLines(new[] { new OrderLineRequest(1, quantity) }));
    }

    [Fact]
    public void NormalizeLines_QuantityAboveLimit_ThrowsValidationError()
    {
        Assert.Throws<OrderValidationException>(() => OrderService.NormalizeLines(new[]
        {
            new OrderLineRequest(1, OrderService.MaxQuantityPerLine + 1)
        }));
    }

    [Fact]
    public void NormalizeLines_DuplicatesThatExceedLimit_AreRejected()
    {
        Assert.Throws<OrderValidationException>(() => OrderService.NormalizeLines(new[]
        {
            new OrderLineRequest(1, 6000), new OrderLineRequest(1, 6000)
        }));
    }

    [Fact]
    public void FindShortages_EnoughStock_ReturnsNoShortage()
    {
        var items = new Dictionary<int, Item> { [1] = new() { ItemID = 1, ItemName = "Paper", StockQuantity = 10 } };
        var shortages = OrderService.FindShortages(new[] { new OrderLineRequest(1, 10) }, items);
        Assert.Empty(shortages);
    }

    [Fact]
    public void FindShortages_InsufficientStock_ReportsRequestedAndAvailable()
    {
        var items = new Dictionary<int, Item> { [1] = new() { ItemID = 1, ItemName = "Paper", StockQuantity = 4 } };
        var result = Assert.Single(OrderService.FindShortages(new[] { new OrderLineRequest(1, 7) }, items));
        Assert.Equal(1, result.ItemId);
        Assert.Equal("Paper", result.ItemName);
        Assert.Equal(7, result.Requested);
        Assert.Equal(4, result.Available);
    }

    [Fact]
    public void FindShortages_UnknownItem_IsReported()
    {
        var result = Assert.Single(OrderService.FindShortages(
            new[] { new OrderLineRequest(999, 2) }, new Dictionary<int, Item>()));
        Assert.Equal(999, result.ItemId);
        Assert.Equal(0, result.Available);
    }

    [Fact]
    public void CalculateTotal_SeveralOrderDetails_UsesDecimalAmounts()
    {
        var details = new[]
        {
            new OrderDetail { Quantity = 3, UnitAmount = 12.50m },
            new OrderDetail { Quantity = 2, UnitAmount = 8.25m }
        };
        Assert.Equal(54.00m, OrderService.CalculateTotal(details));
    }

    [Fact]
    public void OrderTotalAmount_UsesAllDetailLines()
    {
        var order = new Order();
        order.OrderDetails.Add(new OrderDetail { Quantity = 2, UnitAmount = 9.90m });
        order.OrderDetails.Add(new OrderDetail { Quantity = 1, UnitAmount = 3.20m });
        Assert.Equal(23.00m, order.TotalAmount);
    }

    [Theory]
    [InlineData(OrderStatuses.Pending, OrderStatuses.Confirmed, true)]
    [InlineData(OrderStatuses.Pending, OrderStatuses.Cancelled, true)]
    [InlineData(OrderStatuses.Confirmed, OrderStatuses.Shipped, true)]
    [InlineData(OrderStatuses.Shipped, OrderStatuses.Delivered, true)]
    [InlineData(OrderStatuses.Delivered, OrderStatuses.Cancelled, false)]
    [InlineData(OrderStatuses.Shipped, OrderStatuses.Pending, false)]
    [InlineData(OrderStatuses.Cancelled, OrderStatuses.Confirmed, false)]
    public void CanMove_EnforcesOrderWorkflow(string current, string next, bool expected)
    {
        Assert.Equal(expected, OrderStatuses.CanMove(current, next));
    }
}
