using StackExchange.Redis;

namespace moduleMonoliteloT.Infrastructure;

public class RedisRateLimitCache : IRateLimitCache
{
    private readonly IDatabase _redisDb;

    // Внедряем ConnectionMultiplexer (главный объект подключения к Redis) через конструктор
    public RedisRateLimitCache(IConnectionMultiplexer redis)
    {
        _redisDb = redis.GetDatabase();
    }

    public async Task<int> GetRequestCountInWindowAsync(string key, long nowSeconds, int windowSeconds)
    {
        long oldestAllowedTime = nowSeconds - windowSeconds;

        // Создаем атомарную транзакцию (Pipeline) в Redis
        var tran = _redisDb.CreateTransaction();

        // 1. Команда: Удалить из Sorted Set все метки времени, которые вылетели за левую границу окна (старее 5 секунд)
        _ = tran.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity, oldestAllowedTime);

        // 2. Команда: Добавить текущий timestamp пакета в этот Sorted Set
        _ = tran.SortedSetAddAsync(key, nowSeconds, nowSeconds);

        // 3. Команда: Посчитать, сколько элементов (пакетов) сейчас осталось в этом списке
        var countTask = tran.SortedSetLengthAsync(key);

        // 4. Команда: Продлить жизнь ключа (TTL), чтобы если корова потеряется, пустой ключ сам удалился через время и не забивал память
        _ = tran.KeyExpireAsync(key, TimeSpan.FromSeconds(windowSeconds * 2));

        // Отправляем всю пачку команд в Redis за ОДИН сетевой запрос ( Highload-оптимизация! )
        await tran.ExecuteAsync();

        // Возвращаем результат подсчета количества запросов
        return (int)await countTask;
    }
}
