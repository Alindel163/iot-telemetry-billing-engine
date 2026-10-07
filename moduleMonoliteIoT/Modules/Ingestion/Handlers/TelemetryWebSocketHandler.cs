using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using moduleMonoliteloT.Domain;
using moduleMonoliteloT.Modules.Ingestion.Channels;
using System.Security.Cryptography;

namespace moduleMonoliteloT.Modules.Ingestion.Handlers;

public class TelemetryWebSocketHandler
{
    private readonly TelemetryChannel _telemetryChannel;
    private readonly ILogger<TelemetryWebSocketHandler> _logger;

    // Внедряем наш синглтон-канал через конструктор
    public TelemetryWebSocketHandler(TelemetryChannel telemetryChannel, ILogger<TelemetryWebSocketHandler> logger)
    {
        _telemetryChannel = telemetryChannel;
        _logger = logger;
    }

    /// <summary>
    /// Главный метод, который удерживает WebSocket-соединение с устройством
    /// </summary>
    public async Task HandleConnectionAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        // Буфер для приема байтов из сети (4 Килобайта — более чем достаточно для нашей DTO)
        var buffer = new byte[1024 * 4];

        try
        {
            // Пока сокет открыт, крутимся в цикле и ждем пакеты
            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                // Ждем данные из сети. Этот вызов асинхронно приостанавливает поток, пока устройство молчит.
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                // Если устройство решило закрыть соединение
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Соединение закрыто устройством", cancellationToken);
                    break;
                }

                // Переводим массив байтов в строку UTF-8
                var jsonText = Encoding.UTF8.GetString(buffer, 0, result.Count);

                try
                {
                    // Десериализуем JSON-текст в наш C# объект record
                    var payload = JsonSerializer.Deserialize<TelemetryPayload>(jsonText, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true // Чтобы не бояться разного регистра букв в JSON
                    });

                    if (payload != null)
                    {
                        const string serverSecretKey = "FarmSecretKeyXYZ123";

                        // 🛡️ HMAC Signature Verification
                        if (!ValidateHmac(payload, serverSecretKey))
                        {
                            _logger.LogCritical("🚨 [SECURITY-ALERT] Data tampering detected! Packet from Device [{DevId}] has INVALID signature! Rejected.", payload.DeviceId);
                            continue; // Drop the malicious packet immediately before hitting memory queues
                        }

                        // If signature is valid, safely write to the memory channel
                        await _telemetryChannel.WriteAsync(payload, cancellationToken);
                    }
                }
                catch (JsonException)
                {
                    _logger.LogWarning("Пришел невалидный JSON пакет. Игнорируем.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при работе с WebSocket соединением");
        }
    }
    private bool ValidateHmac(TelemetryPayload payload, string secret)
    {
        // Восстанавливаем сырую строку точно в таком же формате, как её собирало устройство
        string rawData = $"{payload.DeviceId}|{payload.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{payload.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{payload.BatteryLevel}|{payload.Timestamp}";

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var messageBytes = Encoding.UTF8.GetBytes(rawData);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(messageBytes);
        string computedSignature = Convert.ToHexString(hashBytes).ToLower();

        // Сверяем подпись, которую прислало устройство, с тем, что вычислил сервер
        return payload.Signature == computedSignature;
    }
}
