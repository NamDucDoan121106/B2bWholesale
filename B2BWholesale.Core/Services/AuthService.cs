using B2BWholesale.Core.Data;
using B2BWholesale.Core.Entities;
using B2BWholesale.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace B2BWholesale.Core.Services;

public enum LoginStatus
{
    Success,
    InvalidCredentials,
    Locked,
    WrongRole
}

public sealed record LoginResult(LoginStatus Status, User? User = null, int RemainingAttempts = 0)
{
    public bool Succeeded => Status == LoginStatus.Success;
}

/// <summary>
/// Shared login logic for WinForms (Admin only) and the web portal (Agent only).
/// - Passwords are checked against a PBKDF2 hash.
/// - After <see cref="MaxFailedAttempts"/> wrong passwords the account is locked (Users.lock = 1).
/// - A correct password for the wrong role is refused without revealing anything else.
/// </summary>
public class AuthService
{
    public const int MaxFailedAttempts = 5;
    private readonly WholesaleDbContext _db;

    public AuthService(WholesaleDbContext db) => _db = db;

    public async Task<LoginResult> LoginAsync(string userName, string password, string requiredRole)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
            return new LoginResult(LoginStatus.InvalidCredentials);

        var name = userName.Trim();
        var user = await _db.Users.Include(u => u.Agent)
                                  .FirstOrDefaultAsync(u => u.UserName == name || u.Email == name);
        if (user is null)
            return new LoginResult(LoginStatus.InvalidCredentials, RemainingAttempts: MaxFailedAttempts);

        if (user.IsLocked)
            return new LoginResult(LoginStatus.Locked, user);

        if (!PasswordHasher.Verify(password, user.Password))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
                user.IsLocked = true;
            await _db.SaveChangesAsync();
            return user.IsLocked
                ? new LoginResult(LoginStatus.Locked, user)
                : new LoginResult(LoginStatus.InvalidCredentials, user, MaxFailedAttempts - user.FailedLoginAttempts);
        }

        if (!string.Equals(user.Role, requiredRole, StringComparison.OrdinalIgnoreCase))
            return new LoginResult(LoginStatus.WrongRole, user);

        if (user.FailedLoginAttempts != 0)
        {
            user.FailedLoginAttempts = 0;
            await _db.SaveChangesAsync();
        }
        return new LoginResult(LoginStatus.Success, user);
    }

    public static string Describe(LoginResult r, string appName) => r.Status switch
    {
        LoginStatus.Success => "Login successful.",
        LoginStatus.Locked => "This account is locked. Please contact an administrator.",
        LoginStatus.WrongRole => $"Your account is not allowed to use the {appName}.",
        _ when r.User is not null && r.RemainingAttempts > 0 =>
            $"Invalid username or password. {r.RemainingAttempts} attempt(s) left before the account is locked.",
        _ => "Invalid username or password."
    };
}
