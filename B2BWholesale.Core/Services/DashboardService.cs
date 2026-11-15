using B2BWholesale.Core.Data;
using B2BWholesale.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace B2BWholesale.Core.Services;

public class BestSellerRow
{
    public int Rank { get; set; }
    public int ItemID { get; set; }
    public string ItemName { get; set; } = "";
    public string Size { get; set; } = "";
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public int StockLeft { get; set; }
}

public class LowStockRow
{
    public int ItemID { get; set; }
    public string ItemName { get; set; } = "";
    public string Size { get; set; } = "";
    public int StockQuantity { get; set; }
    public string Level { get; set; } = "";
}

public class AgentRevenueRow
{
    public int AgentID { get; set; }
    public string AgentName { get; set; } = "";
    public int OrderCount { get; set; }
    public int UnitsBought { get; set; }
    public decimal Revenue { get; set; }
}

public class DashboardSummary
{
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public int PendingOrders { get; set; }
    public int LowStockItems { get; set; }
}

/// <summary>
/// Reporting queries for the back-office dashboard. All aggregation runs in SQL Server.
/// Revenue ignores Cancelled orders.
/// </summary>
public class DashboardService
{
    private readonly WholesaleDbContext _db;
    public DashboardService(WholesaleDbContext db) => _db = db;

    private IQueryable<OrderDetail> ValidLines(DateTime? from, DateTime? to)
    {
        var q = _db.OrderDetails.AsNoTracking().Where(d => d.Order!.OrderStatus != OrderStatuses.Cancelled);
        if (from.HasValue) q = q.Where(d => d.Order!.OrderDate >= from.Value.Date);
        if (to.HasValue) q = q.Where(d => d.Order!.OrderDate < to.Value.Date.AddDays(1));
        return q;
    }

    public async Task<List<BestSellerRow>> GetBestSellersAsync(int top, DateTime? from, DateTime? to)
    {
        var sales = from d in ValidLines(@from, to)
                    group d by d.ItemID into g
                    select new
                    {
                        ItemID = g.Key,
                        Qty = g.Sum(x => x.Quantity),
                        Revenue = g.Sum(x => x.Quantity * x.UnitAmount)
                    };

        var rows = await (from s in sales
                          join i in _db.Items on s.ItemID equals i.ItemID
                          orderby s.Qty descending, s.Revenue descending
                          select new BestSellerRow
                          {
                              ItemID = i.ItemID,
                              ItemName = i.ItemName,
                              Size = i.Size,
                              QuantitySold = s.Qty,
                              Revenue = s.Revenue,
                              StockLeft = i.StockQuantity
                          }).Take(top).ToListAsync();

        for (var k = 0; k < rows.Count; k++)
            rows[k].Rank = k + 1;

        return rows;
    }

    public async Task<List<LowStockRow>> GetLowStockAsync(int threshold)
    {
        var rows = await _db.Items.AsNoTracking()
            .Where(i => i.StockQuantity <= threshold)
            .OrderBy(i => i.StockQuantity)
            .Select(i => new LowStockRow
            {
                ItemID = i.ItemID,
                ItemName = i.ItemName,
                Size = i.Size,
                StockQuantity = i.StockQuantity
            }).ToListAsync();

        foreach (var r in rows)
            r.Level = r.StockQuantity == 0 ? "OUT OF STOCK"
                    : r.StockQuantity <= threshold / 2 ? "CRITICAL"
                    : "LOW";
        return rows;
    }

    public async Task<List<AgentRevenueRow>> GetRevenueByAgentAsync(int? agentId, DateTime? from, DateTime? to)
    {
        var lines = ValidLines(from, to);
        if (agentId.HasValue) lines = lines.Where(d => d.Order!.AgentID == agentId.Value);

        var perAgent = from d in lines
                       join o in _db.Orders on d.OrderID equals o.OrderID
                       group new { d.Quantity, d.UnitAmount, d.OrderID } by o.AgentID into g
                       select new
                       {
                           AgentID = g.Key,
                           Orders = g.Select(x => x.OrderID).Distinct().Count(),
                           Units = g.Sum(x => x.Quantity),
                           Revenue = g.Sum(x => x.Quantity * x.UnitAmount)
                       };

        return await (from s in perAgent
                      join a in _db.Agents on s.AgentID equals a.AgentID
                      orderby s.Revenue descending
                      select new AgentRevenueRow
                      {
                          AgentID = a.AgentID,
                          AgentName = a.AgentName,
                          OrderCount = s.Orders,
                          UnitsBought = s.Units,
                          Revenue = s.Revenue
                      }).ToListAsync();
    }

    public async Task<DashboardSummary> GetSummaryAsync(int lowStockThreshold, DateTime? from, DateTime? to)
    {
        var lines = ValidLines(from, to);
        var orders = _db.Orders.AsNoTracking().Where(o => o.OrderStatus != OrderStatuses.Cancelled);
        if (from.HasValue) orders = orders.Where(o => o.OrderDate >= from.Value.Date);
        if (to.HasValue) orders = orders.Where(o => o.OrderDate < to.Value.Date.AddDays(1));

        return new DashboardSummary
        {
            TotalRevenue = await lines.SumAsync(d => (decimal?)(d.Quantity * d.UnitAmount)) ?? 0m,
            TotalOrders = await orders.CountAsync(),
            PendingOrders = await _db.Orders.CountAsync(o => o.OrderStatus == OrderStatuses.Pending),
            LowStockItems = await _db.Items.CountAsync(i => i.StockQuantity <= lowStockThreshold)
        };
    }
}
