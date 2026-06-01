using WebsiteMonitorApi.Models;

namespace WebsiteMonitorApi.Services
{
    public interface IMonitoringService
    {
        Task CheckWebsiteAsync(Website website);
        Task CheckAllActiveWebsitesAsync();
    }
}