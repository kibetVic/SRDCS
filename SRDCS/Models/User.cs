// Models/User.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection.Metadata;

namespace SRDCS.Models.Entities
{
    public class User
    {
        [Key]
        public int UserId { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? UserType { get; set; } // "SACCO_Manager", "Accounts_Officer", etc.

        [ForeignKey("SACCO")]
        public int? SACCOId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLogin { get; set; }

        // Navigation properties
        public virtual SACCO? SACCO { get; set; }
        public virtual ICollection<MonthlyReturn>? MonthlyReturns { get; set; }
        public virtual ICollection<Document>? Documents { get; set; }
        public virtual ICollection<AuditLog>? AuditLogs { get; set; }

        // Computed property for full name
        [NotMapped]
        public string FullName => $"{FirstName} {LastName}".Trim();
    }
    public enum UserType
    {
        SACCO_Manager,
        Accounts_Officer,
        Data_Entry_Officer,
        Analyst,
        Supervisor,
        System_Admin
    }
}