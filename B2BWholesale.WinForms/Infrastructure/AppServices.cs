using System.Configuration;
using B2BWholesale.Core.Data;
using B2BWholesale.Core.Entities;

namespace B2BWholesale.WinForms.Infrastructure;

/// <summary>Reads settings from App.config (never hard-coded) and creates DbContexts.</summary>
public static class AppServices
{
    private static string? _connectionString;

    public static string ConnectionString =>
        _connectionString ??= ConfigurationManager.ConnectionStrings["WholesaleDb"]?.ConnectionString
            ?? throw new ConfigurationErrorsException("Connection string 'WholesaleDb' is missing from App.config.");

    public static int LowStockThreshold =>
        int.TryParse(ConfigurationManager.AppSettings["LowStockThreshold"], out var v) && v > 0 ? v : 20;

    /// <summary>Short-lived context: create one per operation and dispose it.</summary>
    public static WholesaleDbContext NewDb() => WholesaleDbContext.Create(ConnectionString);
}

/// <summary>The admin who is currently logged in.</summary>
public static class Session
{
    public static User? CurrentUser { get; set; }
}
