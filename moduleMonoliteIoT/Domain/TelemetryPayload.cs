namespace moduleMonoliteloT.Domain;

/// <summary>
/// Класс-контракт для входящих данных от IoT-устройства.
/// Мы используем 'record' вместо 'class', так как эти данные неизменяемы (immutable).
/// </summary>
public record TelemetryPayload(
    string DeviceId,      // Уникальный идентификатор ошейника/датчика (например, "cow-001")
    double Latitude,      // Широта (координаты для карты)
    double Longitude,     // Долгота
    int BatteryLevel,     // Заряд батареи устройства в процентах (0-100)
    long Timestamp,        // Время фиксации данных на самом устройстве (Unix Timestamp в секундах)
    string Signature
);
