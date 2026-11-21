using System.Diagnostics;
using System.Security.Claims;
using LifeMineralWater.Common;
using LifeMineralWater.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeMineralWater.Controllers;

public sealed class HomeController : Controller
{
    [AllowAnonymous]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? AppRoles.Customer;
            return RedirectToAction(AppRoles.LandingAction(role), AppRoles.LandingController(role));
        }
        return RedirectToAction("Login", "Account");
    }

    [AllowAnonymous]
    public IActionResult Privacy() => View();

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
        => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
