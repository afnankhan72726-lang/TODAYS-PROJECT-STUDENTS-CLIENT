using LifeMineralWater.Common;
using LifeMineralWater.Models;
using Microsoft.EntityFrameworkCore;

namespace LifeMineralWater.Data;

/// <summary>
/// Idempotent demo/sample data seeder. It fills missing rows only, so real data is never
/// overwritten and a repeated application start is safe.
/// </summary>
public static class DbSeeder
{
    public const string OwnerEmail = "owner@lifemineral.local";
    public const string SalesmanEmail = "salesman@lifemineral.local";
    public const string CustomerEmail = "customer@lifemineral.local";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;

        // 1. Portal identities. Legacy rows created by the old system are repaired in place.
        var owner = await EnsureUserAsync(db, OwnerEmail, "Life Mineral Owner", "Owner@123", AppRoles.Owner, now);
        var salesmanUser = await EnsureUserAsync(db, SalesmanEmail, "Field Salesman", "Sales@123", AppRoles.Salesman, now);
        var customerUser = await EnsureUserAsync(db, CustomerEmail, "Demo Customer", "Cust@123", AppRoles.Customer, now);
        await db.SaveChangesAsync();

        // 2. Catalog master data.
        var product = await EnsureProductAsync(db, "LM-1L", "Life Mineral 1L", "One litre mineral water");
        var small = await EnsureProductAsync(db, "LM-500", "Life Mineral 500ml", "Half litre mineral water");
        var bottle = await EnsureBottleAsync(db, "1L Bottle", 1000, 500m);
        var smallBottle = await EnsureBottleAsync(db, "500ml Bottle", 500, 300m);
        await db.SaveChangesAsync();

        // 3. Domain roles — strictly separate from portal identities.
        var salesman = await db.Salesmen.FirstOrDefaultAsync(x => x.UserId == salesmanUser.Id);
        if (salesman is null)
        {
            salesman = new Salesman
            {
                Code = "SM-001", UserId = salesmanUser.Id, OpeningCash = 5000m, CurrentCash = 5000m,
                IsActive = true, JoinedOn = now.Date
            };
            db.Salesmen.Add(salesman);
            await db.SaveChangesAsync();
        }

        var customer = await db.Customers.FirstOrDefaultAsync(x => x.UserId == customerUser.Id);
        if (customer is null)
        {
            customer = new Customer
            {
                Code = "CUS-001", UserId = customerUser.Id, AssignedSalesmanId = salesman.Id,
                Status = AccountStatus.Active, DepositAmount = 500m, DepositBottles = 10,
                BottleBalance = 0, MoneyBalance = 0, CreatedAt = now, CreatedBy = "Seeder"
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
        }

        // 4. Dynamic rate history.
        await EnsureRateAsync(db, null, product.Id, bottle.Id, 100m, now);
        await EnsureRateAsync(db, null, small.Id, smallBottle.Id, 60m, now);
        await db.SaveChangesAsync();

        // 5. One opening delivery so every demo flow has data (10 bottles / 1000 money).
        if (!await db.LedgerEntries.IgnoreQueryFilters().AnyAsync(x => x.CustomerId == customer.Id))
        {
            var deliveryDate = new DateTime(now.Year, now.Month, Math.Min(2, now.Day), 9, 0, 0);
            db.LedgerEntries.Add(new LedgerEntry
            {
                CustomerId = customer.Id, SalesmanId = salesman.Id, EntryType = LedgerEntryType.Delivery,
                EntryDate = deliveryDate, ProductId = product.Id, BottleTypeId = bottle.Id,
                FilledDelivered = 10, EmptyReceived = 0, Rate = 100m, SaleAmount = 1000m,
                AmountReceived = 0, AmountAdjusted = 0, BottleBalanceAfter = 10, MoneyBalanceAfter = 1000m,
                Description = "Opening delivery", CreatedAt = now, CreatedBy = "Seeder"
            });
            customer.BottleBalance = 10;
            customer.MoneyBalance = 1000m;
        }

        if (!await db.CashBookEntries.AnyAsync())
        {
            db.CashBookEntries.Add(new CashBookEntry
            {
                SalesmanId = salesman.Id, Direction = CashDirection.In, Category = CashCategory.Opening,
                Amount = 5000m, TransactionDate = now.Date, Description = "Opening cash",
                CreatedBy = "Seeder", CreatedAt = now
            });
        }

        if (!await db.BankAccounts.AnyAsync())
        {
            db.BankAccounts.Add(new BankAccount
            {
                BankName = "Life Bank", AccountTitle = "Life Mineral Water", AccountNumber = "0001-2345-6789",
                Iban = "PK00LIFE000123456789", Branch = "Main Branch", IsDefault = true, IsActive = true
            });
        }

        // 6. All previously hardcoded values live in SQL settings.
        var settings = new (string Key, string Value, string Group, string Display, SettingType Type, string? Description)[]
        {
            ("Company.Name", "Life Mineral Water", "Company", "Company Name", SettingType.Text, "Shown on invoices and portal"),
            ("Company.Address", "Head Office, Main Road", "Company", "Company Address", SettingType.Text, null),
            ("Company.Phone", "+92 300 0000000", "Company", "Company Phone", SettingType.Text, null),
            ("Company.Email", "info@lifemineral.local", "Company", "Company Email", SettingType.Text, null),
            ("Company.TaxNo", "NTN-0000000", "Company", "Tax / NTN Number", SettingType.Text, null),
            ("Invoice.DueDay", "5", "Invoice", "Invoice Due Day", SettingType.Number, "Day of the following month"),
            ("Invoice.Message", "Dear Customer, your monthly invoice is ready. Please clear your outstanding amount by the 5th. Thank you for choosing Life Mineral Water.", "Invoice", "Invoice Message", SettingType.Text, null),
            ("WhatsApp.Template", "Dear {CustomerName}, your monthly invoice for {Period} is ready. Outstanding: Rs. {Amount}. Please clear your outstanding amount by the {DueDate}. Thank you for choosing Life Mineral Water.", "Invoice", "WhatsApp Message Template", SettingType.Text, "Placeholders: {CustomerName}, {Period}, {Amount}, {DueDate}"),
            ("Deposit.PaidTemplate", "Deposit Paid: Rs. {Amount} against {Bottles} Bottles", "Deposit", "Deposit Paid Message", SettingType.Text, null),
            ("Deposit.NoneMessage", "No Deposit Paid — Bottles remain the property of the company.", "Deposit", "No Deposit Message", SettingType.Text, null),
            ("Rates.Default", "100", "Rates", "Default Bottle Rate", SettingType.Number, "Fallback when no rate rule matches"),
            ("Currency.Symbol", "Rs.", "General", "Currency Symbol", SettingType.Text, null)
        };
        foreach (var s in settings)
        {
            if (await db.SystemSettings.AnyAsync(x => x.Key == s.Key)) continue;
            db.SystemSettings.Add(new SystemSetting
            {
                Key = s.Key, Value = s.Value, Group = s.Group, DisplayName = s.Display, Type = s.Type,
                Description = s.Description, UpdatedAt = now, UpdatedBy = "Seeder"
            });
        }

        if (!await db.Announcements.AnyAsync())
        {
            db.Announcements.Add(new Announcement
            {
                Title = "Monthly offer: 5% off on 500ml cases",
                Message = "Order 20 or more 500ml bottles this month and get 5% off. Terms apply.",
                Type = AnnouncementType.SpecialOffer, StartOn = now.AddDays(-10), EndOn = now.AddDays(20),
                IsActive = true, CreatedAt = now, CreatedBy = "Seeder"
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task<AppUser> EnsureUserAsync(ApplicationDbContext db, string email,
        string fullName, string password, string role, DateTime now)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Email.ToLower() == email);
        if (user is not null)
        {
            // Repair only blank fields left behind by the legacy system.
            if (string.IsNullOrWhiteSpace(user.FullName)) user.FullName = fullName;
            if (string.IsNullOrWhiteSpace(user.Role)) user.Role = role;
            if (string.IsNullOrWhiteSpace(user.PasswordHash)) user.PasswordHash = PasswordHash.Hash(password);
            user.IsActive = true;
            return user;
        }

        user = new AppUser
        {
            FullName = fullName, Email = email, PasswordHash = PasswordHash.Hash(password), Role = role,
            IsActive = true, CreatedAt = now, CreatedBy = "Seeder"
        };
        db.Users.Add(user);
        return user;
    }

    private static async Task<Product> EnsureProductAsync(ApplicationDbContext db, string code, string name, string description)
    {
        var product = await db.Products.FirstOrDefaultAsync(x => x.Code == code);
        if (product is not null) return product;
        product = new Product { Code = code, Name = name, Description = description, IsActive = true };
        db.Products.Add(product);
        return product;
    }

    private static async Task<BottleType> EnsureBottleAsync(ApplicationDbContext db, string name, int capacity, decimal deposit)
    {
        var bottle = await db.BottleTypes.FirstOrDefaultAsync(x => x.Name == name);
        if (bottle is not null) return bottle;
        bottle = new BottleType { Name = name, CapacityMl = capacity, DepositAmount = deposit, IsActive = true };
        db.BottleTypes.Add(bottle);
        return bottle;
    }

    private static async Task EnsureRateAsync(ApplicationDbContext db, int? customerId, int? productId,
        int? bottleTypeId, decimal amount, DateTime now)
    {
        if (await db.Rates.AnyAsync(x => x.CustomerId == customerId && x.ProductId == productId &&
                                         x.BottleTypeId == bottleTypeId)) return;
        db.Rates.Add(new Rate
        {
            CustomerId = customerId, ProductId = productId, BottleTypeId = bottleTypeId, Amount = amount,
            EffectiveFrom = new DateTime(2026, 1, 1), CreatedAt = now, CreatedBy = "Seeder"
        });
    }
}
