using B2BWholesale.Core.Data;
using B2BWholesale.Core.Entities;
using B2BWholesale.Core.Services;
using B2BWholesale.Web.Infrastructure;
using B2BWholesale.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace B2BWholesale.Web.Controllers;

/// <summary>
/// Task 3.2 - master-detail order placement (transactional stock deduction in OrderService).
/// Task 3.3 - "My Orders", order details and printable invoice.
/// Every query is filtered by the AgentID from the auth cookie, so agents only ever see their own orders.
/// </summary>
public class OrdersController : Controller
{
    private readonly WholesaleDbContext _db;
    private readonly OrderService _orders;
    private readonly ILogger<OrdersController> _log;

    public OrdersController(WholesaleDbContext db, OrderService orders, ILogger<OrdersController> log)
    {
        _db = db;
        _orders = orders;
        _log = log;
    }

    // GET /Orders  (My Orders)
    public async Task<IActionResult> Index(string? status)
    {
        var agentId = User.GetAgentId();
        var q = _db.Orders.AsNoTracking().Where(o => o.AgentID == agentId);
        if (!string.IsNullOrEmpty(status) && OrderStatuses.All.Contains(status))
            q = q.Where(o => o.OrderStatus == status);

        var vm = new MyOrdersViewModel
        {
            Status = status,
            Orders = await q.OrderByDescending(o => o.OrderDate)
                .Select(o => new OrderSummaryVm
                {
                    OrderID = o.OrderID, OrderDate = o.OrderDate, OrderStatus = o.OrderStatus,
                    LineCount = o.OrderDetails.Count,
                    Units = o.OrderDetails.Sum(d => (int?)d.Quantity) ?? 0,
                    Total = o.OrderDetails.Sum(d => (decimal?)(d.Quantity * d.UnitAmount)) ?? 0m
                }).ToListAsync()
        };
        return View(vm);
    }

    // GET /Orders/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var order = await _orders.GetOrderAsync(id, User.GetAgentId());
        return order == null ? NotFound() : View(order);
    }

    // GET /Orders/Create?itemId=3
    public async Task<IActionResult> Create(int? itemId)
    {
        var vm = await BuildCreateVmAsync(new CreateOrderViewModel());
        if (itemId.HasValue && vm.Catalog.Any(i => i.ItemID == itemId && i.StockQuantity > 0))
            vm.Lines.Add(new OrderLineInput { ItemId = itemId.Value, Quantity = 1 });
        return View(vm);
    }

    // POST /Orders/Create
    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderViewModel input)
    {
        input.Lines = input.Lines.Where(l => l.ItemId > 0).ToList();
        if (input.Lines.Count == 0)
            ModelState.AddModelError(string.Empty, "Add at least one item to the order.");

        if (ModelState.IsValid)
        {
            try
            {
                var order = await _orders.PlaceOrderAsync(User.GetAgentId(),
                    input.Lines.Select(l => new OrderLineRequest(l.ItemId, l.Quantity)));
                _log.LogInformation("Agent {Agent} placed order {Order}", User.Identity?.Name, order.OrderID);
                TempData["Toast"] = $"Order #{order.OrderID} placed successfully. Stock has been reserved.";
                return RedirectToAction(nameof(Details), new { id = order.OrderID });
            }
            catch (InsufficientStockException ex)
            {
                // Transaction was rolled back - nothing was saved and no stock was deducted.
                ModelState.AddModelError(string.Empty, "The order was blocked because of insufficient stock:");
                foreach (var s in ex.Shortages)
                    ModelState.AddModelError(string.Empty,
                        $"{s.ItemName}: you requested {s.Requested:N0}, only {s.Available:N0} available.");
            }
            catch (OrderValidationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }
        return View(await BuildCreateVmAsync(input));
    }

    // POST /Orders/Cancel/5  - agents may cancel their own order while it is still Pending
    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        var agentId = User.GetAgentId();
        var status = await _db.Orders.Where(o => o.OrderID == id && o.AgentID == agentId)
                                     .Select(o => o.OrderStatus).FirstOrDefaultAsync();
        if (status == null) return NotFound();
        if (status != OrderStatuses.Pending)
        {
            TempData["Error"] = $"Order #{id} is already {status} and can no longer be cancelled online.";
            return RedirectToAction(nameof(Details), new { id });
        }
        try
        {
            await _orders.UpdateStatusAsync(id, OrderStatuses.Cancelled, agentId);
            TempData["Toast"] = $"Order #{id} was cancelled and the stock was released.";
        }
        catch (OrderValidationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // GET /Orders/Invoice/5  - printable invoice (print CSS / Save as PDF from the browser)
    public async Task<IActionResult> Invoice(int id)
    {
        var order = await _orders.GetOrderAsync(id, User.GetAgentId());
        if (order == null) return NotFound();
        var subtotal = OrderService.CalculateTotal(order.OrderDetails);
        var vat = Math.Round(subtotal * InvoiceViewModel.VatRate, 0, MidpointRounding.AwayFromZero);
        return View(new InvoiceViewModel { Order = order, Subtotal = subtotal, Vat = vat, GrandTotal = subtotal + vat });
    }

    private async Task<CreateOrderViewModel> BuildCreateVmAsync(CreateOrderViewModel vm)
    {
        var agentId = User.GetAgentId();
        var agent = await _db.Agents.AsNoTracking().FirstAsync(a => a.AgentID == agentId);
        vm.AgentName = agent.AgentName;
        vm.AgentAddress = agent.Address;
        vm.AgentPhone = agent.Phone;
        vm.Catalog = await _db.Items.AsNoTracking().OrderBy(i => i.ItemName)
            .Select(i => new CatalogItemVm
            {
                ItemID = i.ItemID, ItemName = i.ItemName, Size = i.Size, Price = i.Price, StockQuantity = i.StockQuantity
            }).ToListAsync();
        return vm;
    }
}
