using System.ComponentModel.DataAnnotations;
using B2BWholesale.Core.Entities;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace B2BWholesale.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter your username or email.")]
    [Display(Name = "Username or email")]
    public string UserName { get; set; } = "";

    [Required(ErrorMessage = "Please enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class CatalogItemVm
{
    public int ItemID { get; set; }
    public string ItemName { get; set; } = "";
    public string Size { get; set; } = "";
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
}

public class CatalogViewModel
{
    public List<CatalogItemVm> Items { get; set; } = new();
    public int LowStockThreshold { get; set; }
}

public class OrderLineInput
{
    [Range(1, int.MaxValue, ErrorMessage = "Choose an item.")]
    public int ItemId { get; set; }

    [Range(1, 10_000, ErrorMessage = "Quantity must be between 1 and 10,000.")]
    public int Quantity { get; set; } = 1;
}

public class CreateOrderViewModel
{
    public List<OrderLineInput> Lines { get; set; } = new();

    // display only (never bound from the form, never validated)
    [BindNever, ValidateNever] public string AgentName { get; set; } = "";
    [BindNever, ValidateNever] public string AgentAddress { get; set; } = "";
    [BindNever, ValidateNever] public string AgentPhone { get; set; } = "";
    [BindNever, ValidateNever] public List<CatalogItemVm> Catalog { get; set; } = new();
}

public class OrderSummaryVm
{
    public int OrderID { get; set; }
    public DateTime OrderDate { get; set; }
    public string OrderStatus { get; set; } = "";
    public int LineCount { get; set; }
    public int Units { get; set; }
    public decimal Total { get; set; }
}

public class MyOrdersViewModel
{
    public List<OrderSummaryVm> Orders { get; set; } = new();
    public string? Status { get; set; }
}

public class HomeViewModel
{
    public string AgentName { get; set; } = "";
    public int TotalOrders { get; set; }
    public int OpenOrders { get; set; }
    public decimal TotalSpent { get; set; }
    public int CatalogCount { get; set; }
    public List<OrderSummaryVm> RecentOrders { get; set; } = new();
}

public class InvoiceViewModel
{
    public Order Order { get; set; } = null!;
    public decimal Subtotal { get; set; }
    public decimal Vat { get; set; }
    public decimal GrandTotal { get; set; }
    public const decimal VatRate = 0.08m;   // Vietnam VAT 8% (reduced rate)
}

public static class StatusUi
{
    public static string Badge(string status) => status switch
    {
        OrderStatuses.Pending => "text-bg-warning",
        OrderStatuses.Confirmed => "text-bg-primary",
        OrderStatuses.Shipped => "text-bg-info",
        OrderStatuses.Delivered => "text-bg-success",
        _ => "text-bg-secondary"
    };

    public static string Icon(string status) => status switch
    {
        OrderStatuses.Pending => "bi-hourglass-split",
        OrderStatuses.Confirmed => "bi-check2-circle",
        OrderStatuses.Shipped => "bi-truck",
        OrderStatuses.Delivered => "bi-box-seam",
        _ => "bi-x-circle"
    };

    public static string Money(decimal v) => v.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + " ₫";
}
