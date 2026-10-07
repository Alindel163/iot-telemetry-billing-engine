using moduleMonoliteloT.Domain;
using moduleMonoliteloT.Infrastructure;

namespace moduleMonoliteloT.Modules.AntiFraud;

public class AntiFraudService
{
    private readonly IRateLimitCache _cache;
    private readonly ILogger<AntiFraudService> _logger;

    // Лимит: максимум 3 пакета в течение 5 секунд
    private const int MaxRequests = 3;
    private const int WindowSeconds = 5;

    public AntiFraudService(IRateLimitCache cache, ILogger<AntiFraudService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> IsRequestAllowedAsync(TelemetryPayload payload)
    {
        string cacheKey = $"ratelimit:device:{payload.DeviceId}";
        int currentRequestCount = await _cache.GetRequestCountInWindowAsync(cacheKey, payload.Timestamp, WindowSeconds);

        if (currentRequestCount > MaxRequests)
        {
            _logger.LogCritical("🚨 [ANTI-FRAUD] RATE LIMIT EXCEEDED! Device [{DevId}] is spamming! ({Count}/{Max} requests in {Sec}s)",
                payload.DeviceId, currentRequestCount, MaxRequests, WindowSeconds);
            return false;
        }

        return true;
    }
}
