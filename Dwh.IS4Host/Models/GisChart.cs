using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dwh.IS4Host.Models
{
    public class GisChart
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Link { get; set; }
        public int Section { get; set; }
        public int Status { get; set; }
    }
}
