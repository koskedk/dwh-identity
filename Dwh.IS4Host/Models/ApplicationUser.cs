using System;
using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;

namespace Dwh.IS4Host.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
        public int UserType { get; set; }
        public bool IsTableau { get; set; }
        public bool IsDisabled { get; set; }
        public int UserConfirmed { get; set; }
        public string ReasonForAccessing { get; set; }
        public string Designation { get; set; }
        public int Title { get; set; }
        public string Discriminator { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid ImpersonatorId { get; set; }
    }

    public enum UserType
    {
        None,
        Admin,
        Steward,
        Normal,
        Guest
    }

    public enum Title
    {
        Mr = 1,
        Mrs,
        Ms,
        Dr,
        Prof,
        Eng
    }

    public enum UserConfirmation
    {
        Pending,
        Confirmed,
        Denyed
    }
}
