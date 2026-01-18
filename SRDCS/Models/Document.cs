// Models/Document.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRDCS.Models.Entities
{
    public class Document
    {
        [Key]
        public int DocumentId { get; set; }

        [Required]
        [ForeignKey("MonthlyReturn")]
        public int ReturnId { get; set; }

        [Required]
        [StringLength(50)]
        public string? DocumentType { get; set; } // "Audited_Accounts", "Management_Report", etc.

        [Required]
        [StringLength(255)]
        public string? FileName { get; set; }

        [Required]
        [StringLength(500)]
        public string? FilePath { get; set; }

        public int FileSize { get; set; } // in bytes

        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        [ForeignKey("UploadedByUser")]
        public int UploadedBy { get; set; }

        // Navigation properties
        public virtual MonthlyReturn? MonthlyReturn { get; set; }
        public virtual User? UploadedByUser { get; set; }
    }

    // Optional: Enum for Document Types
    public enum DocumentType
    {
        Audited_Accounts,
        Management_Report,
        Board_Resolution,
        Other_Supporting
    }
}