using System.ComponentModel.DataAnnotations;

namespace MusicBoxManagement.Models
{
    public class Permission
    {
        public int PermissionId { get; set; }

        [Required]
        [StringLength(128)]
        public string Code { get; set; }

        [Required]
        public string Name { get; set; }
    }
}
