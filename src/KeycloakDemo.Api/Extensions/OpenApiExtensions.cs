using Microsoft.OpenApi.Models;

namespace KeycloakDemo.Api.Extensions;

internal static class OpenApiExtensions
{
    internal static IServiceCollection AddKeycloakOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((doc, _, _) =>
            {
                doc.Info.Title       = "Keycloak Demo API";
                doc.Info.Version     = "v1";
                doc.Info.Description = "Part 3: Clean Architecture. Obtain a Bearer token via Keycloak.";

                doc.Components ??= new();
                doc.Components.SecuritySchemes ??= new Dictionary<string, OpenApiSecurityScheme>();
                doc.Components.SecuritySchemes["Bearer"] = new()
                {
                    Type         = SecuritySchemeType.Http,
                    Scheme       = "bearer",
                    BearerFormat = "JWT",
                    Description  = "Paste the access_token from Keycloak (without the 'Bearer ' prefix).",
                };

                doc.SecurityRequirements ??= [];
                doc.SecurityRequirements.Add(new()
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new() { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                        },
                        []
                    }
                });

                return Task.CompletedTask;
            });
        });

        return services;
    }
}
