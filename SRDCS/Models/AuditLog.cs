// Models/AuditLog.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRDCS.Models.Entities
{
    public class AuditLog
    {
        [Key]
        public int LogId { get; set; }

        [ForeignKey("User")]
        public int? UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string Action { get; set; }

        [StringLength(50)]
        public string EntityType { get; set; }

        public int? EntityId { get; set; }

        public string OldValues { get; set; }
        public string NewValues { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [StringLength(45)]
        public string IPAddress { get; set; }

        // Navigation property
        public virtual User User { get; set; }
    }
}