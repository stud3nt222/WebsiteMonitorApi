using Microsoft.EntityFrameworkCore;
using WebsiteMonitorApi.Models;

namespace WebsiteMonitorApi.Data
{
    // головний клас для налаштування підключення та взаємодії з базою даних через Entity Framework Core
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        public DbSet<Website> Websites { get; set; }
        public DbSet<MonitoringLog> MonitoringLogs { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                entity.SetTableName(entity.GetTableName().ToLower());
            }
        }
    }
}