using Microsoft.Extensions.Configuration;
using SessionLab.Services;

namespace SessionLab.Tests;

public class LabSettingsTests
{
    static LabSettings From(Dictionary<string, string?> values) =>
        LabSettings.From(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

    [Fact]
    public void Placeholder_values_count_as_not_configured()
    {
        var settings = From(new()
        {
            ["ANTHROPIC_API_KEY"] = "your_api_key",
            ["CLAUDE_MANAGED_AGENT_ID"] = "your_agent_id",
            ["CLAUDE_MANAGED_ENVIRONMENT_ID"] = "your_environment_id",
        });

        Assert.False(settings.HasApiKey);
        Assert.False(settings.IsConfigured);
    }

    [Fact]
    public void All_three_real_values_are_required()
    {
        var partial = From(new() { ["ANTHROPIC_API_KEY"] = "sk-ant-test", ["CLAUDE_MANAGED_AGENT_ID"] = "agent_1" });
        var complete = From(new()
        {
            ["ANTHROPIC_API_KEY"] = "sk-ant-test",
            ["CLAUDE_MANAGED_AGENT_ID"] = "agent_1",
            ["CLAUDE_MANAGED_ENVIRONMENT_ID"] = "env_1",
        });

        Assert.True(partial.HasApiKey);
        Assert.False(partial.IsConfigured);
        Assert.True(complete.IsConfigured);
    }
}
