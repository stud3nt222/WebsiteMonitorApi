using System.Text.Json.Serialization;

namespace WebsiteMonitorApi.Models
{
    // сутність бази даних, що описує налаштування веб-ресурсу
    public class Website
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; 
        public string Url { get; set; } = string.Empty; 
        public string? ExpectedKeyword { get; set; } 
        public bool IsActive { get; set; } = true;
        public bool IsCurrentlyDown { get; set; } = false;

        [JsonIgnore]
        public ICollection<MonitoringLog>? Logs { get; set; }
    }
}