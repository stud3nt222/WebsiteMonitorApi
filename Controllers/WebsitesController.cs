using Microsoft.AspNetCore.Mvc;
using System;
using WebsiteMonitorApi.Models;
using WebsiteMonitorApi.Repositories;
using WebsiteMonitorApi.Services;

namespace WebsiteMonitorApi.Controllers
{
    // API-контролер для керування системою через зовнішні HTTP-запити
    [Route("api/[controller]")]
    [ApiController]
    public class WebsitesController : ControllerBase
    {
        private readonly IWebsiteRepository _repository;
        private readonly IMonitoringService _monitoringService;

        public WebsitesController(IWebsiteRepository repository, IMonitoringService monitoringService)
        {
            _repository = repository;
            _monitoringService = monitoringService;
        }

        // ендпоінт для отримання інформації про сайт за його ID
        [HttpGet("{id}")]
        public async Task<IActionResult> GetWebsiteById(int id)
        {
            var website = await _repository.GetWebsiteByIdAsync(id);
            if (website == null)
            {
                return NotFound(new { Message = $"Сайт з ID {id} не знайдено." });
            }
            return Ok(website);
        }

        // ендпоінт для перегляду історії перевірок конкретного сайту
        [HttpGet("{id}/logs")]
        public async Task<IActionResult> GetWebsiteLogs(int id)
        {
            var website = await _repository.GetWebsiteByIdAsync(id);
            if (website == null)
            {
                return NotFound(new { Message = $"Сайт з ID {id} не знайдено." });
            }
            var logs = await _repository.GetLogsByWebsiteIdAsync(id);
            return Ok(logs);
        }

        // ендпоінт для отримання списку всіх активних сайтів
        [HttpGet]
        public async Task<IActionResult> GetAllWebsites()
        {
            var websites = await _repository.GetAllActiveWebsitesAsync();
            return Ok(websites);
        }

        // ендпоінт для додавання нового сайту до системи моніторингу
        [HttpPost]
        public async Task<IActionResult> AddWebsite([FromBody] WebsiteCreateDto dto)
        {
            var website = new Website
            {
                Name = dto.Name,
                Url = dto.Url,
                ExpectedKeyword = dto.ExpectedKeyword,
                IsActive = true
            };

            await _repository.AddWebsiteAsync(website);
            await _repository.SaveChangesAsync();

            return Ok(new { Message = "Сайт успішно додано!", WebsiteId = website.Id });
        }

        // ендпоінт для ручного примусового запуску перевірки ресурсу поза розкладом
        [HttpPost("{id}/check")]
        public async Task<IActionResult> CheckWebsite(int id)
        {
            var website = await _repository.GetWebsiteByIdAsync(id);
            if (website == null)
            {
                return NotFound($"Сайт з ID {id} не знайдено.");
            }

            await _monitoringService.CheckWebsiteAsync(website);

            return Ok($"Перевірка для '{website.Name}' виконана! Подивіться логи в базі даних.");
        }

        // ендпоінт для зупинки моніторингу ресурсу (Soft Delete)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteWebsite(int id)
        {
            var website = await _repository.GetWebsiteByIdAsync(id);
            if (website == null)
            {
                return NotFound($"Сайт з ID {id} не знайдено.");
            }

            website.IsActive = false;
            await _repository.UpdateWebsiteAsync(website);
            await _repository.SaveChangesAsync();

            return Ok(new { Message = $"Моніторинг для сайту '{website.Name}' зупинено. Дані збережено в історії." });
        }

        // ендпоінт для повного редагування налаштувань ресурсу(URL, назви, ключових слів)
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateWebsite(int id, Website updatedWebsite)
        {
            if (id != updatedWebsite.Id)
            {
                return BadRequest("ID в URL не співпадає з ID в тілі запиту.");
            }

            var existingWebsite = await _repository.GetWebsiteByIdAsync(id);
            if (existingWebsite == null)
            {
                return NotFound($"Сайт з ID {id} не знайдено.");
            }
            existingWebsite.Name = updatedWebsite.Name;
            existingWebsite.Url = updatedWebsite.Url;
            existingWebsite.ExpectedKeyword = updatedWebsite.ExpectedKeyword;
            existingWebsite.IsActive = updatedWebsite.IsActive;
            await _repository.UpdateWebsiteAsync(existingWebsite);

            return NoContent();
        }
    }
}