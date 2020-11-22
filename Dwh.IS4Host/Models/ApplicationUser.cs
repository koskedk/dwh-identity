using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using Microsoft.AspNetCore.Identity;

namespace Dwh.IS4Host.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        [Column("UserId")]
        public string Id { get; set; }
        public string FullName { get; set; }
        public int UserType { get; set; }
        public bool IsTableau { get; set; }
        public bool IsDisabled { get; set; }
        public int UserConfirmed { get; set; }
        public string ReasonForAccessing { get; set; }
        public string Designation { get; set; }
        public int Title { get; set; }
        public string Discriminator { get; set; }
        public string OrganizationId { get; set; }
        public string ImpersonatorId { get; set; }

        public ICollection<Impersonator> Actions { get; set; }
    }
}
