using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

public enum ServiceHealth
{
    Healthy,
    Degraded,
    Critical
}

public record ServiceAlert(
    string ServiceName,
    ServiceHealth Health,
    string Message,
    DateTime Timestamp);

public record MonitorProgress(
    int Cycle,
    string CurrentService,
    ServiceHealth Status);

public class CircuitBrokenException : Exception
{
    public CircuitBrokenException(string message)
        : base(message)
    {
    }
}

public class ManualCircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _openDuration;

    private int _failures;
    private DateTime _openedAt;
    private CircuitState _state = CircuitState.Closed;

    private enum CircuitState
    {
        Closed,
        Open
    }

    public ManualCircuitBreaker(
        int failureThreshold,
        TimeSpan openDuration)
    {
        _failureThreshold = failureThreshold;
        _openDuration = openDuration;
    }

    public async Task<T> ExecuteAsync<T>(
        Func<Task<T>> action)
    {
        if (_state == CircuitState.Open)
        {
            if (DateTime.UtcNow - _openedAt >= _openDuration)
            {
                _state = CircuitState.Closed;
                _failures = 0;
            }
            else
            {
                throw new CircuitBrokenException(
                    "Circuit Breaker jest otwarty.");
            }
        }

        try
        {
            T result = await action();

            _failures = 0;
            _state = CircuitState.Closed;

            return result;
        }
        catch
        {
            _failures++;

            if (_failures >= _failureThreshold)
            {
                _state = CircuitState.Open;
                _openedAt = DateTime.UtcNow;
            }

            throw;
        }
    }
}

public class FintechMonitor
{
    private readonly ManualCircuitBreaker _bankBreaker =
        new(
            3,
            TimeSpan.FromSeconds(10));

    private readonly Channel<ServiceAlert> _alerts =
        Channel.CreateBounded<ServiceAlert>(50);

    public async Task RunAsync(
        int maxCycles,
        IProgress<MonitorProgress> progress,
        CancellationToken ct)
    {
        // Konsument kanału
        var consumer = Task.Run(async () =>
        {
            await foreach (
                var alert in _alerts.Reader.ReadAllAsync(ct))
            {
                Console.WriteLine(
                    $"[ALERT] {alert.ServiceName}: " +
                    $"{alert.Health} - {alert.Message}");
            }
        }, ct);

        try
        {
            for (
                int cycle = 1;
                cycle <= maxCycles;
                cycle++)
            {
                Console.WriteLine();
                Console.WriteLine(
                    $"=== CYKL {cycle} ===");

                using var timeoutCts =
                    CancellationTokenSource
                        .CreateLinkedTokenSource(ct);

                // Timeout całego cyklu: 5 sekund
                timeoutCts.CancelAfter(
                    TimeSpan.FromSeconds(5));

                // Trzy usługi uruchamiane równolegle
                var tasks = new[]
                {
                    CheckBankApiAsync(
                        timeoutCts.Token),

                    CheckExchangeAsync(
                        timeoutCts.Token),

                    CheckFraudAsync(
                        timeoutCts.Token)
                };

                var remaining = tasks.ToList();

                // Task.WhenAny pozwala reagować
                // na wynik usługi, która skończy pierwsza.
                while (remaining.Count > 0)
                {
                    var completed =
                        await Task.WhenAny(remaining);

                    remaining.Remove(completed);

                    var alert = await completed;

                    // Raportowanie postępu
                    progress.Report(
                        new MonitorProgress(
                            cycle,
                            alert.ServiceName,
                            alert.Health));

                    // Wysłanie alertu do kanału
                    await _alerts.Writer.WriteAsync(
                        alert,
                        ct);

                    // Natychmiastowa reakcja na Critical
                    if (alert.Health ==
                        ServiceHealth.Critical)
                    {
                        Console.WriteLine(
                            $"!!! KRYTYCZNY ALERT: " +
                            $"{alert.ServiceName}");

                        break;
                    }
                }

                // Przerwa pomiędzy cyklami
                await Task.Delay(
                    TimeSpan.FromSeconds(1),
                    ct);
            }
        }
        finally
        {
            // Zamknięcie kanału
            _alerts.Writer.Complete();

            try
            {
                await consumer;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task<ServiceAlert> CheckBankApiAsync(
        CancellationToken ct)
    {
        try
        {
            return await _bankBreaker.ExecuteAsync(
                async () =>
                {
                    await Task.Delay(
                        500,
                        ct);

                    // 40% szans na awarię
                    if (Random.Shared.Next(100) < 40)
                    {
                        throw new Exception(
                            "API banku niedostępne");
                    }

                    return new ServiceAlert(
                        "Bank API",
                        ServiceHealth.Healthy,
                        "API działa poprawnie",
                        DateTime.Now);
                });
        }
        catch (CircuitBrokenException)
        {
            return new ServiceAlert(
                "Bank API",
                ServiceHealth.Degraded,
                "Circuit Breaker jest otwarty",
                DateTime.Now);
        }
        catch (Exception ex)
        {
            return new ServiceAlert(
                "Bank API",
                ServiceHealth.Degraded,
                ex.Message,
                DateTime.Now);
        }
    }

    private static async Task<ServiceAlert>
        CheckExchangeAsync(
            CancellationToken ct)
    {
        await Task.Delay(
            300,
            ct);

        // 80% Healthy, 20% Degraded
        var healthy =
            Random.Shared.Next(100) < 80;

        return new ServiceAlert(
            "Exchange API",
            healthy
                ? ServiceHealth.Healthy
                : ServiceHealth.Degraded,

            healthy
                ? "Kursy walut dostępne"
                : "Opóźniona odpowiedź",

            DateTime.Now);
    }

    private static async Task<ServiceAlert>
        CheckFraudAsync(
            CancellationToken ct)
    {
        await Task.Delay(
            400,
            ct);

        int value =
            Random.Shared.Next(100);

        // 70% Healthy
        // 20% Degraded
        // 10% Critical
        var status =
            value < 70
                ? ServiceHealth.Healthy
                : value < 90
                    ? ServiceHealth.Degraded
                    : ServiceHealth.Critical;

        string message =
            status switch
            {
                ServiceHealth.Healthy =>
                    "System działa poprawnie",

                ServiceHealth.Degraded =>
                    "Wykryto podwyższone ryzyko",

                _ =>
                    "Wykryto krytyczne zagrożenie"
            };

        return new ServiceAlert(
            "Fraud System",
            status,
            message,
            DateTime.Now);
    }
}

public class Program
{
    public static async Task Main()
    {
        using var cts =
            new CancellationTokenSource();

        var progress =
            new Progress<MonitorProgress>(p =>
            {
                Console.WriteLine(
                    $"[PROGRESS] " +
                    $"Cykl {p.Cycle}: " +
                    $"{p.CurrentService} -> " +
                    $"{p.Status}");
            });

        var monitor =
            new FintechMonitor();

        try
        {
            await monitor.RunAsync(
                maxCycles: 10,
                progress,
                cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine(
                "Monitoring anulowany.");
        }
    }
}