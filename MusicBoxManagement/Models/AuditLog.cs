using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MusicBoxManagement.Models
{
    public class AuditLog
    {
        public int AuditLogId { get; set; }

        [Required]
        public string ActorType { get; set; }

        public string UserId { get; set; }

        [Required]
        public string Action { get; set; }

        [Required]
        public string EntityName { get; set; }

        public string EntityId { get; set; }

        public string Description { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser User { get; set; }
    }
}
