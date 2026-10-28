using System.Text.Json;
using Anthropic;
using Anthropic.Models.Beta.Agents;
using Anthropic.Models.Beta.Environments;
using SessionLab.Services;

namespace SessionLab.Setup;

/// <summary>
/// One-off setup commands: <c>dotnet run -- create-agent</c>, <c>create-environment</c>,
/// and <c>create-pending-demo-agent</c>. Each run creates a new remote resource.
/// </summary>
public static class SetupCommands
{
    public static readonly string[] Names = ["create-agent", "create-environment", "create-pending-demo-agent"];

    public static async Task<int> RunAsync(string command, LabSettings settings)
    {
        if (!settings.HasApiKey)
        {
            Console.Error.WriteLine("Set ANTHROPIC_API_KEY with dotnet user-secrets before running this command.");
            return 1;
        }
        AnthropicClient client = new() { ApiKey = settings.ApiKey! };
        try
        {
            await (command switch
            {
                "create-agent" => CreateAgentAsync(client),
                "create-environment" => CreateEnvironmentAsync(client),
                _ => CreatePendingDemoAgentAsync(client),
            });
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Could not run {command}: {error.Message}");
            return 1;
        }
    }

    static async Task CreateAgentAsync(AnthropicClient client)
    {
        Console.WriteLine("Creating agent in your Anthropic workspace...");
        var agent = await client.Beta.Agents.Create(new AgentCreateParams
        {
            Name = "Session Lab Agent",
            Model = BetaManagedAgentsModel.ClaudeSonnet4_6,
            System = "You are a helpful assistant.",
            Tools = [new BetaManagedAgentsAgentToolset20260401Params()],
        });
        Console.WriteLine("CLAUDE_MANAGED_AGENT_ID=" + agent.ID);
        Console.WriteLine("Save it with: dotnet user-secrets set CLAUDE_MANAGED_AGENT_ID " + agent.ID);
    }

    static async Task CreateEnvironmentAsync(AnthropicClient client)
    {
        Console.WriteLine("Creating environment in your Anthropic workspace...");
        var environment = await client.Beta.Environments.Create(new EnvironmentCreateParams
        {
            Name = "Session Lab Environment",
            Config = new BetaCloudConfigParams
            {
                Networking = new BetaLimitedNetworkParams { AllowPackageManagers = true },
            },
        });
        Console.WriteLine("CLAUDE_MANAGED_ENVIRONMENT_ID=" + environment.ID);
        Console.WriteLine("Save it with: dotnet user-secrets set CLAUDE_MANAGED_ENVIRONMENT_ID " + environment.ID);
    }

    static async Task CreatePendingDemoAgentAsync(AnthropicClient client)
    {
        var agent = await client.Beta.Agents.Create(new AgentCreateParams
        {
            Name = "Session Lab Pending Action Demo",
            Model = BetaManagedAgentsModel.ClaudeSonnet4_6,
            System = "For requests about the demo fact, call get_demo_fact. For the approval demonstration, use the bash tool to run the requested harmless command. Explain the result to the user.",
            Tools =
            [
                new BetaManagedAgentsCustomToolParams
                {
                    Name = "get_demo_fact",
                    Description = "Ask the application for a demonstration fact about a topic.",
                    InputSchema = new BetaManagedAgentsCustomToolInputSchema
                    {
                        Properties = new Dictionary<string, JsonElement>
                        {
                            ["topic"] = JsonSerializer.SerializeToElement(new { type = "string", description = "The requested topic." }),
                        },
                        Required = ["topic"],
                    },
                },
                new BetaManagedAgentsAgentToolset20260401Params
                {
                    DefaultConfig = new BetaManagedAgentsAgentToolsetDefaultConfigParams
                    {
                        PermissionPolicy = new BetaManagedAgentsAlwaysAskPolicy(),
                    },
                },
            ],
        });
        Console.WriteLine("CLAUDE_MANAGED_AGENT_ID=" + agent.ID);
        Console.WriteLine("For the pending-action demo, save this ID with dotnet user-secrets and restart the server. Note your previous agent ID first.");
    }
}
