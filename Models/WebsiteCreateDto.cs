namespace WebsiteMonitorApi.Models
{
    // DTO для безпечного отримання даних від клієнта без розкриття повної моделі БД
    public class WebsiteCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? ExpectedKeyword { get; set; }
    }
}