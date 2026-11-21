using System.Security.Claims;
using FluentValidation;
using LifeMineralWater.Common;
using LifeMineralWater.Data;
using LifeMineralWater.Models.ViewModels;
using LifeMineralWater.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeMineralWater.Controllers;

public sealed class AccountController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IValidator<LoginViewModel> _validator;

    public AccountController(ApplicationDbContext db, IValidator<LoginViewModel> validator)
    {
        _db = db;
        _validator = validator;
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToLanding();
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(model, ct);
        foreach (var e in validation.Errors) ModelState.AddModelError(e.PropertyName, e.ErrorMessage);
        if (!validation.IsValid) return View(model);

        var email = model.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email.ToLower() == email, ct);

        // Same generic error for unknown email / bad password / disabled account.
        if (user is null || !PasswordHash.Verify(model.Password, user.PasswordHash) || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Invalid email, password, or inactive account.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role),
            new(ClaimTypes.Email, user.Email)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = model.RememberMe, AllowRefresh = true });

        var tracked = await _db.Users.FindAsync(new object[] { user.Id }, ct);
        if (tracked is not null) tracked.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);
        return RedirectToLanding(user.Role);
    }

    [HttpGet]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    [HttpGet, Authorize(Roles = AppRoles.Owner)]
    public IActionResult Register() => RedirectToAction("Users", "Admin");

    private IActionResult RedirectToLanding(string? role = null)
    {
        role ??= User.FindFirstValue(ClaimTypes.Role) ?? AppRoles.Customer;
        return RedirectToAction(AppRoles.LandingAction(role), AppRoles.LandingController(role));
    }
}
