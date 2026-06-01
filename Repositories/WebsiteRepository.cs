using Microsoft.EntityFrameworkCore;
using WebsiteMonitorApi.Data;
using WebsiteMonitorApi.Models;

namespace WebsiteMonitorApi.Repositories
{
    // шар доступу до даних: інкапсулює всі прямі запити до бази даних
    public class WebsiteRepository : IWebsiteRepository
    {
        private readonly AppDbContext _context;

        public WebsiteRepository(AppDbContext context)
        {
            _context = context;
        }

        // отримуємо лише ті сайти, які зараз увімкнені для моніторингу
        public async Task<IEnumerable<Website>> GetAllActiveWebsitesAsync()
        {
            return await _context.Websites
                .Where(w => w.IsActive)
                .ToListAsync();
        }

        // пошук конкретного ресурсу за його унікальним ідентифікатором
        public async Task<Website?> GetWebsiteByIdAsync(int id)
        {
            return await _context.Websites.FindAsync(id);
        }

        // додавання нового сайту в таблицю
        public async Task AddWebsiteAsync(Website website)
        {
            await _context.Websites.AddAsync(website);
        }

        // оновлення існуючого запису
        public async Task UpdateWebsiteAsync(Website website)
        {
            _context.Websites.Update(website);
            await _context.SaveChangesAsync();
        }

        // запис результатів кожної окремої перевірки (логування)
        public async Task AddMonitoringLogAsync(MonitoringLog log)
        {
            await _context.MonitoringLogs.AddAsync(log);
        }

        // фізичне збереження всіх накопичених змін у базу даних
        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        // отримання останніх 50 логів для конкретного сайту (для відображення історії)
        public async Task<IEnumerable<MonitoringLog>> GetLogsByWebsiteIdAsync(int websiteId)
        {
            return await _context.MonitoringLogs
                .Where(l => l.WebsiteId == websiteId)
                .OrderByDescending(l => l.Id)
                .Take(50)
                .ToListAsync();
        }
    }
}