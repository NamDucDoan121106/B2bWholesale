using System.Security.Claims;
using B2BWholesale.Core.Entities;
using B2BWholesale.Core.Services;
using B2BWholesale.Web.Infrastructure;
using B2BWholesale.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace B2BWholesale.Web.Controllers;

/// <summary>Task 3.1 - Web login. Only users with the Agent role can sign in to the portal.</summary>
[AllowAnonymous]
public class AccountController : Controller
{
    private readonly AuthService _auth;
    private readonly ILogger<AccountController> _log;

    public AccountController(AuthService auth, ILogger<AccountController> log)
    {
        _auth = auth;
        _log = log;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true && User.IsInRole(Roles.Agent))
            return RedirectToAction("Index", "Home");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _auth.LoginAsync(model.UserName, model.Password, Roles.Agent);
        if (!result.Succeeded)
        {
            _log.LogWarning("Portal login refused for {User}: {Status}", model.UserName, result.Status);
            ModelState.AddModelError(string.Empty, AuthService.Describe(result, "Agent Portal (Admins use the desktop app)"));
            model.Password = "";
            return View(model);
        }

        var user = result.User!;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserID.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new(PortalClaims.AgentId, user.AgentID?.ToString() ?? ""),
            new(PortalClaims.AgentName, user.Agent?.AgentName ?? user.UserName)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = model.RememberMe });

        _log.LogInformation("Agent {User} signed in", user.UserName);
        // Only redirect to local URLs (prevents open-redirect attacks)
        return Url.IsLocalUrl(model.ReturnUrl) ? LocalRedirect(model.ReturnUrl!) : RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Toast"] = "You have been signed out.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();
}
