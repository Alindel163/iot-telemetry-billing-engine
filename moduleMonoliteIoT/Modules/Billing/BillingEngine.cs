using Microsoft.EntityFrameworkCore;
using moduleMonoliteloT.Domain;
using moduleMonoliteloT.Infrastructure;
using moduleMonoliteloT.Modules.Alerting;
using StackExchange.Redis;
using System.Collections.Concurrent;

namespace moduleMonoliteloT.Modules.Billing;

public class BillingEngine
{
    private readonly IDatabase _redisDb;
    private readonly ILogger<BillingEngine> _logger;

    // Временный буфер в оперативной памяти для накопления транзакций перед отправкой в Postgres
    private static readonly ConcurrentQueue<BillingTransaction> _transactionBuffer = new();

    private const decimal PacketCost = 0.01m;
   


    private const string RedisBalanceKey = "billing:account:farmer-john:balance";

    // Внедряем Redis через конструктор (Postgres здесь больше не нужен при каждом пакете!)
    public BillingEngine(IConnectionMultiplexer redis, ILogger<BillingEngine> logger)
    {
        _redisDb = redis.GetDatabase();
        _logger = logger;
    }

    /// <summary>
    /// Шаг 1: Быстрое списание баланса в Redis и постановка транзакции в очередь в памяти
    /// </summary>
    public async Task ProcessBillingAsync(TelemetryPayload payload)
    {
        string userId = (payload.DeviceId == "cow-004" || payload.DeviceId == "cow-005")
            ? "farmer-bob"
            : "farmer-john";

        string idempotencyKey = $"billing:{payload.DeviceId}:{payload.Timestamp}";
        string redisBalanceKey = $"billing:account:{userId}:balance";

        // Idempotency check in Redis
        string redisDupKey = $"billing:idempotency:{idempotencyKey}";
        if (await _redisDb.KeyExistsAsync(redisDupKey))
        {
            _logger.LogWarning("⚠️ [BILLING] Duplicate packet detected! Request dropped by idempotency key: {Key}", idempotencyKey);
            return;
        }

        // Atomically decrement balance in Redis memory cache
        double newBalance = await _redisDb.HashIncrementAsync(redisBalanceKey, "balance", -(double)PacketCost);
        await _redisDb.StringSetAsync(redisDupKey, "processed", TimeSpan.FromMinutes(10));

        var billingTx = new BillingTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceId = payload.DeviceId,
            Amount = PacketCost,
            CreatedAtUtc = DateTime.UtcNow,
            IdempotencyKey = idempotencyKey
        };

        _transactionBuffer.Enqueue(billingTx);

        _logger.LogInformation("⚡ [REDIS-BILLING] Balance updated in cache. [{User}] -> Device: {DevId}. Current cache balance: {Balance} RUB",
            userId, payload.DeviceId, Math.Round(newBalance, 2));
    }

    public static async Task FlushBatchToPostgresAsync(PlatformDbContext dbContext, IDatabase redisDb, IServiceProvider serviceProvider, ILogger logger)
    {
        if (_transactionBuffer.IsEmpty) return;

        var batch = new List<BillingTransaction>();
        while (_transactionBuffer.TryDequeue(out var tx))
        {
            batch.Add(tx);
        }

        logger.LogWarning("📥 [BATCH-ENGINE] Flushing a batch of {Count} transactions to PostgreSQL...", batch.Count);

        using var dbTransaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            dbContext.Transactions.AddRange(batch);

            var groupedTransactions = batch.GroupBy(t => t.UserId);

            foreach (var group in groupedTransactions)
            {
                string currentUserId = group.Key;
                decimal totalBatchAmount = group.Sum(t => t.Amount);

                var dbBalance = await dbContext.Balances.FirstOrDefaultAsync(b => b.UserId == currentUserId);
                if (dbBalance != null)
                {
                    dbBalance.Balance -= totalBatchAmount;

                    string redisBalanceKey = $"billing:account:{currentUserId}:balance";
                    await redisDb.HashSetAsync(redisBalanceKey, "balance", (double)dbBalance.Balance);

                    // Financial alert: Balance below critical threshold
                    if (dbBalance.Balance < 99.50m)
                    {
                        var alertingService = serviceProvider.GetRequiredService<moduleMonoliteloT.Modules.Alerting.AlertingService>();
                        alertingService.AlertLowBalance(currentUserId, dbBalance.Balance);
                    }
                }
            }

            await dbContext.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            logger.LogWarning("✅ [BATCH-ENGINE] Transaction batch successfully persisted to disk. DB balances updated.");
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            logger.LogError(ex, "🚨 [BATCH-ENGINE] Failed to save batch! Rolled back and data returned to buffer queue.");

            foreach (var tx in batch)
            {
                _transactionBuffer.Enqueue(tx);
            }
        }
    }

}
