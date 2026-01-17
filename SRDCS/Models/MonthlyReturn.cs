// Models/MonthlyReturn.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection.Metadata;

namespace SRDCS.Models.Entities
{
    public class MonthlyReturn
    {
        [Key]
        public int ReturnId { get; set; }

        [Required]
        [ForeignKey("SACCO")]
        public int SACCOId { get; set; }

        [Required]
        public DateTime ReportingMonth { get; set; } // First day of the month

        public DateTime SubmissionDate { get; set; } = DateTime.UtcNow;

        [ForeignKey("SubmittedByUser")]
        public int SubmittedBy { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Draft"; // Draft, Submitted, Under_Review, Approved, Rejected, Flagged

        public string ReviewNotes { get; set; }

        [ForeignKey("ReviewedByUser")]
        public int? ReviewedBy { get; set; }

        public DateTime? ReviewDate { get; set; }

        // Navigation properties
        public virtual SACCO SACCO { get; set; }
        public virtual User SubmittedByUser { get; set; }
        public virtual User ReviewedByUser { get; set; }
        public virtual FinancialData FinancialData { get; set; }
        public virtual ICollection<Document> Documents { get; set; }
    }

    // Optional: Enum for Return Status
    public enum ReturnStatus
    {
        Draft,
        Submitted,
        Under_Review,
        Approved,
        Rejected,
        Flagged
    }
}