using System.ComponentModel.DataAnnotations.Schema;
namespace PropertySystem.Models
{
    public class RepairMaterial
    {
        public int Id { get; set; }
        public int RepairRequestId { get; set; }
        public int MaterialId { get; set; }
        public int Quantity { get; set; }
        public decimal Cost { get; set; }

        [ForeignKey("MaterialId")] public virtual Material? Material { get; set; }
        // 👇 新增：物料核算成本
        public decimal MaterialCost { get; set; } = 0;

    }
}
