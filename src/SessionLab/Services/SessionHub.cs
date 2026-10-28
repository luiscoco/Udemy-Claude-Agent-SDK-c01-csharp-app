using System.Collections.Concurrent;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Beta.Sessions;
using Anthropic.Models.Beta.Sessions.Events;

namespace SessionLab.Services;

/// <summary>
/// The host's only gateway to Managed Agents. Pages call these methods; the API key
/// and the Anthropic SDK client never leave the server process.
/// </summary>
public sealed class SessionHub(AnthropicClient client, LabSettings settings, ILogger<SessionHub> logger)
{
    readonly ConcurrentDictionary<string, Lazy<Task<SessionFeed>>> sessions = new();

    public bool IsConfigured => settings.IsConfigured;

    public async Task<string> CreateSessionAsync()
    {
        if (!settings.IsConfigured)
            throw new InvalidOperationException(
                "Configure ANTHROPIC_API_KEY, CLAUDE_MANAGED_AGENT_ID, and CLAUDE_MANAGED_ENVIRONMENT_ID with dotnet user-secrets.");
        var session = await client.Beta.Sessions.Create(new SessionCreateParams
        {
            Agent = settings.AgentId!,
            EnvironmentID = settings.EnvironmentId!,
            Budget = new BetaManagedAgentsBudgetLimit
            {
                MaxListCost = new() { Amount = "50", Currency = "USD" },
            },
        });
        try
        {
            await AttachAsync(session.ID);
        }
        catch (Exception error)
        {
            throw new SessionAttachException(session.ID, error);
        }
        return session.ID;
    }

    /// <summary>Reuses the host reader for a session, or opens the stream and then loads history.</summary>
    public Task<SessionFeed> AttachAsync(string id)
    {
        var lazy = sessions.GetOrAdd(id, key => new Lazy<Task<SessionFeed>>(() => OpenAsync(key)));
        return AwaitOrForget(id, lazy);
    }

    async Task<SessionFeed> AwaitOrForget(string id, Lazy<Task<SessionFeed>> lazy)
    {
        try
        {
            return await lazy.Value;
        }
        catch
        {
            sessions.TryRemove(new(id, lazy));
            throw;
        }
    }

    async Task<SessionFeed> OpenAsync(string id)
    {
        // Open first: events produced while history loads stay buffered in the open HTTP stream.
        var stop = new CancellationTokenSource();
        var stream = await client.Beta.Sessions.Events.WithRawResponse.StreamStreaming(id, cancellationToken: stop.Token);
        var feed = new SessionFeed(id);
        try
        {
            var history = await client.Beta.Sessions.Events.List(id);
            await foreach (var evt in history.Paginate())
                feed.Emit(ToJson(evt));
        }
        catch
        {
            stop.Cancel();
            stream.Dispose();
            throw;
        }
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var evt in stream.Enumerate(stop.Token))
                    feed.Emit(ToJson(evt));
                feed.Emit(AppEvent("app.stream_closed", "Live stream closed. Connect again to refresh."));
            }
            catch (Exception error)
            {
                logger.LogWarning(error, "Stream for {SessionId} failed", id);
                feed.Emit(AppEvent("app.stream_error", error.Message));
            }
            finally
            {
                sessions.TryRemove(id, out _);
                stream.Dispose();
            }
        });
        return feed;
    }

    public async Task<JsonElement> RetrieveAsync(string id) =>
        ToJson(await client.Beta.Sessions.Retrieve(id));

    public async Task SendMessageAsync(string id, string text)
    {
        await AttachAsync(id);
        await client.Beta.Sessions.Events.Send(id, new EventSendParams
        {
            Events =
            [
                new BetaManagedAgentsUserMessageEventParams
                {
                    Content = [new BetaManagedAgentsTextBlock { Text = text }],
                },
            ],
        });
    }

    public Task InterruptAsync(string id) =>
        client.Beta.Sessions.Events.Send(id, new EventSendParams
        {
            Events = [new BetaManagedAgentsUserInterruptEventParams()],
        });

    public Task ConfirmToolAsync(string id, string toolUseId, bool allow) =>
        client.Beta.Sessions.Events.Send(id, new EventSendParams
        {
            Events =
            [
                new BetaManagedAgentsUserToolConfirmationEventParams
                {
                    ToolUseID = toolUseId,
                    Result = allow ? "allow" : "deny",
                },
            ],
        });

    public Task SendCustomToolResultAsync(string id, string customToolUseId, string text) =>
        client.Beta.Sessions.Events.Send(id, new EventSendParams
        {
            Events =
            [
                new BetaManagedAgentsUserCustomToolResultEventParams
                {
                    CustomToolUseID = customToolUseId,
                    Content = [new BetaManagedAgentsTextBlock { Text = text }],
                },
            ],
        });

    // SDK models serialize to their wire JSON, so the page reads the same fields the API documents.
    static JsonElement ToJson<T>(T value) => JsonSerializer.SerializeToElement(value);

    static JsonElement AppEvent(string type, string message) =>
        JsonSerializer.SerializeToElement(new { type, message });
}

public sealed class SessionAttachException(string sessionId, Exception inner)
    : Exception($"Session {sessionId} was created, but its stream could not be opened: {inner.Message}", inner)
{
    public string SessionId { get; } = sessionId;
}
