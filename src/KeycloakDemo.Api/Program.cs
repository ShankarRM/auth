using KeycloakDemo.Api.Endpoints;
using KeycloakDemo.Api.Extensions;
using KeycloakDemo.Application;
using KeycloakDemo.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Services ───────────────────────────────────────────────────────────────────
// All auth wiring, DI registrations, and Keycloak config live inside these two
// extension methods. Program.cs knows WHAT is being composed, not HOW.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddKeycloakOpenApi();

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
app.MapIdentityEndpoints();
app.MapOrderEndpoints();
app.MapAdminEndpoints();

app.Run();

// Exposes Program to WebApplicationFactory<Program> in integration tests.
// Without this, the compiler-generated Program class is internal and unreachable
// from a test assembly that references this project.
public partial class Program { }
