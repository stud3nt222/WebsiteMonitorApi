using System.Diagnostics;
using HtmlAgilityPack;
using WebsiteMonitorApi.Models;
using WebsiteMonitorApi.Repositories;

namespace WebsiteMonitorApi.Services
{
    // головний сервіс, який виконує фізичну перевірку сайтів
    public class MonitoringService : IMonitoringService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IWebsiteRepository _repository;
        private readonly ILogger<MonitoringService> _logger;
        private readonly IConfiguration _configuration;
        private const int MaxAcceptableResponseTimeMs = 3000; 

        public MonitoringService(IHttpClientFactory httpClientFactory, IWebsiteRepository repository, ILogger<MonitoringService> logger, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _repository = repository;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task CheckAllActiveWebsitesAsync()
        {
            // отримуємо список активних сайтів, які потрібно перевірити
            var websites = await _repository.GetAllActiveWebsitesAsync();
            // запускаємо перевірку всіх сайтів одночасно для економії часу
            var tasks = websites.Select(website => CheckWebsiteInternalAsync(website)).ToList();
            var logs = await Task.WhenAll(tasks);

            // зберігаємо результати кожної перевірки у базу даних
            foreach (var log in logs)
            {
                await _repository.AddMonitoringLogAsync(log);
            }
            await _repository.SaveChangesAsync();

            _logger.LogInformation($"Всі сайти успішно перевірено паралельно. Оброблено логів: {logs.Length}");
        }

        private async Task<MonitoringLog> CheckWebsiteInternalAsync(Website website)
        {
            // створюємо порожній запис результату перевірки
            var log = new MonitoringLog { WebsiteId = website.Id };
            var stopwatch = Stopwatch.StartNew();
            int maxAttempts = 3; // встановлення ліміту спроб для Resilience

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    // ініціалізація HTTP-клієнта з налаштуванням User-Agent для обходу анти-бот систем
                    var client = _httpClientFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(10);
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                    var response = await client.GetAsync(website.Url);
                    log.StatusCode = (int)response.StatusCode;
                    log.IsSuccess = response.IsSuccessStatusCode;

                    // логіка валідації контенту (якщо задано очікуване ключове слово)
                    if (log.IsSuccess && !string.IsNullOrEmpty(website.ExpectedKeyword))
                    {
                        var html = await response.Content.ReadAsStringAsync();
                        var htmlDoc = new HtmlDocument();
                        htmlDoc.LoadHtml(html);
                        var pageText = htmlDoc.DocumentNode.InnerText;
                        var keywords = website.ExpectedKeyword.Split(',').Select(k => k.Trim()).ToList();
                        
                        // пошук відсутніх ключових слів на сторінці
                        var missingKeywords = new List<string>();
                        foreach (var keyword in keywords)
                        {
                            if (!pageText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                            {
                                missingKeywords.Add(keyword);
                            }
                        }

                        if (missingKeywords.Any())
                        {
                            log.IsSuccess = false;
                            log.ErrorMessage = $"Не знайдено очікуваний контент: {string.Join(", ", missingKeywords)}";
                        }
                    }
                    else if (!log.IsSuccess)
                    {
                        log.ErrorMessage = $"Помилка HTTP: {response.StatusCode}";
                    }
                }
                catch (Exception ex)
                {
                    // обробка мережевих виключень та таймаутів
                    log.IsSuccess = false;
                    log.ErrorMessage = $"Таймаут або помилка з'єднання: {ex.Message}";
                }

                if (log.IsSuccess) break;

                // реалізація затримки перед повторною спробою
                if (attempt < maxAttempts)
                {
                    _logger.LogWarning($"Спроба {attempt} для {website.Url} невдала. Повтор через 2 секунди...");
                    await Task.Delay(2000);
                }
            }
            stopwatch.Stop();
            log.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            _logger.LogInformation($"Перевірка {website.Url} завершена: Успіх={log.IsSuccess}, Час={log.ResponseTimeMs}мс");
            await ProcessAlertsAsync(website, log); // надсилання сповіщення якщо зміна стану

            return log;
        }



        private async Task ProcessAlertsAsync(Website website, MonitoringLog log)
        {
            // відправка повідомлення при зміні стану на "впав"
            if (!log.IsSuccess && !website.IsCurrentlyDown)
            {
                website.IsCurrentlyDown = true;
                await _repository.UpdateWebsiteAsync(website);

                var message = $"🚨 <b>САЙТ ВПАВ!</b>\n\n" +
                              $"🌐 <b>Сайт:</b> {website.Name}\n" +
                              $"🔗 <b>URL:</b> {website.Url}\n" +
                              $"❌ <b>Причина:</b> {log.ErrorMessage}\n" +
                              $"⏱ <b>Час падіння:</b> {DateTime.UtcNow.AddHours(3):HH:mm:ss} (Київ)";

                await SendTelegramAlertAsync(message);
            }
            // відправка повідомлення про відновлення при зміні стану на "працює"
            else if (log.IsSuccess && website.IsCurrentlyDown)
            {
                website.IsCurrentlyDown = false;
                await _repository.UpdateWebsiteAsync(website);

                var message = $"✅ <b>САЙТ ВІДНОВЛЕНО!</b>\n\n" +
                              $"🌐 <b>Сайт:</b> {website.Name}\n" +
                              $"⚡ <b>Час відгуку:</b> {log.ResponseTimeMs} мс";

                await SendTelegramAlertAsync(message);
            }
            // моніторинг деградації: сайт працює, але час відгуку перевищує норму
            else if (log.IsSuccess && log.ResponseTimeMs > MaxAcceptableResponseTimeMs)
            {
                var message = $"⚠️ <b>САЙТ ГАЛЬМУЄ! (Деградація сервісу)</b>\n\n" +
                              $"🌐 <b>Сайт:</b> {website.Name}\n" +
                              $"🐌 <b>Час відгуку:</b> {log.ResponseTimeMs} мс (Норма: <{MaxAcceptableResponseTimeMs} мс)";

                await SendTelegramAlertAsync(message);
            }
        }

        public async Task CheckWebsiteAsync(Website website)
        {
            // ручний запуск перевірки для одного конкретного сайту
            var log = await CheckWebsiteInternalAsync(website);
            await _repository.AddMonitoringLogAsync(log);
            await _repository.SaveChangesAsync();
        }

        private async Task SendTelegramAlertAsync(string message)
        {
            // отримуємо ключі доступу до бота з файлу налаштувань
            var token = _configuration["TelegramBot:Token"];
            var chatId = _configuration["TelegramBot:ChatId"];

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(chatId)) return;

            // формуємо запит до офіційного сервера Telegram для відправки повідомлення
            var url = $"https://api.telegram.org/bot{token}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(message)}&parse_mode=HTML";

            try
            {
                var client = _httpClientFactory.CreateClient();
                await client.GetAsync(url);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Помилка відправки в Telegram: {ex.Message}");
            }
        }
    }
}