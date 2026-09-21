using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNet.Identity.EntityFramework;

namespace MusicBoxManagement.Models
{
    public class RolePermission
    {
        [Key]
        [Column(Order = 0)]
        public string RoleId { get; set; }

        [Key]
        [Column(Order = 1)]
        public int PermissionId { get; set; }

        [ForeignKey(nameof(RoleId))]
        public virtual IdentityRole Role { get; set; }

        [ForeignKey(nameof(PermissionId))]
        public virtual Permission Permission { get; set; }
    }
}
