using KeycloakDemo.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;

namespace KeycloakDemo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Transient: use cases hold no state between calls and are cheap to construct.
        services.AddTransient<GetOrdersUseCase>();
        return services;
    }
}
