using B2BWholesale.Core.Data;
using B2BWholesale.Core.Entities;
using B2BWholesale.Core.Security;
using B2BWholesale.Core.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace B2BWholesale.Tests;

public class AuthBusinessTests
{
    private static WholesaleDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<WholesaleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new WholesaleDbContext(options);
    }

    private static async Task AddUserAsync(WholesaleDbContext db, string role = Roles.Admin)
    {
        db.Users.Add(new User
        {
            UserID = 1000, UserName = "tester", Email = "tester@example.com",
            Password = PasswordHasher.Hash("Correct#123"), Role = role,
            AgentID = role == Roles.Agent ? 1 : null
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public void PasswordHash_VerifiesCorrectPasswordAndRejectsIncorrectOne()
    {
        var hash = PasswordHasher.Hash("StrongPass#42");
        Assert.True(PasswordHasher.Verify("StrongPass#42", hash));
        Assert.False(PasswordHasher.Verify("WrongPass", hash));
    }

    [Fact]
    public void PasswordHash_MalformedStoredHash_IsRejected()
    {
        Assert.False(PasswordHasher.Verify("password", "not-a-hash"));
    }

    [Fact]
    public async Task Login_CorrectAdminCredentials_Succeeds()
    {
        await using var db = NewContext();
        await AddUserAsync(db);
        var result = await new AuthService(db).LoginAsync("tester", "Correct#123", Roles.Admin);
        Assert.Equal(LoginStatus.Success, result.Status);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Login_CorrectPasswordWrongRole_IsDenied()
    {
        await using var db = NewContext();
        await AddUserAsync(db, Roles.Agent);
        var result = await new AuthService(db).LoginAsync("tester", "Correct#123", Roles.Admin);
        Assert.Equal(LoginStatus.WrongRole, result.Status);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Login_FiveWrongPasswords_LocksUser()
    {
        await using var db = NewContext();
        await AddUserAsync(db);
        var service = new AuthService(db);
        for (int attempt = 1; attempt < AuthService.MaxFailedAttempts; attempt++)
        {
            var result = await service.LoginAsync("tester", "incorrect", Roles.Admin);
            Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
            Assert.Equal(AuthService.MaxFailedAttempts - attempt, result.RemainingAttempts);
        }
        var locked = await service.LoginAsync("tester", "incorrect", Roles.Admin);
        Assert.Equal(LoginStatus.Locked, locked.Status);
        Assert.True((await db.Users.SingleAsync(u => u.UserName == "tester")).IsLocked);
        Assert.Equal(LoginStatus.Locked, (await service.LoginAsync("tester", "Correct#123", Roles.Admin)).Status);
    }

    [Fact]
    public async Task Login_AfterWrongPassword_CorrectPasswordResetsAttempts()
    {
        await using var db = NewContext();
        await AddUserAsync(db);
        var service = new AuthService(db);
        await service.LoginAsync("tester", "incorrect", Roles.Admin);
        Assert.Equal(LoginStatus.Success, (await service.LoginAsync("tester", "Correct#123", Roles.Admin)).Status);
        Assert.Equal(0, (await db.Users.SingleAsync(u => u.UserName == "tester")).FailedLoginAttempts);
    }

    [Fact]
    public async Task Login_UnknownUser_ReturnsInvalidCredentials()
    {
        await using var db = NewContext();
        var result = await new AuthService(db).LoginAsync("nobody", "guess", Roles.Agent);
        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
    }
}
