using System.ComponentModel.DataAnnotations;
namespace PropertySystem.Models
{
    public class Material
    {
        public int Id { get; set; }
        public string SKU { get; set; }
        public string Name { get; set; }
        public int Stock { get; set; }
        public decimal UnitPrice { get; set; }
        public string Unit { get; set; }
    }
}
