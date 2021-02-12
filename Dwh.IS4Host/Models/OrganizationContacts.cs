using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dwh.IS4Host.Models
{
    public class OrganizationContacts
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public string Names { get; set; }
        public string Title { get; set; }
        public string Email { get; set; }
        public string Mobile { get; set; }
        public int PointPerson { get; set; }
    }
}
