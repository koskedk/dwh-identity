using System;
using System.ComponentModel.DataAnnotations;

namespace Dwh.IS4Host.ViewModels
{
    public class UpdateUserModel
    {
        [Required]
        public string Designation
        {
            get;
            set;
        }

        [EmailAddress]
        [Required]
        public string Email
        {
            get;
            set;
        }

        [Required]
        public string FullName
        {
            get;
            set;
        }

        public string Id
        {
            get;
            set;
        }

        [Required]
        public Guid OrganizationId
        {
            get;
            set;
        }

        [Required]
        public string PhoneNumber
        {
            get;
            set;
        }

        [Required]
        public string ReasonForAccessing
        {
            get;
            set;
        }

        public bool SubscribeToNewsLetter
        {
            get;
            set;
        }

        [Required]
        public int Title
        {
            get;
            set;
        }
    }
}