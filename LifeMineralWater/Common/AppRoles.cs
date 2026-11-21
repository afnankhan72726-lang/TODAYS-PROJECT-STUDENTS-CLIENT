namespace LifeMineralWater.Common;

/// <summary>
/// Single source of truth for authorization role names. Never use raw strings elsewhere.
/// </summary>
public static class AppRoles
{
    public const string Owner = "Owner";
    public const string Salesman = "Salesman";
    public const string Customer = "Customer";

    public static readonly string[] All = { Owner, Salesman, Customer };

    public static string LandingAction(string role) => role switch
    {
        Owner => "Dashboard",
        Salesman => "Index",
        Customer => "Index",
        _ => "Login"
    };

    public static string LandingController(string role) => role switch
    {
        Owner => "Admin",
        Salesman => "Salesman",
        Customer => "Customer",
        _ => "Account"
    };
}
