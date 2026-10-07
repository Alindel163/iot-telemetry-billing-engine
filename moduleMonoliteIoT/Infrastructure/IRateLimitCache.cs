namespace moduleMonoliteloT.Infrastructure;

/// <summary>
/// Интерфейс для взаимодействия с быстрой памятью (Redis).
/// Описывает методы, необходимые для алгоритма Скользящего окна (Sliding Window).
/// </summary>
public interface IRateLimitCache
{
    /// <summary>
    /// Добавляет timestamp запроса в историю устройства в Redis и проверяет текущее количество запросов за окно.
    /// </summary>
    Task<int> GetRequestCountInWindowAsync(string key, long nowSeconds, int windowSeconds);
}
