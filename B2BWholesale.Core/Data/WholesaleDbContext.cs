using B2BWholesale.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace B2BWholesale.Core.Data;

/// <summary>
/// Code First model. Table/column names match Database/B2BWholesaleDB.sql exactly,
/// so the app works with either the SQL script or EF migrations.
/// </summary>
public class WholesaleDbContext : DbContext
{
    public WholesaleDbContext(DbContextOptions<WholesaleDbContext> options) : base(options) { }

    public DbSet<Item> Items => Set<Item>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();
    public DbSet<User> Users => Set<User>();

    /// <summary>Builds a context from a connection string (used by the WinForms app).</summary>
    public static WholesaleDbContext Create(string connectionString) =>
        new(new DbContextOptionsBuilder<WholesaleDbContext>().UseSqlServer(connectionString).Options);

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Item>(e =>
        {
            e.ToTable("Item", t =>
            {
                t.HasCheckConstraint("CK_Item_Price", "[Price] >= 0");
                t.HasCheckConstraint("CK_Item_Stock", "[StockQuantity] >= 0");
            });
            e.HasKey(x => x.ItemID);
            e.Property(x => x.ItemName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Size).HasMaxLength(100).IsRequired();
            e.Property(x => x.Price).HasColumnType("decimal(18,2)");
            e.HasIndex(x => new { x.ItemName, x.Size }).IsUnique().HasDatabaseName("IX_Item_Name_Size");
        });

        mb.Entity<Agent>(e =>
        {
            e.ToTable("Agent");
            e.HasKey(x => x.AgentID);
            e.Property(x => x.AgentName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Address).HasMaxLength(300).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
        });

        mb.Entity<Order>(e =>
        {
            e.ToTable("Order", t => t.HasCheckConstraint("CK_Order_Status",
                "[OrderStatus] IN (N'Pending', N'Confirmed', N'Shipped', N'Delivered', N'Cancelled')"));
            e.HasKey(x => x.OrderID);
            e.Property(x => x.OrderDate).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            e.Property(x => x.OrderStatus).HasMaxLength(20).IsRequired().HasDefaultValue(OrderStatuses.Pending);
            e.Ignore(x => x.TotalAmount);
            e.HasOne(x => x.Agent).WithMany(a => a.Orders).HasForeignKey(x => x.AgentID)
             .OnDelete(DeleteBehavior.Restrict)          // agent with history cannot be deleted
             .HasConstraintName("FK_Order_Agent");
        });

        mb.Entity<OrderDetail>(e =>
        {
            e.ToTable("OrderDetail", t =>
            {
                t.HasCheckConstraint("CK_OrderDetail_Qty", "[Quantity] > 0");
                t.HasCheckConstraint("CK_OrderDetail_Amount", "[UnitAmount] >= 0");
            });
            e.HasKey(x => x.ID);
            e.Property(x => x.UnitAmount).HasColumnType("decimal(18,2)");
            e.Ignore(x => x.LineTotal);
            e.HasOne(x => x.Order).WithMany(o => o.OrderDetails).HasForeignKey(x => x.OrderID)
             .OnDelete(DeleteBehavior.Cascade)           // deleting an order removes its lines
             .HasConstraintName("FK_OrderDetail_Order");
            e.HasOne(x => x.Item).WithMany(i => i.OrderDetails).HasForeignKey(x => x.ItemID)
             .OnDelete(DeleteBehavior.Restrict)          // a sold item cannot be deleted
             .HasConstraintName("FK_OrderDetail_Item");
        });

        mb.Entity<User>(e =>
        {
            e.ToTable("Users", t =>
            {
                t.HasCheckConstraint("CK_Users_Role", "[Role] IN (N'Admin', N'Agent')");
                t.HasCheckConstraint("CK_Users_AgentLink",
                    "([Role] = N'Agent' AND [AgentID] IS NOT NULL) OR ([Role] = N'Admin' AND [AgentID] IS NULL)");
            });
            e.HasKey(x => x.UserID);
            e.Property(x => x.UserName).HasMaxLength(50).IsRequired();
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(150).IsRequired();
            e.Property(x => x.Password).HasColumnName("password").HasMaxLength(256).IsRequired();
            e.Property(x => x.IsLocked).HasColumnName("lock");
            e.Property(x => x.Role).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.UserName).IsUnique().HasDatabaseName("UQ_Users_UserName");
            e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("UQ_Users_Email");
            e.HasOne(x => x.Agent).WithMany(a => a.Users).HasForeignKey(x => x.AgentID)
             .OnDelete(DeleteBehavior.Cascade)           // deleting an agent removes its login
             .HasConstraintName("FK_Users_Agent");
        });

        SeedData.Apply(mb);
    }
}
