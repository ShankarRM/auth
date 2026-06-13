using KeycloakDemo.Api.Endpoints;
using KeycloakDemo.Application;
using KeycloakDemo.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Services ───────────────────────────────────────────────────────────────────
// All auth wiring, DI registrations, and Keycloak config live inside these two
// extension methods. Program.cs knows WHAT is being composed, not HOW.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((doc, _, _) =>
    {
        doc.Info.Title       = "Keycloak Demo API";
        doc.Info.Version     = "v1";
        doc.Info.Description = "Part 3: Clean Architecture. Obtain a Bearer token via Keycloak.";

        doc.Components ??= new();
        doc.Components.SecuritySchemes ??= new Dictionary<string, Microsoft.OpenApi.Models.OpenApiSecurityScheme>();
        doc.Components.SecuritySchemes["Bearer"] = new()
        {
            Type         = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme       = "bearer",
            BearerFormat = "JWT",
            Description  = "Paste the access_token from Keycloak (without the 'Bearer ' prefix).",
        };

        doc.SecurityRequirements ??= [];
        doc.SecurityRequirements.Add(new()
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                []
            }
        });

        return Task.CompletedTask;
    });
});

// ── App ────────────────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title             = "Keycloak Demo API";
        options.Theme             = ScalarTheme.Purple;
        options.DefaultHttpClient = new(ScalarTarget.Http, ScalarClient.Http11);
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapOrderEndpoints();
app.MapAdminEndpoints();

app.Run();
