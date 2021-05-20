using System.ComponentModel.DataAnnotations.Schema;

namespace Dwh.IS4Host.Models
{
    [Table("lkp_USGPartnerMenchanism")]
    public class UsgPartnerMenchanism
    {
        public string MFL_Code { get; set; }
        public string FacilityName { get; set; }
        public string County { get; set; }
        public string Agency { get; set; }
        public string Implementing_Mechanism_Name { get; set; }
        public string Mechanism { get; set; }
    }
}