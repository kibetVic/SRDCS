// Data/ApplicationDbContext.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SRDCS.Models.Entities;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace SRDCS.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSets for each entity
        public DbSet<User> Users { get; set; }
        public DbSet<SACCO> SACCOs { get; set; }
        public DbSet<MonthlyReturn> MonthlyReturns { get; set; }
        public DbSet<FinancialData> FinancialData { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure unique constraints
            modelBuilder.Entity<SACCO>()
                .HasIndex(s => s.RegistrationNumber)
                .IsUnique();

            modelBuilder.Entity<MonthlyReturn>()
                .HasIndex(mr => new { mr.SACCOId, mr.ReportingMonth })
                .IsUnique();

            // Configure relationships
            // SACCO -> Users (One-to-Many)
            modelBuilder.Entity<SACCO>()
                .HasMany(s => s.Users)
                .WithOne(u => u.SACCO)
                .HasForeignKey(u => u.SACCOId)
                .OnDelete(DeleteBehavior.Restrict);

            // User -> MonthlyReturns (Submitted By)
            modelBuilder.Entity<User>()
                .HasMany(u => u.MonthlyReturns)
                .WithOne(mr => mr.SubmittedByUser)
                .HasForeignKey(mr => mr.SubmittedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // MonthlyReturn -> FinancialData (One-to-One)
            modelBuilder.Entity<MonthlyReturn>()
                .HasOne(mr => mr.FinancialData)
                .WithOne(fd => fd.MonthlyReturn)
                .HasForeignKey<FinancialData>(fd => fd.ReturnId)
                .OnDelete(DeleteBehavior.Cascade);

            // MonthlyReturn -> Documents (One-to-Many)
            modelBuilder.Entity<MonthlyReturn>()
                .HasMany(mr => mr.Documents)
                .WithOne(d => d.MonthlyReturn)
                .HasForeignKey(d => d.ReturnId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure default values
            modelBuilder.Entity<MonthlyReturn>()
                .Property(mr => mr.Status)
                .HasDefaultValue("Draft");

            modelBuilder.Entity<SACCO>()
                .Property(s => s.Status)
                .HasDefaultValue("Active");

            modelBuilder.Entity<User>()
                .Property(u => u.IsActive)
                .HasDefaultValue(true);

            // Configure decimal precision
            modelBuilder.Entity<FinancialData>()
                .Property(fd => fd.ShareCapital)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FinancialData>()
                .Property(fd => fd.MemberDeposits)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FinancialData>()
                .Property(fd => fd.TotalAssets)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FinancialData>()
                .Property(fd => fd.TotalLiabilities)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FinancialData>()
                .Property(fd => fd.PAR30)
                .HasPrecision(5, 2);

            modelBuilder.Entity<FinancialData>()
                .Property(fd => fd.PAR60)
                .HasPrecision(5, 2);

            modelBuilder.Entity<FinancialData>()
                .Property(fd => fd.PAR90)
                .HasPrecision(5, 2);

            // Seed data for roles
            modelBuilder.Entity<IdentityRole<int>>().HasData(
                new IdentityRole<int> { Id = 1, Name = "SACCO_Manager", NormalizedName = "SACCO_MANAGER" },
                new IdentityRole<int> { Id = 2, Name = "Accounts_Officer", NormalizedName = "ACCOUNTS_OFFICER" },
                new IdentityRole<int> { Id = 3, Name = "Data_Entry_Officer", NormalizedName = "DATA_ENTRY_OFFICER" },
                new IdentityRole<int> { Id = 4, Name = "Analyst", NormalizedName = "ANALYST" },
                new IdentityRole<int> { Id = 5, Name = "Supervisor", NormalizedName = "SUPERVISOR" },
                new IdentityRole<int> { Id = 6, Name = "System_Admin", NormalizedName = "SYSTEM_ADMIN" }
            );
        }
    }
}