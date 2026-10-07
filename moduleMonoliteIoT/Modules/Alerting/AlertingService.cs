namespace moduleMonoliteloT.Modules.Alerting;

public class AlertingService
{
    private readonly ILogger<AlertingService> _logger;

    public AlertingService(ILogger<AlertingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Фиксация критического разряда батареи конкретного устройства.
    /// </summary>
    public void AlertLowBattery(string deviceId, int batteryLevel)
    {
        _logger.LogWarning("⚠️ [ALERT-HARDWARE] Low battery detected! Device [{DevId}] is low on power. Current level: {Level}%! Battery replacement required.",
            deviceId, batteryLevel);
    }

    public void AlertLowBalance(string userId, decimal currentBalance)
    {
        _logger.LogCritical("🚨 [ALERT-FINANCE] CRITICAL BALANCE! User [{UserId}] is running out of funds. Remainder: {Balance} RUB. Risk of telemetry suspension!",
            userId, currentBalance);
    }
}
