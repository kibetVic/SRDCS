// Models/FinancialData.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRDCS.Models.Entities
{
    public class FinancialData
    {
        [Key]
        public int FinancialId { get; set; }

        [Required]
        [ForeignKey("MonthlyReturn")]
        public int ReturnId { get; set; }

        // Capital & Deposits
        [Column(TypeName = "decimal(18,2)")]
        public decimal ShareCapital { get; set; } = 0.00m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal MemberDeposits { get; set; } = 0.00m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAssets { get; set; } = 0.00m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalLiabilities { get; set; } = 0.00m;

        // Membership
        public int TotalMembers { get; set; } = 0;
        public int NewMembers { get; set; } = 0;
        public int ExitedMembers { get; set; } = 0;

        // Loans Portfolio
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalLoansCumulative { get; set; } = 0.00m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal LoansIssuedMonthly { get; set; } = 0.00m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal LoansRepaidMonthly { get; set; } = 0.00m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal OutstandingLoanBalance { get; set; } = 0.00m;

        public int NumberOfLoanees { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal InterestEarnedMonthly { get; set; } = 0.00m;

        // Portfolio Quality
        [Column(TypeName = "decimal(5,2)")]
        public decimal PAR30 { get; set; } = 0.00m;

        [Column(TypeName = "decimal(5,2)")]
        public decimal PAR60 { get; set; } = 0.00m;

        [Column(TypeName = "decimal(5,2)")]
        public decimal PAR90 { get; set; } = 0.00m;

        // Income & Expenses
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalIncomeMonthly { get; set; } = 0.00m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalExpensesMonthly { get; set; } = 0.00m;

        // Navigation property
        public virtual MonthlyReturn MonthlyReturn { get; set; }
    }
}