using System.Diagnostics;
using B2BWholesale.Core.Data;
using B2BWholesale.Core.Entities;
using B2BWholesale.Web.Infrastructure;
using B2BWholesale.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace B2BWholesale.Web.Controllers;

public class HomeController : Controller
{
    private readonly WholesaleDbContext _db;
    public HomeController(WholesaleDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var agentId = User.GetAgentId();
        var mine = _db.Orders.AsNoTracking().Where(o => o.AgentID == agentId);

        var vm = new HomeViewModel
        {
            AgentName = User.GetAgentName(),
            TotalOrders = await mine.CountAsync(),
            OpenOrders = await mine.CountAsync(o => o.OrderStatus == OrderStatuses.Pending
                                                 || o.OrderStatus == OrderStatuses.Confirmed
                                                 || o.OrderStatus == OrderStatuses.Shipped),
            TotalSpent = await _db.OrderDetails.Where(d => d.Order!.AgentID == agentId && d.Order.OrderStatus != OrderStatuses.Cancelled)
                                               .SumAsync(d => (decimal?)(d.Quantity * d.UnitAmount)) ?? 0m,
            CatalogCount = await _db.Items.CountAsync(i => i.StockQuantity > 0),
            RecentOrders = await mine.OrderByDescending(o => o.OrderDate).Take(5)
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

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(model: Activity.Current?.Id ?? HttpContext.TraceIdentifier);
}
