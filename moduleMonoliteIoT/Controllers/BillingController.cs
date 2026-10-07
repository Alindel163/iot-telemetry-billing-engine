using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using moduleMonoliteloT.Infrastructure;

namespace moduleMonoliteloT.Controllers;

[ApiController]
[Route("api/billing")] // Все запросы будут начинаться с /api/billing
public class BillingController : ControllerBase
{
    private readonly PlatformDbContext _dbContext;

    // Внедряем контекст БД PostgreSQL для чтения данных
    public BillingController(PlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Получить текущий баланс фермера.
    /// Вызов: GET /api/billing/balance?userId=farmer-john
    /// </summary>
    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance([FromQuery] string userId = "farmer-john")
    {
        // Ищем баланс пользователя в PostgreSQL
        var accountBalance = await _dbContext.Balances
            .AsNoTracking() // Оптимизация EF Core: только чтение, без кэширования изменений
            .FirstOrDefaultAsync(b => b.UserId == userId);

        if (accountBalance == null)
        {
            return NotFound(new { error = $"Кошелек для пользователя {userId} не найден." });
        }

        // Возвращаем красивый JSON с текущим остатком
        return Ok(new
        {
            UserId = accountBalance.UserId,
            CurrentBalanceBytes = accountBalance.Balance,
            Currency = "RUB",
            CheckedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Получить последние 10 списаний из книги Ledger для аналитики.
    /// Вызов: GET /api/billing/transactions
    /// </summary>
    [HttpGet("transactions")]
    public async Task<IActionResult> GetLatestTransactions()
    {
        var transactions = await _dbContext.Transactions
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAtUtc) // Сначала самые свежие
            .Take(10) // Берем только последние 10 штук
            .ToListAsync();

        return Ok(transactions);
    }
}
