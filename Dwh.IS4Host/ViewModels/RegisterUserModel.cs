using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Dwh.IS4Host.ViewModels
{
    public class RegisterUserModel
    {
        [Required] 
        public int Title { get; set; }

        [Required] 
        public string FullName { get; set; }

        [Required]
        public Guid OrganizationId { get; set; }

        [Required]
        public string Designation { get; set; }

        [Required]
        public string ReasonForAccessing { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string PhoneNumber { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Confirm password doesn't match")]
        public string ConfirmPassword { get; set; }

        [Required]
        public string CaptchaCode { get; set; }
    }
}
