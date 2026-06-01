
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using WebsiteMonitorApi.Repositories;
using WebsiteMonitorApi.Models;

namespace WebsiteMonitorApi.Services
{
    // фоновий сервіс для прийому та обробки команд від telegram-бота
    public class TelegramBotListener : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TelegramBotListener> _logger;
        private TelegramBotClient? _botClient;

        public TelegramBotListener(IConfiguration configuration, IServiceProvider serviceProvider, ILogger<TelegramBotListener> logger)
        {
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // отримуємо токен доступу до бота з налаштувань
            var token = _configuration["TelegramBot:Token"];
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Telegram Token не знайдено в конфігурації системи.");
                return;
            }

            _botClient = new TelegramBotClient(token);

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };
            // запускаємо постійне прослуховування нових повідомлень (long polling)
            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: stoppingToken
            );
            _logger.LogInformation("Фоновий слухач Telegram Bot Listener успішно запущено.");
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        // головний метод, який обробляє кожне вхідне повідомлення
        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Message is not { } message || message.Text is not { } messageText)
                return;

            var chatId = message.Chat.Id;
            var allowedChatId = _configuration["TelegramBot:ChatId"];

            // перевірка прав доступу (блокуємо повідомлення від сторонніх користувачів)
            if (!string.IsNullOrEmpty(allowedChatId) && chatId.ToString() != allowedChatId)
            {
                await botClient.SendMessage(chatId, "🔒 <b>Доступ обмежено.</b> Ви не є адміністратором цієї системи моніторингу.", parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                return;
            }
            var text = messageText.Trim();
            var textLower = text.ToLower();

            // маршрутизація команд (визначає, яку дію хоче виконати адміністратор)
            if (textLower.StartsWith("/add "))
            {
                await HandleAddCommandAsync(chatId, text, cancellationToken);
                return;
            }
            if (textLower.StartsWith("/delete "))
            {
                await HandleDeleteCommandAsync(chatId, text, cancellationToken);
                return;
            }

            switch (textLower)
            {
                case "/start":
                case "меню":
                    await SendMenuAsync(chatId, cancellationToken);
                    break;

                case "🔍 статус проблемних":
                    await HandleStatusQueryAsync(chatId, cancellationToken);
                    break;

                case "📋 список всіх сайтів":
                    await HandleListQueryAsync(chatId, cancellationToken);
                    break;

                default:
                    await botClient.SendMessage(chatId, "❓ Невідома команда. Будь ласка, використовуйте інтерактивні кнопки меню або спеціальні команди: \n/add [URL] [Слово]\n/delete [ID]", cancellationToken: cancellationToken);
                    break;
            }
        }

        // створення та відправка зручної клавіатури з кнопками у чат
        private async Task SendMenuAsync(long chatId, CancellationToken cancellationToken)
        {
            var replyKeyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "🔍 Статус проблемних", "📋 Список всіх сайтів" }
            })
            {
                ResizeKeyboard = true 
            };
            await _botClient!.SendMessage(
                chatId: chatId,
                text: "🤖 <b>Вітаю в панелі керування інфраструктурою ChatOps!</b>\n\nСистема моніторингу активна. Виберіть необхідний звіт на клавіатурі:",
                parseMode: ParseMode.Html,
                replyMarkup: replyKeyboardMarkup,
                cancellationToken: cancellationToken
            );
        }

        // формування звіту лише про ті сайти, які наразі впали
        private async Task HandleStatusQueryAsync(long chatId, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IWebsiteRepository>();
            var websites = await repository.GetAllActiveWebsitesAsync();

            var downWebsites = websites.Where(w => w.IsCurrentlyDown).ToList();

            if (!downWebsites.Any())
            {
                await _botClient!.SendMessage(chatId, "💚 <b>Всі системи працюють стабільно!</b>\nНемає жодного ресурсу зі статусом відмови.", parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                return;
            }

            var report = "🚨 <b>Звіт про деградацію та падіння сервісів:</b>\n\n";
            foreach (var site in downWebsites)
            {
                report += $"🔴 <b>{site.Name}</b>\n🔗 {site.Url}\n⚠️ Статус: Потребує втручання адміністратора.\n\n";
            }
            await _botClient!.SendMessage(chatId, report, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
        }

        // виведення списку всіх підключених до моніторингу ресурсів
        private async Task HandleListQueryAsync(long chatId, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IWebsiteRepository>();
            var websites = await repository.GetAllActiveWebsitesAsync();

            if (!websites.Any())
            {
                await _botClient!.SendMessage(chatId, "📭 У базі даних моніторингу поки немає підключених сайтів.", cancellationToken: cancellationToken);
                return;
            }

            var report = "📋 <b>Загальний зріз стану підключених ресурсів:</b>\n\n";
            foreach (var site in websites)
            {
                var statusStr = site.IsCurrentlyDown ? "🔴 впав" : "🟢 OK";
                report += $"[{statusStr}] ID:{site.Id} — <b>{site.Name}</b>\n🔗 {site.Url}\n\n";
            }
            await _botClient!.SendMessage(chatId, report, parseMode: ParseMode.Html, linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true }, cancellationToken: cancellationToken);
        }

        // запис помилок зв'язку з серверами telegram
        private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            _logger.LogError($"Помилка пулінгу сервера Telegram API: {exception.Message}");
            return Task.CompletedTask;
        }

        // обробка команди додавання нового сайту через повідомлення
        private async Task HandleAddCommandAsync(long chatId, string text, CancellationToken cancellationToken)
        {
            var parts = text.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 3)
            {
                await _botClient!.SendMessage(chatId, "⚠️ <b>Помилка формату.</b>\nВикористовуйте: <code>/add [URL] [ОчікуванеСлово]</code>\nНаприклад: <code>/add https://google.com Google</code>", parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                return;
            }

            var url = parts[1];
            var keyword = parts[2];

            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                await _botClient!.SendMessage(chatId, "⚠️ URL має починатися з http:// або https://", cancellationToken: cancellationToken);
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IWebsiteRepository>();

            // створюємо об'єкт сайту та автоматично генеруємо для нього коротку назву з URL
            var website = new Website
            {
                Name = url.Replace("https://", "").Replace("http://", "").Split('/')[0], 
                Url = url,
                ExpectedKeyword = keyword,
                IsActive = true, 
                IsCurrentlyDown = false
            };

            await repository.AddWebsiteAsync(website);
            await repository.SaveChangesAsync();
            await _botClient!.SendMessage(chatId, $"✅ Ресурс <b>{website.Name}</b> успішно додано до системи!\nКлючове слово для перевірки: <i>{keyword}</i>", parseMode: ParseMode.Html, cancellationToken: cancellationToken);
        }

        // обробка команди зупинки моніторингу за ідентифікатором
        private async Task HandleDeleteCommandAsync(long chatId, string text, CancellationToken cancellationToken)
        {
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2 || !int.TryParse(parts[1], out int id))
            {
                await _botClient!.SendMessage(chatId, "⚠️ <b>Помилка формату.</b>\nВикористовуйте: <code>/delete [ID]</code>", parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IWebsiteRepository>();

            var website = await repository.GetWebsiteByIdAsync(id);
            if (website == null)
            {
                await _botClient!.SendMessage(chatId, $"⚠️ Ресурс з ID <b>{id}</b> не знайдено в базі даних.", parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                return;
            }

            // робимо soft delete (лише вимикаємо прапорець активності, не видаляючи історію)
            website.IsActive = false;
            website.IsCurrentlyDown = false; 
            await repository.UpdateWebsiteAsync(website);
            await repository.SaveChangesAsync();
            await _botClient!.SendMessage(chatId, $"🗑 Ресурс <b>{website.Name}</b> деактивовано (Soft Delete).\nМоніторинг зупинено, історія логів збережена.", parseMode: ParseMode.Html, cancellationToken: cancellationToken);
        }
    }
}