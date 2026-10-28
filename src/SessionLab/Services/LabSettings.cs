namespace SessionLab.Services;

/// <summary>Configuration read from user-secrets, environment variables, or appsettings.</summary>
public sealed record LabSettings(string? ApiKey, string? AgentId, string? EnvironmentId)
{
    public static LabSettings From(IConfiguration configuration) => new(
        Value(configuration["ANTHROPIC_API_KEY"]),
        Value(configuration["CLAUDE_MANAGED_AGENT_ID"]),
        Value(configuration["CLAUDE_MANAGED_ENVIRONMENT_ID"]));

    public bool HasApiKey => ApiKey is not null;

    public bool IsConfigured => ApiKey is not null && AgentId is not null && EnvironmentId is not null;

    // Placeholder values from the examples count as "not configured".
    static string? Value(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.StartsWith("your_", StringComparison.Ordinal)
            ? null
            : value.Trim();
}
