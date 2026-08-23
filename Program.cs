using System.Threading.Channels;

// MODELE

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


// URUCHOMIENIE PROGRAMU

using var cts = new CancellationTokenSource();

var progress = new Progress<MonitorProgress>(p =>
{
    Console.WriteLine(
        $"[PROGRESS] Cykl {p.Cycle}: " +
        $"{p.CurrentService} -> {p.Status}");
});

var monitor = new FintechMonitor();

try
{
    await monitor.RunAsync(
        maxCycles: 10,
        progress,
        cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Monitoring anulowany.");
}


// KLASA MONITORA

public class FintechMonitor
{
    private readonly ManualCircuitBreaker _bankBreaker =
        new(3, TimeSpan.FromSeconds(10));

    private readonly Channel<ServiceAlert> _alerts =
        Channel.CreateBounded<ServiceAlert>(50);

    public async Task RunAsync(
        int maxCycles,
        IProgress<MonitorProgress> progress,
        CancellationToken ct)
    {
        var consumer = Task.Run(async () =>
        {
            await foreach (var alert in
                _alerts.Reader.ReadAllAsync(ct))
            {
                Console.WriteLine(
                    $"[ALERT] {alert.ServiceName}: " +
                    $"{alert.Health} - {alert.Message}");
            }
        }, ct);

        try
        {
            for (int cycle = 1; cycle <= maxCycles; cycle++)
            {
                Console.WriteLine();
                Console.WriteLine($"=== CYKL {cycle} ===");

                using var timeoutCts =
                    CancellationTokenSource
                        .CreateLinkedTokenSource(ct);

                timeoutCts.CancelAfter(
                    TimeSpan.FromSeconds(5));

                var tasks = new[]
                {
                    CheckBankApiAsync(timeoutCts.Token),
                    CheckExchangeAsync(timeoutCts.Token),
                    CheckFraudAsync(timeoutCts.Token)
                };

                var remaining = tasks.ToList();

                while (remaining.Count > 0)
                {
                    var completed =
                        await Task.WhenAny(remaining);

                    remaining.Remove(completed);

                    var alert = await completed;

                    progress.Report(
                        new MonitorProgress(
                            cycle,
                            alert.ServiceName,
                            alert.Health));

                    await _alerts.Writer.WriteAsync(
                        alert,
                        ct);

                    if (alert.Health ==
                        ServiceHealth.Critical)
                    {
                        Console.WriteLine(
                            $"!!! KRYTYCZNY ALERT: " +
                            $"{alert.ServiceName}");

                        break;
                    }
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(1), ct);
            }
        }
        finally
        {
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
                    await Task.Delay(500, ct);

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

    private static async Task<ServiceAlert> CheckExchangeAsync(
        CancellationToken ct)
    {
        await Task.Delay(300, ct);

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

    private static async Task<ServiceAlert> CheckFraudAsync(
        CancellationToken ct)
    {
        await Task.Delay(400, ct);

        int value = Random.Shared.Next(100);

        var status =
            value < 70
                ? ServiceHealth.Healthy
                : value < 90
                    ? ServiceHealth.Degraded
                    : ServiceHealth.Critical;

        return new ServiceAlert(
            "Fraud System",
            status,
            status switch
            {
                ServiceHealth.Healthy =>
                    "System działa poprawnie",

                ServiceHealth.Degraded =>
                    "Wykryto podwyższone ryzyko",

                _ =>
                    "Wykryto krytyczne zagrożenie"
            },
            DateTime.Now);
    }
}