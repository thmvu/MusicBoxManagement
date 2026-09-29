using System;
using System.Collections.Generic;

namespace MusicBoxManagement.Models
{
    public sealed class PermissionMatrixRowViewModel
    {
        public string Code { get; set; }
        public bool StaffGranted { get; set; }
        public bool ManagerGranted { get; set; }
        public bool StaffEditable { get; set; }
        public bool ManagerEditable { get; set; }
    }

    public sealed class AuditLogSearchViewModel
    {
        public string Action { get; set; }
        public string EntityName { get; set; }
        public string FromDate { get; set; }
        public string ToDate { get; set; }
        public int Page { get; set; } = 1;
        public bool HasNext { get; set; }
        public IList<AuditLogItemViewModel> Items { get; set; } = new List<AuditLogItemViewModel>();
    }

    public sealed class AuditLogItemViewModel
    {
        public int AuditLogId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string ActorType { get; set; }
        public string UserName { get; set; }
        public string Action { get; set; }
        public string EntityName { get; set; }
        public string EntityId { get; set; }
        public string Description { get; set; }
    }
}
