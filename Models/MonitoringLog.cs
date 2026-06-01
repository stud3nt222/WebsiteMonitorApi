using System.Text.Json.Serialization;

namespace WebsiteMonitorApi.Models
{
    // сутність бази даних для збереження історії перевірок (логів)
    public class MonitoringLog
    {
        public int Id { get; set; }
        public int WebsiteId { get; set; }

        [JsonIgnore]
        public Website? Website { get; set; } 
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow; 
        public int? StatusCode { get; set; } 
        public long ResponseTimeMs { get; set; } 
        public bool IsSuccess { get; set; } 
        public string? ErrorMessage { get; set; }
    }
}