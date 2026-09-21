using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MusicBoxManagement.Models
{
    public class UserListItemViewModel
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateUserViewModel
    {
        [Required]
        [StringLength(256)]
        [Display(Name = "Tên đăng nhập")]
        public string UserName { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Họ tên")]
        public string FullName { get; set; }

        [Required]
        [Display(Name = "Vai trò")]
        public string Role { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu ban đầu")]
        public string Password { get; set; }
    }

    public class EditUserViewModel
    {
        public string Id { get; set; }
        public string UserName { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Họ tên")]
        public string FullName { get; set; }

        [Required]
        [Display(Name = "Vai trò")]
        public string Role { get; set; }

        [Display(Name = "Tài khoản hoạt động")]
        public bool IsActive { get; set; }
    }

    public class ResetUserPasswordViewModel
    {
        public string Id { get; set; }
        public string UserName { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu mới")]
        public string NewPassword { get; set; }
    }

    public class UserManagementResult
    {
        public bool Succeeded { get; private set; }
        public string UserId { get; private set; }
        public IEnumerable<string> Errors { get; private set; }

        public static UserManagementResult Success(string userId)
        {
            return new UserManagementResult { Succeeded = true, UserId = userId, Errors = new string[0] };
        }

        public static UserManagementResult Failure(params string[] errors)
        {
            return new UserManagementResult { Succeeded = false, Errors = errors };
        }
    }
}
