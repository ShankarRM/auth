namespace KeycloakDemo;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/users", () =>
        {
            var users = new[]
            {
                new { Id = "u1", Username = "alice", Roles = new[] { "api-admin"  } },
                new { Id = "u2", Username = "bob",   Roles = new[] { "api-reader" } },
            };

            return Results.Ok(users);
        })
        .RequireAuthorization(AuthorizationPolicies.AdminAccess)
        .WithName("GetAdminUsers")
        .WithTags("Admin")
        .WithSummary("List all users. Requires api-admin role.");

        app.MapGet("/audit/logs", () =>
        {
            var logs = new[]
            {
                new { Timestamp = "2024-01-15T10:00:00Z", Action = "USER_CREATED",  Actor = "alice" },
                new { Timestamp = "2024-01-15T11:23:00Z", Action = "ROLE_ASSIGNED", Actor = "alice" },
            };

            return Results.Ok(logs);
        })
        .RequireAuthorization(AuthorizationPolicies.AuditAccess)
        .WithName("GetAuditLogs")
        .WithTags("Audit")
        .WithSummary("List audit logs. Requires api-admin role AND audit.read scope.");

        return app;
    }
}
