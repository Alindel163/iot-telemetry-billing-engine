using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using moduleMonoliteloT.Infrastructure;

namespace moduleMonoliteloT.Modules.Billing;

/// <summary>
/// Фоновый сервис, который просыпается каждые 5 секунд и сбрасывает накопленный буфер транзакций в БД.
/// </summary>
public class BillingBatchScheduler : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDatabase _redisDb;
    private readonly ILogger<BillingBatchScheduler> _logger;

    public BillingBatchScheduler(IServiceProvider serviceProvider, IConnectionMultiplexer redis, ILogger<BillingBatchScheduler> logger)
    {
        _serviceProvider = serviceProvider;
        _redisDb = redis.GetDatabase();
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[BATCH-SCHEDULER] Billing batch scheduler successfully STARTED (Interval: 5s).");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

                await BillingEngine.FlushBatchToPostgresAsync(dbContext, _redisDb, _serviceProvider, _logger);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[BATCH-SCHEDULER] Stopping billing batch scheduler.");
        }
    }

}
