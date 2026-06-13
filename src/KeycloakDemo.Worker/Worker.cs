using System.Net;

namespace KeycloakDemo.Worker;

/// <summary>
/// Polls GET /orders on the API every 30 seconds using Client Credentials auth.
/// The Bearer token is fetched and cached automatically by
/// <c>AuthenticatedHttpClientHandler</c> — this worker never deals with token lifecycle.
/// </summary>
public sealed class OrderPollerWorker(
    IHttpClientFactory httpClientFactory,
    ILogger<OrderPollerWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OrderPollerWorker started. Polling every {Interval}s.", PollInterval.TotalSeconds);

        // Run immediately on startup, then wait between iterations.
        while (!stoppingToken.IsCancellationRequested)
        {
            await PollOrdersAsync(stoppingToken);

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Host is shutting down — exit the loop cleanly.
                break;
            }
        }

        logger.LogInformation("OrderPollerWorker stopped.");
    }

    private async Task PollOrdersAsync(CancellationToken ct)
    {
        try
        {
            // "api-client" is the named HttpClient registered in AddWorkerInfrastructure.
            // Its pipeline includes AuthenticatedHttpClientHandler which injects the
            // Bearer token obtained via Client Credentials before the request is sent.
            var client = httpClientFactory.CreateClient("api-client");

            using var response = await client.GetAsync("/orders", ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                logger.LogWarning("GET /orders → 401. Token may have been rejected — check client credentials.");
                return;
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                logger.LogWarning("GET /orders → 403. Service account lacks the required role (api-reader).");
                return;
            }

            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogInformation("GET /orders → {StatusCode}. Body: {Body}", (int)response.StatusCode, body);
        }
        catch (HttpRequestException ex)
        {
            // API unreachable — log and continue. The next iteration will retry.
            logger.LogError(ex, "Failed to reach the API at GET /orders. Will retry in {Interval}s.", PollInterval.TotalSeconds);
        }
        catch (OperationCanceledException)
        {
            // Propagate cancellation — do not log it as an error.
            throw;
        }
    }
}
