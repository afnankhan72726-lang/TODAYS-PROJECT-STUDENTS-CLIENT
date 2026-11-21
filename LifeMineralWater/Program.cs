using System.Security.Claims;
using FluentValidation;
using LifeMineralWater.Common;
using LifeMineralWater.Data;
using LifeMineralWater.Models.ViewModels;
using LifeMineralWater.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

// Explicit transactions with UPDLOCK/HOLDLOCK are used for all balance-changing operations.
// EF Core's retrying execution strategy is incompatible with user-initiated transactions,
// so the default (non-retrying) strategy is used; operations remain atomic and concurrency-safe.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditContext, AuditContext>();

builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IRateService, RateService>();
builder.Services.AddScoped<ILedgerService, LedgerService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();

builder.Services.AddScoped<IValidator<DeliveryInput>, DeliveryInputValidator>();
builder.Services.AddScoped<IValidator<PaymentInput>, PaymentInputValidator>();
builder.Services.AddScoped<IValidator<ExpenseInput>, ExpenseInputValidator>();
builder.Services.AddScoped<IValidator<AdvanceInput>, AdvanceInputValidator>();
builder.Services.AddScoped<IValidator<AdjustmentInput>, AdjustmentInputValidator>();
builder.Services.AddScoped<IValidator<ChangeRequestInput>, ChangeRequestInputValidator>();
builder.Services.AddScoped<IValidator<LoginViewModel>, LoginViewModelValidator>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.LogoutPath = "/Account/Logout";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(app.Services);
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

// Bind the signed-in actor to the audit context for every request.
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var audit = context.RequestServices.GetRequiredService<IAuditContext>();
        var id = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        audit.Set(int.TryParse(id, out var parsed) ? parsed : null, context.User.Identity.Name);
    }
    await next();
});

app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

await app.RunAsync();
