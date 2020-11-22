using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dwh.IS4Host.Models
{
    public class Impersonator
    {
        public Guid Id { get; set; }
        public string UserName { get; set; }
        public string UserId { get; set; }
        public string Password { get; set; }
        public bool IsDefault { get; set; }
        public bool IsDisabled { get; set; }
    }
}
