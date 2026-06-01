using WebsiteMonitorApi.Models;

namespace WebsiteMonitorApi.Repositories
{
    public interface IWebsiteRepository
    {
        Task<IEnumerable<Website>> GetAllActiveWebsitesAsync();
        Task<Website?> GetWebsiteByIdAsync(int id);
        Task AddWebsiteAsync(Website website);
        Task UpdateWebsiteAsync(Website website);
        Task AddMonitoringLogAsync(MonitoringLog log);
        Task SaveChangesAsync();
        Task<IEnumerable<MonitoringLog>> GetLogsByWebsiteIdAsync(int websiteId);
    }
}