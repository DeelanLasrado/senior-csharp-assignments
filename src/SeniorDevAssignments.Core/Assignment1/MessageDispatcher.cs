using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace SeniorDevAssignments.Core.Assignment1;

/// <summary>
/// Dispatches OMS events to N parallel consumers while guaranteeing
/// strict FIFO processing for messages that share the same OrderId.
///
/// Concurrency model (why this design):
/// 1. An unbounded Channel&lt;T&gt; simulates the message bus — producers write,
///    consumers read asynchronously without busy-waiting.
/// 2. Three long-lived worker tasks compete for messages → high throughput
///    across different orders.
/// 3. A ConcurrentDictionary&lt;int, OrderGate&gt; holds a per-OrderId SemaphoreSlim(1,1).
///    Before processing, a worker awaits that gate. Different OrderIds never
///    contend, so Order 2/3 still run in parallel with Order 1. Same OrderId
///    (Create then Cancel) is strictly serialised — Cancel cannot start until
///    Create releases the gate.
/// 4. This is preferred over static hash-partitioning (OrderId % N) because
///    partitions can become skewed, and over a global lock because that would
///    kill cross-order parallelism.
///
/// Create failure policy:
/// If "Create" throws, we mark the OrderId as failed in _failedOrders and still
/// release the gate. Subsequent Cancel/Update for that OrderId are skipped with
/// a clear log line instead of hitting the legacy system and causing a fatal error.
/// A real OMS would typically dead-letter or retry Create; we keep the demo focused.
/// </summary>
public sealed class MessageDispatcher : IAsyncDisposable
{
    private readonly Channel<OrderMessage> _queue;
    private readonly ConcurrentDictionary<int, OrderGate> _gates = new();
    private readonly ConcurrentDictionary<int, byte> _failedOrders = new();
    private readonly int _workerCount;
    private readonly List<string> _log = new();
    private readonly object _logLock = new();
    private readonly Random _random = new();

    public MessageDispatcher(int workerCount = 3)
    {
        if (workerCount < 1) throw new ArgumentOutOfRangeException(nameof(workerCount));
        _workerCount = workerCount;

        // Single-reader-safe unbounded channel stands in for RabbitMQ / Service Bus.
        _queue = Channel.CreateUnbounded<OrderMessage>(new UnboundedChannelOptions
        {
            SingleWriter = true,
            SingleReader = false // multiple workers compete for messages
        });
    }

    public IReadOnlyList<string> Log
    {
        get
        {
            lock (_logLock) return _log.ToList();
        }
    }

    public async Task EnqueueAsync(IEnumerable<OrderMessage> messages)
    {
        foreach (var message in messages)
        {
            await _queue.Writer.WriteAsync(message);
            WriteLog($"[ENQUEUE] {message}");
        }

        // Signal that no more messages will arrive so workers can drain and exit.
        _queue.Writer.Complete();
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var workers = Enumerable.Range(1, _workerCount)
            .Select(id => Task.Run(() => WorkerLoopAsync(id, cancellationToken), cancellationToken))
            .ToArray();

        await Task.WhenAll(workers);
        WriteLog("[DONE] All workers finished. Queue drained.");
    }

    private async Task WorkerLoopAsync(int workerId, CancellationToken cancellationToken)
    {
        await foreach (var message in _queue.Reader.ReadAllAsync(cancellationToken))
        {
            var gate = _gates.GetOrAdd(message.OrderId, _ => new OrderGate());

            // Acquire per-order gate BEFORE any side effects. This is what
            // forces Order 1 Cancel to wait for Order 1 Create even when
            // Worker A and Worker B pick them up "at the same time".
            WriteLog($"[Worker {workerId}] waiting for OrderId={message.OrderId} gate → {message.Event}");
            await gate.Lock.WaitAsync(cancellationToken);

            try
            {
                if (_failedOrders.ContainsKey(message.OrderId) &&
                    !string.Equals(message.Event, "Create", StringComparison.OrdinalIgnoreCase))
                {
                    WriteLog($"[Worker {workerId}] SKIP {message} — prior Create failed for this OrderId");
                    continue;
                }

                WriteLog($"[Worker {workerId}] START  {message}");
                var sw = Stopwatch.StartNew();

                try
                {
                    await ProcessAsync(message, cancellationToken);
                    sw.Stop();
                    WriteLog($"[Worker {workerId}] FINISH {message} in {sw.ElapsedMilliseconds} ms");
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    _failedOrders[message.OrderId] = 0;
                    WriteLog($"[Worker {workerId}] FAIL   {message} after {sw.ElapsedMilliseconds} ms — {ex.Message}");
                }
            }
            finally
            {
                // Always release so a failed Create cannot deadlock subsequent messages.
                gate.Lock.Release();
            }
        }

        WriteLog($"[Worker {workerId}] exited");
    }

    /// <summary>
    /// Simulates calling the legacy Inventory System. Duration is random (100–500 ms)
    /// so race windows are realistic under parallel workers.
    /// </summary>
    private async Task ProcessAsync(OrderMessage message, CancellationToken cancellationToken)
    {
        int delayMs;
        lock (_random)
        {
            delayMs = _random.Next(100, 501);
        }

        await Task.Delay(delayMs, cancellationToken);

        // Placeholder for the legacy Inventory System call:
        // Create → insert, Update → mutate, Cancel → delete (requires prior Create).
        // Sequential per-OrderId gating makes Cancel-after-Create safe.
    }

    private void WriteLog(string line)
    {
        var stamped = $"{DateTime.UtcNow:HH:mm:ss.fff} {line}";
        lock (_logLock)
        {
            _log.Add(stamped);
        }

        Console.WriteLine(stamped);
    }

    public ValueTask DisposeAsync()
    {
        foreach (var gate in _gates.Values)
            gate.Lock.Dispose();

        return ValueTask.CompletedTask;
    }

    /// <summary>Holds the mutual-exclusion primitive for one OrderId.</summary>
    private sealed class OrderGate
    {
        public SemaphoreSlim Lock { get; } = new(1, 1);
    }
}
