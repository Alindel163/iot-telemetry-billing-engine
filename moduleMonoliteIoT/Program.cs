using Microsoft.EntityFrameworkCore;
using moduleMonoliteloT.Domain;
using moduleMonoliteloT.Infrastructure;
using moduleMonoliteloT.Modules.AntiFraud;
using moduleMonoliteloT.Modules.Ingestion.Background;
using moduleMonoliteloT.Modules.Ingestion.Channels;
using moduleMonoliteloT.Modules.Ingestion.Handlers;
using StackExchange.Redis;
using moduleMonoliteloT.Modules.Billing;
using Serilog;
using moduleMonoliteloT.Modules.Alerting;

var builder = WebApplication.CreateBuilder(args);

// Настраиваем Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/platform_log.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-day HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();


// Переключаем стандартный логгер .NET на Serilog
builder.Host.UseSerilog();

var connectionString = "Host=localhost;Port=5432;Database=iot_billing;Username=postgres;Password=root";

builder.Services.AddSingleton<TelemetryChannel>();

// 2. Регистрируем наш фоновый воркер, чтобы .NET сам запустил его при старте приложения.
builder.Services.AddHostedService<TelemetryProcessor>();
builder.Services.AddHostedService<BillingBatchScheduler>();
builder.Services.AddHostedService<DeviceTrafficSimulator>();
builder.Services.AddSingleton<IRateLimitCache, RedisRateLimitCache>();
builder.Services.AddSingleton<AntiFraudService>();
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect("localhost:6379"));
builder.Services.AddDbContext<PlatformDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddSingleton<BillingEngine>();
builder.Services.AddSingleton<AlertingService>();

// Регистрируем хендлер как Transient (создается заново на каждый запрос/подключение)
builder.Services.AddTransient<TelemetryWebSocketHandler>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

    // 1. Создаем богатого Джона
    if (!dbContext.Balances.Any(b => b.UserId == "farmer-john"))
    {
        dbContext.Balances.Add(new moduleMonoliteloT.Domain.AccountBalance
        {
            Id = Guid.NewGuid(),
            UserId = "farmer-john",
            Balance = 50000.00m, // У Джона много денег, алертов по нему не будет
            Version = 1
        });
    }

    // 2. Создаем бедного Боба
    if (!dbContext.Balances.Any(b => b.UserId == "farmer-bob"))
    {
        dbContext.Balances.Add(new moduleMonoliteloT.Domain.AccountBalance
        {
            Id = Guid.NewGuid(),
            UserId = "farmer-bob",
            Balance = 105.40m, // У Боба баланс сразу ниже 99.50, по нему мгновенно пойдут финансовые криты!
            Version = 1
        });
    }

    await dbContext.SaveChangesAsync();
}


app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(120) // Как часто проверять, живы ли устройства в сети
});

// 2. Пишем Middleware (прослойку), которая перехватывает путь /telemetry
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/telemetry")
    {
        // Проверяем, является ли запрос именно WebSocket-подключением
        if (context.WebSockets.IsWebSocketRequest)
        {
            // Запрашиваем у DI-контейнера наш хендлер
            var handler = context.RequestServices.GetRequiredService<TelemetryWebSocketHandler>();

            // Принимаем подключение (апгрейдим HTTP до WebSocket)
            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

            // Передаем управление хендлеру
            await handler.HandleConnectionAsync(webSocket, context.RequestAborted);
            return;
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Сюда нужно подключаться по протоколу WebSocket (ws://)!");
            return;
        }
    }

    // Если путь другой — идем дальше по конвейеру (к контроллерам или Swagger)
    await next(context);
});
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
