using System.Text.Json;
using LifeMineralWater.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LifeMineralWater.Data;

public class ApplicationDbContext : DbContext
{
    private readonly IAuditContext _audit;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IAuditContext audit) : base(options)
        => _audit = audit;

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Salesman> Salesmen => Set<Salesman>();
    public DbSet<StaffMember> Staff => Set<StaffMember>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<BottleType> BottleTypes => Set<BottleType>();
    public DbSet<Rate> Rates => Set<Rate>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<ModificationRequest> ModificationRequests => Set<ModificationRequest>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<StaffAdvance> StaffAdvances => Set<StaffAdvance>();
    public DbSet<PayrollEntry> PayrollEntries => Set<PayrollEntry>();
    public DbSet<CashBookEntry> CashBookEntries => Set<CashBookEntry>();
    public DbSet<BankBookEntry> BankBookEntries => Set<BankBookEntry>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierLedgerEntry> SupplierLedgerEntries => Set<SupplierLedgerEntry>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // One money precision rule for the entire schema.
        foreach (var entityType in b.Model.GetEntityTypes())
        foreach (var prop in entityType.GetProperties())
            if (prop.ClrType == typeof(decimal) || prop.ClrType == typeof(decimal?))
                prop.SetColumnType("decimal(18,2)");

        // Soft-deleted ledger rows never leak into read queries.
        b.Entity<LedgerEntry>().HasQueryFilter(e => !e.IsDeleted);

        b.Entity<AppUser>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("UX_Users_Email");
            e.HasIndex(x => new { x.Role, x.IsActive }).HasDatabaseName("IX_Users_Role_Active");
            e.Property(x => x.Role).HasMaxLength(20);
        });

        b.Entity<Customer>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_Customers_Code");
            e.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("UX_Customers_User");
            e.HasIndex(x => new { x.AssignedSalesmanId, x.Status }).HasDatabaseName("IX_Customers_Salesman_Status");
            e.HasOne(x => x.User).WithOne(x => x.CustomerProfile)
             .HasForeignKey<Customer>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AssignedSalesman).WithMany(x => x.AssignedCustomers)
             .HasForeignKey(x => x.AssignedSalesmanId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Salesman>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_Salesmen_Code");
            e.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("UX_Salesmen_User");
            e.HasOne(x => x.User).WithOne(x => x.SalesmanProfile)
             .HasForeignKey<Salesman>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<StaffMember>().HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_Staff_Code");

        b.Entity<Product>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_Products_Code");
            e.HasIndex(x => x.Name).IsUnique().HasDatabaseName("UX_Products_Name");
        });

        b.Entity<BottleType>().HasIndex(x => x.Name).IsUnique().HasDatabaseName("UX_BottleTypes_Name");

        b.Entity<Rate>(e =>
        {
            e.HasIndex(x => new { x.CustomerId, x.ProductId, x.BottleTypeId, x.EffectiveFrom })
             .HasDatabaseName("IX_Rates_Lookup");
            e.HasIndex(x => new { x.EffectiveFrom, x.EffectiveTo, x.IsActive })
             .HasDatabaseName("IX_Rates_Effective");
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.BottleType).WithMany().HasForeignKey(x => x.BottleTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<LedgerEntry>(e =>
        {
            e.HasIndex(x => new { x.CustomerId, x.EntryDate }).HasDatabaseName("IX_Ledger_Customer_Date");
            e.HasIndex(x => new { x.CustomerId, x.EntryType }).HasDatabaseName("IX_Ledger_Customer_Type");
            e.HasIndex(x => new { x.SalesmanId, x.EntryDate }).HasDatabaseName("IX_Ledger_Salesman_Date");
            e.HasIndex(x => x.EntryDate).HasDatabaseName("IX_Ledger_Date");
            e.HasIndex(x => new { x.IsDeleted, x.EntryDate }).HasDatabaseName("IX_Ledger_Deleted_Date");
            e.HasIndex(x => x.ReferenceNo).HasDatabaseName("IX_Ledger_Reference");
            e.HasOne(x => x.Customer).WithMany(x => x.LedgerEntries)
             .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Salesman).WithMany().HasForeignKey(x => x.SalesmanId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.BottleType).WithMany().HasForeignKey(x => x.BottleTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ModificationRequest>(e =>
        {
            e.HasIndex(x => new { x.Status, x.RequestedAt }).HasDatabaseName("IX_Requests_Status_Date");
            e.HasIndex(x => x.LedgerEntryId).HasDatabaseName("IX_Requests_Entry");
            e.HasOne(x => x.LedgerEntry).WithMany().HasForeignKey(x => x.LedgerEntryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.RequestedBy).WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Expense>(e =>
        {
            e.HasIndex(x => new { x.SalesmanId, x.ExpenseDate }).HasDatabaseName("IX_Expenses_Salesman_Date");
            e.HasIndex(x => x.ExpenseDate).HasDatabaseName("IX_Expenses_Date");
            e.HasOne(x => x.RecordedBy).WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Salesman).WithMany().HasForeignKey(x => x.SalesmanId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<StaffAdvance>(e =>
        {
            e.HasIndex(x => new { x.StaffId, x.AdvanceDate }).HasDatabaseName("IX_Advances_Staff_Date");
            e.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<PayrollEntry>(e =>
        {
            e.HasIndex(x => new { x.StaffId, x.Period }).IsUnique().HasDatabaseName("UX_Payroll_Staff_Period");
            e.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CashBookEntry>(e =>
        {
            e.HasIndex(x => new { x.SalesmanId, x.TransactionDate }).HasDatabaseName("IX_Cash_Salesman_Date");
            e.HasIndex(x => x.TransactionDate).HasDatabaseName("IX_Cash_Date");
            e.HasOne(x => x.Salesman).WithMany().HasForeignKey(x => x.SalesmanId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<BankBookEntry>(e =>
        {
            e.HasIndex(x => new { x.BankAccountId, x.TransactionDate }).HasDatabaseName("IX_BankBook_Account_Date");
            e.HasIndex(x => x.TransactionDate).HasDatabaseName("IX_BankBook_Date");
            e.HasOne(x => x.BankAccount).WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<SupplierLedgerEntry>(e =>
        {
            e.HasIndex(x => new { x.SupplierId, x.TransactionDate }).HasDatabaseName("IX_SupplierLedger_Supplier_Date");
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<SystemSetting>().HasIndex(x => x.Key).IsUnique().HasDatabaseName("UX_Settings_Key");

        b.Entity<Announcement>().HasIndex(x => new { x.IsActive, x.StartOn, x.EndOn }).HasDatabaseName("IX_Announcements_Active");

        b.Entity<AuditLog>(e =>
        {
            e.HasIndex(x => new { x.EntityName, x.EntityId, x.OccurredAt }).HasDatabaseName("IX_Audit_Entity");
            e.HasIndex(x => x.OccurredAt).HasDatabaseName("IX_Audit_Date");
        });
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        WriteAudit();
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges()
    {
        WriteAudit();
        return base.SaveChanges();
    }

    private void WriteAudit()
    {
        var audited = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(AppUser), nameof(Customer), nameof(Salesman), nameof(StaffMember), nameof(Product),
            nameof(BottleType), nameof(Rate), nameof(LedgerEntry), nameof(ModificationRequest),
            nameof(Expense), nameof(StaffAdvance), nameof(PayrollEntry), nameof(CashBookEntry),
            nameof(BankBookEntry), nameof(BankAccount), nameof(Supplier), nameof(SupplierLedgerEntry),
            nameof(SystemSetting), nameof(Announcement)
        };

        var tracked = ChangeTracker.Entries()
            .Where(x => x.State != EntityState.Unchanged && audited.Contains(x.Entity.GetType().Name))
            .ToList();

        foreach (var entry in tracked)
        {
            // Recalculated balance snapshots are not business edits: avoid audit noise/volume.
            if (entry.State == EntityState.Modified && entry.Entity is LedgerEntry &&
                entry.Properties.All(p => !p.IsModified ||
                    p.Metadata.Name is nameof(LedgerEntry.MoneyBalanceAfter) or nameof(LedgerEntry.BottleBalanceAfter)))
                continue;

            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Insert,
                EntityState.Deleted => AuditAction.Delete,
                _ => AuditAction.Update
            };

            var scalar = entry.Properties
                .Where(p => p.Metadata.PropertyInfo is not null)
                .ToList();

            var oldValues = action == AuditAction.Insert ? null
                : JsonSerializer.Serialize(scalar.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue));
            var newValues = action == AuditAction.Delete ? null
                : JsonSerializer.Serialize(scalar.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue));
            var columns = string.Join(", ", entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name));

            var keys = entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => p.CurrentValue?.ToString()).ToArray();

            AuditLogs.Add(new AuditLog
            {
                EntityName = entry.Entity.GetType().Name,
                EntityId = string.Join("|", keys),
                Action = action,
                OldValues = oldValues,
                NewValues = newValues,
                AffectedColumns = columns.Length <= 1000 ? columns : columns[..1000],
                ActorUserId = _audit.UserId,
                ActorName = _audit.UserName ?? "System",
                Reason = _audit.Reason,
                OccurredAt = DateTime.UtcNow,
                CorrelationId = _audit.CorrelationId
            });
        }
    }
}
