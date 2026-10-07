using System.Threading.Channels;
using moduleMonoliteloT.Domain;

namespace moduleMonoliteloT.Modules.Ingestion.Channels;

public class TelemetryChannel
{
    // Сам канал, который умеет хранить объекты TelemetryPayload
    private readonly Channel<TelemetryPayload> _channel;

    public TelemetryChannel()
    {
        // Настраиваем ограниченный канал (Bounded) емкостью 50 000 пакетов.
        // Если сервер перегружен и в очереди накопилось 50к пакетов, 
        // канал временно приостановит прием, чтобы у сервера не кончилась оперативная память.
        var options = new BoundedChannelOptions(50000)
        {
            FullMode = BoundedChannelFullMode.Wait, // Если очередь полная — ждем, пока освободится место
            SingleReader = true,                   // Читать из канала у нас будет только один фоновый воркер
            SingleWriter = false                   // А вот писать в канал будут сотни WebSocket-соединений одновременно
        };

        _channel = Channel.CreateBounded<TelemetryPayload>(options);
    }

    /// <summary>
    /// Метод для сетевых хендлеров. Они используют его, чтобы быстро «сбросить» пакет в очередь.
    /// </summary>
    public async ValueTask<bool> WriteAsync(TelemetryPayload payload, CancellationToken cancellationToken = default)
    {
        // Метод WaitToWriteAsync проверяет, есть ли место в канале без блокировки потока
        while (await _channel.Writer.WaitToWriteAsync(cancellationToken))
        {
            // Помещаем пакет в очередь
            if (_channel.Writer.TryWrite(payload))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Метод для фонового воркера. Он позволяет бесконечно вычитывать пакеты по мере их появления.
    /// </summary>
    public IAsyncEnumerable<TelemetryPayload> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
