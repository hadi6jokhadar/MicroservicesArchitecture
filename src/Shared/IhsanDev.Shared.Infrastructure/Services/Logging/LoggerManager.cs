using System.Diagnostics.Metrics;
using System.Text;
using System.Threading.Channels;
using IhsanDev.Shared.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace IhsanDev.Shared.Infrastructure.Services.Logging;

/// <summary>
/// Custom logger manager implementation with file logging and console output.
///
/// Calling threads never touch the console or disk directly: LogInfo/LogWarn/LogDebug/LogError
/// just format a record and enqueue it (non-blocking, lock-free hand-off via a Channel) — a single
/// background task drains the queue and performs the actual Console.WriteLine + file append. This
/// is a singleton shared by the whole application (registered via AddSingleton, and called twice
/// per MediatR request by LoggingBehavior) — an earlier version took a lock and opened, wrote, and
/// closed a FileStream synchronously on the calling thread for every call, which serialized every
/// concurrent request in the process on that one lock and blocked the calling thread on disk I/O.
/// Under load that showed up as high p95 latency with low CPU and healthy DB/Redis (threads
/// waiting on the lock, not computing) — found via k6 load testing, see LOAD_TESTING_GUIDE.md.
///
/// Two BOUNDED channels, not one: Information/Debug go through a smaller channel that drops the
/// newest entry when full; Warning/Error/Critical go through a much larger channel that evicts the
/// oldest entry when full instead. Both are bounded — during a sustained incident (DB outage,
/// failing downstream dependency, bad deploy) a service can produce an Error per request at full
/// request rate for as long as the incident lasts, and an unbounded "priority" channel would let
/// the logger's own memory usage become part of the outage. The larger bound + evict-oldest policy
/// means an error storm sheds its oldest entries and keeps the newest (most relevant to an ongoing
/// incident) instead of growing without limit. The background writer always drains the priority
/// channel first.
///
/// Both channels use BoundedChannelFullMode.Wait, not the built-in Drop* modes, even though the
/// drop policy is "drop the newest"/"drop the oldest" — Channel&lt;T&gt;.Writer.TryWrite always
/// returns true under DropWrite/DropOldest/DropNewest, even when it silently discards the item, so
/// there is no way to detect (and therefore count) a drop from the return value with those modes.
/// Wait mode's TryWrite reliably returns false when full, so the drop/evict policy is implemented
/// manually below where the result is actually observable.
/// </summary>
public class LoggerManager : ILoggerManager, IAsyncDisposable, IDisposable
{
    // Information/Debug: high volume, scales directly with request rate. Bounded and small — if
    // the writer falls behind this far, newest entries are dropped (see EnqueueLowPriority).
    private const int LowPriorityChannelCapacity = 20_000;

    // Warning/Error/Critical: much larger bound. Sized to comfortably absorb an error-per-request
    // burst for a real incident's duration without either losing recent errors or growing memory
    // without limit. Full means evict-oldest, not drop-newest (see EnqueuePriority).
    private const int PriorityChannelCapacity = 100_000;

    private static readonly Meter Meter = new("IhsanDev.LoggerManager");
    private static readonly Counter<long> DroppedCounter = Meter.CreateCounter<long>(
        "logger.entries.dropped",
        description: "Log entries dropped/evicted because a channel was full. Tagged with channel=low_priority (Information/Debug, newest dropped) or channel=priority (Warning/Error/Critical, oldest evicted).");

    private readonly ILogger<LoggerManager> _logger;
    private readonly string _projectLogFilePath;
    private readonly Channel<LogEntry> _lowPriorityChannel;
    private readonly Channel<LogEntry> _priorityChannel;
    private readonly Task _writerTask;

    private long _lowPriorityDroppedCount;
    private long _priorityDroppedCount;
    private StreamWriter? _fileWriter;
    private string? _fileWriterDate;

    private readonly record struct LogEntry(
        DateTime TimestampUtc,
        LogLevel Level,
        string ContextualMessage,
        Exception? Exception,
        string? TraceId);

    public LoggerManager(ILogger<LoggerManager> logger, string projectLogFilePath)
    {
        _logger = logger;
        _projectLogFilePath = projectLogFilePath;

        if (!string.IsNullOrWhiteSpace(_projectLogFilePath))
        {
            Directory.CreateDirectory(_projectLogFilePath);
        }

        _lowPriorityChannel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(LowPriorityChannelCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait // see class remarks — Wait is what makes TryWrite's result observable
        });

        _priorityChannel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(PriorityChannelCapacity)
        {
            // SingleReader = false, unlike the low-priority channel below: EnqueuePriority's
            // evict-oldest path calls Reader.TryRead(out _) directly from whatever request
            // thread hit a full channel, concurrently with the background task's own TryRead
            // calls on the same reader. SingleReader = true would tell the channel only one
            // thread ever reads, which is false here and would be unsafe.
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        _writerTask = Task.Run(ProcessQueuesAsync);
    }

    public void LogInfo(string message, string? serviceName = null, string? traceId = null)
        => Enqueue(LogLevel.Information, message, serviceName, null, traceId);

    public void LogError(string message, string? serviceName = null, string? traceId = null)
        => Enqueue(LogLevel.Error, message, serviceName, null, traceId);

    public void LogError(Exception exception, string message, string? serviceName = null, string? traceId = null)
        => Enqueue(LogLevel.Error, $"{message} | Exception: {exception.Message}", serviceName, exception, traceId);

    public void LogDebug(string message, string? serviceName = null, string? traceId = null)
        => Enqueue(LogLevel.Debug, message, serviceName, null, traceId);

    public void LogWarn(string message, string? serviceName = null, string? traceId = null)
        => Enqueue(LogLevel.Warning, message, serviceName, null, traceId);

    private void Enqueue(LogLevel logLevel, string message, string? serviceName, Exception? exception, string? traceId)
    {
        // Skip formatting/allocation entirely for disabled levels (e.g. Debug when
        // Logging:LogLevel:Default is Information). Coarse-grained — this checks
        // LoggerManager's own category, not the original caller's — but costs nothing and
        // matches the granularity the previous implementation had (none).
        if (!_logger.IsEnabled(logLevel))
            return;

        var contextualMessage = serviceName != null ? $"[{serviceName}] {message}" : message;
        var entry = new LogEntry(DateTime.UtcNow, logLevel, contextualMessage, exception, traceId);

        if (logLevel >= LogLevel.Warning)
            EnqueuePriority(entry);
        else
            EnqueueLowPriority(entry);
    }

    private void EnqueueLowPriority(LogEntry entry)
    {
        if (_lowPriorityChannel.Writer.TryWrite(entry))
            return;

        // Full: drop the newest (the entry we were about to add) — Information/Debug backlog
        // isn't worth evicting older entries to make room for.
        var dropped = Interlocked.Increment(ref _lowPriorityDroppedCount);
        DroppedCounter.Add(1, new KeyValuePair<string, object?>("channel", "low_priority"));
        if (dropped % 1000 == 0)
        {
            // Bypass both channels for this warning — writing it through the same overloaded
            // pipeline would just add to the backlog it's reporting on.
            Console.Error.WriteLine($"[LoggerManager] WARNING: low-priority log channel full, dropped {dropped} Information/Debug entries so far");
        }
    }

    private void EnqueuePriority(LogEntry entry)
    {
        if (_priorityChannel.Writer.TryWrite(entry))
            return;

        // Full: evict the oldest queued entry to make room, then retry once. Under a genuine
        // sustained error storm this keeps the newest failures (most relevant to an ongoing
        // incident) and sheds old ones, with a hard ceiling on memory instead of none.
        _priorityChannel.Reader.TryRead(out _);
        var wrote = _priorityChannel.Writer.TryWrite(entry);

        // Count this as a drop regardless of whether the retry succeeded — an entry was evicted
        // either way (the one we just read out, or — if a concurrent reader beat us to the freed
        // slot and TryWrite still failed — this new one instead).
        var dropped = Interlocked.Increment(ref _priorityDroppedCount);
        DroppedCounter.Add(1, new KeyValuePair<string, object?>("channel", "priority"));

        // Every priority-channel drop is logged immediately, not sampled — the buffer is sized
        // large enough that this should be rare, and losing an Error/Warning/Critical entry is
        // significant enough to always surface directly.
        Console.Error.WriteLine($"[LoggerManager] WARNING: priority log channel full, evicted an entry to make room ({dropped} total priority-channel drops so far, wrote={wrote})");
    }

    private async Task ProcessQueuesAsync()
    {
        var priorityReader = _priorityChannel.Reader;
        var lowPriorityReader = _lowPriorityChannel.Reader;

        while (true)
        {
            var didWork = false;

            // Always fully drain whatever's waiting in the priority channel before touching
            // the low-priority one.
            while (priorityReader.TryRead(out var priorityEntry))
            {
                await ProcessEntryAsync(priorityEntry).ConfigureAwait(false);
                didWork = true;
            }

            if (lowPriorityReader.TryRead(out var lowPriorityEntry))
            {
                await ProcessEntryAsync(lowPriorityEntry).ConfigureAwait(false);
                didWork = true;
            }

            if (didWork)
                continue; // more may be waiting — re-check the priority channel first

            var priorityWait = priorityReader.WaitToReadAsync().AsTask();
            var lowPriorityWait = lowPriorityReader.WaitToReadAsync().AsTask();
            await Task.WhenAny(priorityWait, lowPriorityWait).ConfigureAwait(false);

            // Completion means the writer side called TryComplete() AND everything queued has
            // already been read out — safe to stop once both report done. (If one side completes
            // well before the other, this can spin a few extra times on an already-completed
            // WaitToReadAsync before the loop exits — bounded by the 5s dispose timeout, not worth
            // guarding further.)
            if (priorityReader.Completion.IsCompleted && lowPriorityReader.Completion.IsCompleted)
                break;
        }

        if (_fileWriter != null)
        {
            await _fileWriter.FlushAsync().ConfigureAwait(false);
            await _fileWriter.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task ProcessEntryAsync(LogEntry entry)
    {
        try
        {
            // Deliberately does NOT also call the standard ILogger<T> pipeline: no service
            // configures a logging exporter (ObservabilityExtensions.cs only wires up OpenTelemetry
            // tracing/metrics, not .WithLogging()), and no service clears or reconfigures the
            // default provider set, so ASP.NET Core's implicit default Console provider is still
            // active for every category — calling it here printed every line twice (once via this
            // class's colored writer below, once via the framework's default formatter).
            // WriteToConsole below is the only Console sink for these categories. If a real
            // ILogger-based sink is added later (an OTel logging exporter, for example),
            // reintroduce that call here on this background thread — never on the caller's — and
            // either silence the default Console provider for these categories or accept the
            // double output deliberately.
            WriteToConsole(entry);

            if (!string.IsNullOrWhiteSpace(_projectLogFilePath))
            {
                await WriteToFileAsync(entry).ConfigureAwait(false);
            }
        }
        catch
        {
            // Never let a logging failure take down the background writer.
        }
    }

    private static void WriteToConsole(LogEntry entry)
    {
        Console.ForegroundColor = entry.Level switch
        {
            LogLevel.Information => ConsoleColor.Green,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            LogLevel.Debug => ConsoleColor.Blue,
            LogLevel.Critical => ConsoleColor.Magenta,
            _ => ConsoleColor.White
        };

        Console.WriteLine($"{entry.TimestampUtc:yyyy-MM-dd HH:mm:ss} [{entry.Level}] {entry.ContextualMessage}");
        Console.ResetColor();
    }

    private async Task WriteToFileAsync(LogEntry entry)
    {
        var date = entry.TimestampUtc.ToString("yyyy-MM-dd");

        // Reopen only when the day rolls over — the common case is appending to an
        // already-open, buffered stream instead of a fresh open/write/close per line.
        if (_fileWriter == null || _fileWriterDate != date)
        {
            if (_fileWriter != null)
            {
                await _fileWriter.FlushAsync().ConfigureAwait(false);
                await _fileWriter.DisposeAsync().ConfigureAwait(false);
            }

            var filePath = Path.Combine(_projectLogFilePath, $"project-{date}.log");
            var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            _fileWriter = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = false };
            _fileWriterDate = date;
        }

        var traceIdPart = !string.IsNullOrWhiteSpace(entry.TraceId) ? $" | TraceId: {entry.TraceId}" : string.Empty;
        var logEntry = $"{entry.TimestampUtc:yyyy-MM-dd HH:mm:ss.fff} [{entry.Level}]{traceIdPart} {entry.ContextualMessage}";

        if (entry.Exception != null)
            logEntry += $"{Environment.NewLine}Exception: {entry.Exception}{Environment.NewLine}";

        await _fileWriter.WriteLineAsync(logEntry).ConfigureAwait(false);

        // Flush on Error/Warning/Critical so nothing important is lost if the process exits
        // unexpectedly; Information/Debug ride the batch and flush on the next reopen/shutdown.
        if (entry.Level is LogLevel.Error or LogLevel.Warning or LogLevel.Critical)
        {
            await _fileWriter.FlushAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _priorityChannel.Writer.TryComplete();
        _lowPriorityChannel.Writer.TryComplete();
        try
        {
            await _writerTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort drain on shutdown — don't block process exit indefinitely.
        }
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        // The DI container prefers DisposeAsync when a singleton implements IAsyncDisposable
        // (ASP.NET Core's root ServiceProvider does, on graceful shutdown). This sync path only
        // runs if something disposes the container synchronously instead.
        DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}
