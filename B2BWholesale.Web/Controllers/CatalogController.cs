using B2BWholesale.Core.Data;
using B2BWholesale.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace B2BWholesale.Web.Controllers;

/// <summary>Task 3.2 - Agents can view the catalog of Items.</summary>
public class CatalogController : Controller
{
    private readonly WholesaleDbContext _db;
    private readonly IConfiguration _config;

    public CatalogController(WholesaleDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<IActionResult> Index()
    {
        var vm = new CatalogViewModel
        {
            LowStockThreshold = _config.GetValue("Portal:LowStockThreshold", 20),
            Items = await _db.Items.AsNoTracking().OrderBy(i => i.ItemName)
                .Select(i => new CatalogItemVm
                {
                    ItemID = i.ItemID, ItemName = i.ItemName, Size = i.Size, Price = i.Price, StockQuantity = i.StockQuantity
                }).ToListAsync()
        };
        return View(vm);
    }
}
