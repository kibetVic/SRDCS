// Models/SACCO.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRDCS.Models.Entities
{
    public class SACCO
    {
        [Key]
        public int SACCOId { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? SACCOName { get; set; }
        public string? County { get; set; }
        public string? SubCounty { get; set; }
        public DateTime RegistrationDate { get; set; }
        public string? SACCOType { get; set; } // "Deposit_Taking" or "Non_DT"
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public DateTime DateUpdated { get; set; }
        public string? updateBy { get; set; }
        public string Status { get; set; } = "Active";

        // Navigation properties
        public virtual ICollection<User> Users { get; set; }
        public virtual ICollection<MonthlyReturn> MonthlyReturns { get; set; }

        internal object? ToUpper()
        {
            throw new NotImplementedException();
        }
    }

    public enum SACCOType
    {
        Deposit_Taking,
        Non_DT
    }
}