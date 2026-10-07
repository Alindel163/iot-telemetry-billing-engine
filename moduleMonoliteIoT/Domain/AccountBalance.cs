namespace moduleMonoliteloT.Domain;

public class AccountBalance
{
    // Уникальный ID записи
    public Guid Id { get; set; }

    // ID фермера / клиента, которому принадлежат устройства
    public string UserId { get; set; } = null!;

    // Баланс. В финтехе для денег ВСЕГДА используется тип decimal (он не теряет точность округления в отличие от double)
    public decimal Balance { get; set; }

    // Версия строки для оптимистичной блокировки (защита от одновременного изменения баланса из разных потоков)
    public int Version { get; set; }
}
