namespace moduleMonoliteloT.Domain;

public class BillingTransaction
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = null!;

    public string DeviceId { get; set; } = null!;

    // Сколько списали за конкретный пакет телеметрии (например, 0.01)
    public decimal Amount { get; set; }

    // Время проведения списания в базу данных
    public DateTime CreatedAtUtc { get; set; }

    // Ключ идемпотентности. Самая важная деталь!
    // Сюда мы будем записывать строку вида "msg:cow-001:1712345678" (ID устройства + timestamp пакета).
    // Если из-за сбоя сети устройство пришлет один и тот же пакет дважды, уникальный индекс в Postgres 
    // не позволит этой транзакции записаться повторно, защищая фермера от двойного списания.
    public string IdempotencyKey { get; set; } = null!;
}
