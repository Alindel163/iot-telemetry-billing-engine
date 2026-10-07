A high-performance, resilient, and scalable backend service designed to handle real-time IoT device telemetry data ingestion and robust financial transaction billing. The system is architected as a Modular Monolith using modern .NET practices, featuring strict isolation of responsibilities, in-memory optimization, and database consistency constraints (ACID).

🏛️ Architecture & System Data Flow

1. Ingestion Layer Network handlers manage continuous WebSocket connections from thousands of IoT devices. To prevent Thread Pool Starvation, the handler performs lightning-fast deserialization (O(1) time complexity) and immediately passes the DTO to a non-blocking in-memory queue using System.Threading.Channels.
2. Background Processing An isolated background service (BackgroundService) continuously reads data packets from the channel in an asynchronous, non-blocking loop, distributing workloads across modules.
3. Anti-Fraud Module (DDoS Protection) Features a Sliding Window rate-limiting algorithm built on top of Redis Sorted Sets (ZSET). Requests are validated via atomic Redis pipelines. Malicious or broken devices that exceed configuration thresholds (e.g., 3 packets per 5 seconds) are dropped immediately at the cache level, shielding the persistent database.
4. Data Integrity & MitM Protection To prevent Man-in-the-Middle (MitM) attacks and packet tampering, every telemetry payload is cryptographically signed using HMAC-SHA256. The server recalculates and verifies the signature on the fly before pushing data down the pipeline.
5. Fintech Billing Ledger Charges users based on a per-packet pricing model. Writing directly to a disk-based database for millions of concurrent requests creates severe IO bottlenecks. To solve this, the engine uses Highload Batching financial transactions are accumulated in a thread-safe ConcurrentQueue, and a background scheduler flushes them to PostgreSQL every 5 seconds as a bulk insert.
6. Consistency & Idempotency The entire batch operation is wrapped in a strict PostgreSQL ACID transaction. At the schema level, a unique database constraint is enforced on the IdempotencyKey (a composite string of DeviceId + Timestamp). This guarantees absolute protection against double-spendingduplicate billing during network retries.
7. Proactive Alerting Module The system actively monitors operational anomalies. If a device's battery drops below 20% or a client's balance falls below a critical threshold, it triggers real-time events, which are written into structured rolling log files using Serilog.

🛠️ Tech Stack

• Platform C# (.NET 8.09.0), ASP.NET Core Web API, WebSockets.
• Concurrency & Concurrency Control System.Threading.Channels, AsyncAwait, PeriodicTimer, Concurrent Collections, Optimistic Concurrency.
• Cache  Hot State  Rate Limiting Redis (Docker) via StackExchange.Redis driver.
• Storage  Cold State  Ledger PostgreSQL (Docker) + Entity Framework Core (Code-First approach).
• Structured Logging Serilog (Rolling File Sink + Console output).
• Traffic Simulator A multi-threaded, self-contained workload generator (DeviceTrafficSimulator) utilizing Jitter (randomized time offsets) to flatten traffic spikes and replicate real-world environments.