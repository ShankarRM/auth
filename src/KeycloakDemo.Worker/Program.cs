using KeycloakDemo.Infrastructure;
using KeycloakDemo.Worker;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        // AddWorkerInfrastructure registers:
        //   - KeycloakWorkerOptions (validated at startup)
        //   - KeycloakTokenService  (Singleton — caches the client-credentials token)
        //   - AuthenticatedHttpClientHandler (Transient — injects Bearer into outbound calls)
        //   - Named HttpClient "api-client" wired to the API base URL
        //   - ICurrentUser = ServiceAccountCurrentUser (Singleton — no HttpContext)
        services.AddWorkerInfrastructure(ctx.Configuration);

        // The background service that calls GET /orders every 30 seconds.
        services.AddHostedService<OrderPollerWorker>();
    })
    .Build();

await host.RunAsync();
