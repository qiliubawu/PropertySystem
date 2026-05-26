using Microsoft.EntityFrameworkCore;
using PropertySystem.Models;

namespace PropertySystem.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<User> Users { get; set; } 
        public DbSet<House> Houses { get; set; }
        public DbSet<Owner> Owners { get; set; }
        public DbSet<ParkingSpace> ParkingSpaces { get; set; }
        public DbSet<Bill> Bills { get; set; }
        public DbSet<RepairRequest> RepairRequests { get; set; }
        public DbSet<Announcement> Announcements { get; set; }
        public DbSet<Equipment> Equipments { get; set; }
        public DbSet<Visitor> Visitors { get; set; }
        public DbSet<Complaint> Complaints { get; set; }
        public DbSet<CleanerTask> CleanerTasks { get; set; }
        public DbSet<ShiftHandover> ShiftHandovers { get; set; }
        public DbSet<SystemConfig> SystemConfigs { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Material> Materials { get; set; }
        public DbSet<RepairMaterial> RepairMaterials { get; set; }
        public DbSet<MaterialRequest> MaterialRequests { get; set; }

    }
}
