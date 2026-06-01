using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.EntityFrameworkCore;
using WebsiteMonitorApi.Data;
using WebsiteMonitorApi.Repositories;
using WebsiteMonitorApi.Services;

// базовий об'єкт для налаштування веб-додатку
var builder = WebApplication.CreateBuilder(args);

// додаємо підтримку контролерів та налаштовуємо формат JSON 
// (щоб уникнути помилок із нескінченним зацикленням даних)
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// підключаємо Swagger для зручного перегляду та тестування API
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// підключаємося до бази даних MySQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// реєструємо основні класи репозиторії та сервіси, щоб система могла їх використовувати
builder.Services.AddScoped<IWebsiteRepository, WebsiteRepository>();
builder.Services.AddScoped<IMonitoringService, MonitoringService>();

// додаємо вбудований інструмент для відправки запитів до веб-сайтів (щоб перевіряти їх стан)
builder.Services.AddHttpClient();

// запускаємо telegram-бота як фоновий процес, який працюватиме постійно
builder.Services.AddHostedService<TelegramBotListener>();

// налаштування Hangfire для виконання завдань за розкладом 
// (всі дані про завдання зберігаємо прямо в оперативній пам'яті)
builder.Services.AddHangfire(configuration => configuration
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseMemoryStorage());

// запускаємо сервер Hangfire для обробки фонових черг
builder.Services.AddHangfireServer();

var app = builder.Build();

// автоматично застосовуємо міграції (створює таблиці в БД) при запуску програми
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// вмикаємо Swagger для середовища розробки та робочого сервера
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
// конфігурація доступу до панелі моніторингу Hangfire
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    // дозволяє доступ лише з localhost, блокуючи зовнішні підключення у Production
    Authorization = new[] { new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter() }
});

// створюємо завдання, яке кожну хвилину буде перевіряти всі активні сайти з бази даних
RecurringJob.AddOrUpdate<IMonitoringService>(
    "check-all-websites",
    service => service.CheckAllActiveWebsitesAsync(),
    Cron.Minutely);

app.MapControllers();
app.Run();