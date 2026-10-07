using moduleMonoliteloT.Modules.Ingestion.Channels;
using moduleMonoliteloT.Modules.AntiFraud;
using moduleMonoliteloT.Modules.Billing;
using moduleMonoliteloT.Modules.Alerting; // Подключаем алерты

namespace moduleMonoliteloT.Modules.Ingestion.Background;

public class TelemetryProcessor : BackgroundService
{
    private readonly TelemetryChannel _telemetryChannel;
    private readonly AntiFraudService _antiFraudService;
    private readonly AlertingService _alertingService; // Поле для алертов
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TelemetryProcessor> _logger;

    public TelemetryProcessor(
        TelemetryChannel telemetryChannel,
        AntiFraudService antiFraudService,
        AlertingService alertingService, // Добавили в конструктор
        IServiceProvider serviceProvider,
        ILogger<TelemetryProcessor> logger)
    {
        _telemetryChannel = telemetryChannel;
        _antiFraudService = antiFraudService;
        _alertingService = alertingService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Telemetry Processor successfully STARTED and waiting for data...");

        try
        {
            await foreach (var payload in _telemetryChannel.ReadAllAsync(stoppingToken))
            {
                try
                {
                    // Step 1: Anti-Fraud validation (Redis)
                    bool isAllowed = await _antiFraudService.IsRequestAllowedAsync(payload);
                    if (!isAllowed) continue; // Drop packet if device is spamming

                    // Step 2: Hardware Health Check
                    if (payload.BatteryLevel < 20)
                    {
                        _alertingService.AlertLowBattery(payload.DeviceId, payload.BatteryLevel);
                    }

                    // Step 3: Transactional Billing (Redis Buffer)
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var billingEngine = scope.ServiceProvider.GetRequiredService<BillingEngine>();
                        await billingEngine.ProcessBillingAsync(payload);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Critical error processing telemetry packet for Device [{DeviceId}]", payload.DeviceId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Background Telemetry Processor is STOPPING (cancellation requested).");
        }
    }

}
