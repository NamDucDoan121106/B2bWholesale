using System.ComponentModel.DataAnnotations;

namespace B2BWholesale.Core.Entities;

public class Item
{
    public int ItemID { get; set; }

    [Required, StringLength(150)]
    public string ItemName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Size { get; set; } = string.Empty;

    [Range(0, 999_999_999)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }

    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}

public class Agent
{
    public int AgentID { get; set; }

    [Required, StringLength(150)]
    public string AgentName { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string Address { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<User> Users { get; set; } = new List<User>();
}

public class Order
{
    public int OrderID { get; set; }
    public DateTime OrderDate { get; set; }
    public int AgentID { get; set; }

    [Required, StringLength(20)]
    public string OrderStatus { get; set; } = OrderStatuses.Pending;

    public Agent? Agent { get; set; }
    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    /// <summary>Sum of Quantity x UnitAmount for all lines (needs OrderDetails loaded).</summary>
    public decimal TotalAmount => OrderDetails.Sum(d => d.LineTotal);
}

public class OrderDetail
{
    public int ID { get; set; }
    public int OrderID { get; set; }
    public int ItemID { get; set; }
    public int Quantity { get; set; }

    /// <summary>Unit price captured at the moment the order was placed.</summary>
    public decimal UnitAmount { get; set; }

    public Order? Order { get; set; }
    public Item? Item { get; set; }

    public decimal LineTotal => Quantity * UnitAmount;
}

public class User
{
    public int UserID { get; set; }

    [Required, StringLength(50)]
    public string UserName { get; set; } = string.Empty;

    [Required, StringLength(150), EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>PBKDF2 hash produced by <see cref="Security.PasswordHasher"/>.</summary>
    [Required, StringLength(256)]
    public string Password { get; set; } = string.Empty;

    public bool IsLocked { get; set; }

    [Required, StringLength(20)]
    public string Role { get; set; } = Roles.Agent;

    public int FailedLoginAttempts { get; set; }

    public int? AgentID { get; set; }
    public Agent? Agent { get; set; }
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Agent = "Agent";
}

public static class OrderStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Shipped = "Shipped";
    public const string Delivered = "Delivered";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = { Pending, Confirmed, Shipped, Delivered, Cancelled };

    /// <summary>Allowed workflow: Pending → Confirmed → Shipped → Delivered; Pending/Confirmed → Cancelled.</summary>
    public static IReadOnlyList<string> NextStatuses(string current) => current switch
    {
        Pending => new[] { Confirmed, Cancelled },
        Confirmed => new[] { Shipped, Cancelled },
        Shipped => new[] { Delivered },
        _ => Array.Empty<string>()
    };

    public static bool CanMove(string from, string to) => NextStatuses(from).Contains(to);
}
