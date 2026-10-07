using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using moduleMonoliteloT.Domain;
using System.Security.Cryptography;

namespace moduleMonoliteloT.Modules.Ingestion.Background;

/// <summary>
/// Фоновый симулятор, который имитирует стадо из 5 «умных ошейников», 
/// подключается к нашему же серверу по сети и шлет данные.
/// </summary>
public class DeviceTrafficSimulator : BackgroundService
{
    private readonly ILogger<DeviceTrafficSimulator> _logger;

    public DeviceTrafficSimulator(ILogger<DeviceTrafficSimulator> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(3000, stoppingToken);
        _logger.LogInformation("[SIMULATOR] Launching virtual IoT device traffic generator...");

        var devicesCount = 5;
        var tasks = new List<Task>();

        for (int i = 1; i <= devicesCount; i++)
        {
            string deviceId = $"cow-{i:D3}";
            tasks.Add(SimulateDeviceAsync(deviceId, stoppingToken));
        }

        await Task.WhenAll(tasks);
    }

    private async Task SimulateDeviceAsync(string deviceId, CancellationToken ct)
    {
        var uri = new Uri("ws://localhost:5181/telemetry");
        var random = new Random();
        const string secretKey = "FarmSecretKeyXYZ123";

        while (!ct.IsCancellationRequested)
        {
            using var client = new ClientWebSocket();
            try
            {
                _logger.LogWarning("[SIMULATOR] Device [{DevId}] attempting network connection...", deviceId);
                await client.ConnectAsync(uri, ct);
                _logger.LogInformation("[SIMULATOR] Device [{DevId}] successfully CONNECTED!", deviceId);

                while (client.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var lat = 55.7558 + (random.NextDouble() - 0.5) * 0.01;
                    var lon = 37.6173 + (random.NextDouble() - 0.5) * 0.01;
                    var bat = random.Next(5, 25);
                    var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                    string rawData = $"{deviceId}|{lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{bat}|{ts}";
                    string signature = GenerateHmacSha256(rawData, secretKey);

                    var payload = new TelemetryPayload(deviceId, lat, lon, bat, ts, signature);
                    var json = JsonSerializer.Serialize(payload);
                    var bytes = Encoding.UTF8.GetBytes(json);

                    await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);

                    _logger.LogInformation("[SIMULATOR] >>> Device [{DevId}] SENT network packet.", deviceId);

                    int jitter = random.Next(0, 500);
                    await Task.Delay(1500 + jitter, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("[SIMULATOR] Connection error for Device [{DevId}]: {Message}. Retrying in 5 seconds...", deviceId, ex.Message);
                await Task.Delay(5000, ct);
            }
        }
    }

    // Вспомогательный метод генерации HMAC-SHA256
    private string GenerateHmacSha256(string message, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(messageBytes);

        // Переводим массив байт в красивую шестнадцатеричную строку (hex)
        return Convert.ToHexString(hashBytes).ToLower();
    }

}
