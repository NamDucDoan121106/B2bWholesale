using B2BWholesale.Core.Data;
using B2BWholesale.Core.Entities;
using B2BWholesale.Core.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Connection string comes from appsettings.json (never hard-coded)
var connectionString = builder.Configuration.GetConnectionString("WholesaleDb")
    ?? throw new InvalidOperationException("Connection string 'WholesaleDb' is missing in appsettings.json.");

builder.Services.AddDbContext<WholesaleDbContext>(o =>
    o.UseSqlServer(connectionString, sql => sql.MigrationsAssembly("B2BWholesale.Core")));

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<OrderService>();

// Task 3.1 - cookie login; the whole portal requires the Agent role by default
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.LogoutPath = "/Account/Logout";
        o.AccessDeniedPath = "/Account/AccessDenied";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.Cookie.HttpOnly = true;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddControllersWithViews(o =>
{
    var agentOnly = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireRole(Roles.Agent).Build();
    o.Filters.Add(new AuthorizeFilter(agentOnly));
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());   // CSRF protection on every POST
    o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});

var app = builder.Build();

// Code First: if migrations were added (dotnet ef migrations add InitialCreate), apply them on startup.
// When the database was created with Database/B2BWholesaleDB.sql instead, there are no migrations and this is skipped.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WholesaleDbContext>();
    if (db.Database.GetMigrations().Any())
    {
        app.Logger.LogInformation("Applying EF Core migrations...");
        db.Database.Migrate();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
