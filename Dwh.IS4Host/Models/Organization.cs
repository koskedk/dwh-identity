using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dwh.IS4Host.Models
{
    public class Organization
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
    }
}
