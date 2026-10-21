using LifeMineralWater.Models;
using Microsoft.EntityFrameworkCore;

namespace LifeMineralWater.Data
{
    public class ApplicationDbContext:DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<CustomerProfile> CustomerProfiles { get; set; }
        public DbSet<Transaction> Transactions { get; set; } 
        public DbSet<Expense> Expenses { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Strict Code-First Initial Model Seeding for Dropdown Population
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "Admin" },
                new Role { Id = 2, Name = "Salesman" },
                new Role { Id = 3, Name = "Customer" }
            );

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Customer)
                .WithMany()
                .HasForeignKey(t => t.CustomerId)
                .OnDelete(DeleteBehavior.Restrict); // Block cascade path 1

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Salesman)
                .WithMany()
                .HasForeignKey(t => t.SalesmanId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
