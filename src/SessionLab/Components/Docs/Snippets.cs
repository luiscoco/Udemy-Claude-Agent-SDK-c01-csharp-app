namespace SessionLab.Components.Docs;

/// <summary>A verbatim excerpt of this app's source, shown on the guide and architecture pages.</summary>
public sealed record Snippet(string File, string Caption, string Code, string Explanation, string Language = "csharp");

/// <summary>
/// Every excerpt the student pages display. SnippetTests fails if an excerpt no longer
/// appears in its source file, so the documentation cannot silently drift from the code.
/// </summary>
public static class Snippets
{
    public static readonly Snippet SetupDispatch = new(
        "src/SessionLab/Program.cs", "run a setup command instead of the server",
        """
        var command = args.FirstOrDefault(SetupCommands.Names.Contains);
        var builder = WebApplication.CreateBuilder(args.Where(arg => arg != command).ToArray());
        var settings = LabSettings.From(builder.Configuration);
        if (command is not null)
            return await SetupCommands.RunAsync(command, settings);
        """,
        "The same configuration pipeline (user-secrets, environment variables, appsettings) serves the web app and the setup commands. A recognised command name runs once and exits instead of starting the server.");

    public static readonly Snippet SetupKeyCheck = new(
        "src/SessionLab/Setup/SetupCommands.cs", "shared setup pattern",
        """
        if (!settings.HasApiKey)
        {
            Console.Error.WriteLine("Set ANTHROPIC_API_KEY with dotnet user-secrets before running this command.");
            return 1;
        }
        AnthropicClient client = new() { ApiKey = settings.ApiKey! };
        """,
        "Each setup command checks for a real API key, then constructs the SDK client. The key comes from the server's configuration and never appears in browser code.");

    public static readonly Snippet CreateAgent = new(
        "src/SessionLab/Setup/SetupCommands.cs", "create and report the agent ID",
        """
        var agent = await client.Beta.Agents.Create(new AgentCreateParams
        {
            Name = "Session Lab Agent",
            Model = BetaManagedAgentsModel.ClaudeSonnet4_6,
            System = "You are a helpful assistant.",
            Tools = [new BetaManagedAgentsAgentToolset20260401Params()],
        });
        Console.WriteLine("CLAUDE_MANAGED_AGENT_ID=" + agent.ID);
        """,
        "Name identifies the configuration, Model selects its model, System supplies instructions, and the toolset enables the built-in tools. Agents.Create returns the new agent ID for later session creation. This call does not send a user message.");

    public static readonly Snippet CreateEnvironment = new(
        "src/SessionLab/Setup/SetupCommands.cs", "create and report the environment ID",
        """
        var environment = await client.Beta.Environments.Create(new EnvironmentCreateParams
        {
            Name = "Session Lab Environment",
            Config = new BetaCloudConfigParams
            {
                Networking = new BetaLimitedNetworkParams { AllowPackageManagers = true },
            },
        });
        Console.WriteLine("CLAUDE_MANAGED_ENVIRONMENT_ID=" + environment.ID);
        """,
        "The cloud configuration selects limited networking while enabling its package-manager exception. Environments.Create returns a reusable environment ID. Creating this record is separate from creating and observing a session.");

    public static readonly Snippet ClientRegistration = new(
        "src/SessionLab/Program.cs", "register the server-side services",
        """
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton(_ => new AnthropicClient { ApiKey = settings.ApiKey ?? "not-configured" });
        builder.Services.AddSingleton<SessionHub>();
        """,
        "One AnthropicClient and one SessionHub are shared by every browser tab. Razor components receive the hub through dependency injection; none of them can read the API key.");

    public static readonly Snippet HomeCreate = new(
        "src/SessionLab/Components/Pages/Home.razor", "New session button",
        """
        var sessionId = await Hub.CreateSessionAsync();
        await ConnectAsync(sessionId);
        """,
        "The click runs on the server inside the Blazor circuit. The component asks the hub to create the session, then connects its own view to the returned ID. The browser never calls Anthropic.");

    public static readonly Snippet HubCreate = new(
        "src/SessionLab/Services/SessionHub.cs", "create a managed session",
        """
        var session = await client.Beta.Sessions.Create(new SessionCreateParams
        {
            Agent = settings.AgentId!,
            EnvironmentID = settings.EnvironmentId!,
            Budget = new BetaManagedAgentsBudgetLimit
            {
                MaxListCost = new() { Amount = "50", Currency = "USD" },
            },
        });
        """,
        "The host selects the configured agent and environment. The budget amount is expressed in cents: \"50\" means 50¢ of USD list cost.");

    public static readonly Snippet OpenStreamThenHistory = new(
        "src/SessionLab/Services/SessionHub.cs", "OpenAsync(): open stream, then read history",
        """
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
        """,
        "WithRawResponse.StreamStreaming completes once the HTTP stream is open, so live events are buffered while history is paged in. If reading history fails, the host cancels and disposes this stream and surfaces the error.");

    public static readonly Snippet FeedEmit = new(
        "src/SessionLab/Services/SessionFeed.cs", "Emit(): deduplicate, retain, notify",
        """
        public void Emit(JsonElement evt)
        {
            lock (gate)
            {
                var id = EventId(evt);
                if (id is not null && !seen.Add(id)) return;
                events.Add(evt);
                foreach (var listener in listeners) listener(evt);
            }
        }
        """,
        "seen prevents repeated IDs from being forwarded twice. The events list supports replay to a newly connected page, and listeners are the open Home.razor components. These collections live only in host memory.");

    public static readonly Snippet AttachOrReport = new(
        "src/SessionLab/Services/SessionHub.cs", "prepare the reader before returning",
        """
        try
        {
            await AttachAsync(session.ID);
        }
        catch (Exception error)
        {
            throw new SessionAttachException(session.ID, error);
        }
        return session.ID;
        """,
        "The hub prepares the host reader before returning the new ID. If attachment fails after creation, the exception carries the created session ID rather than pretending creation never happened.");

    public static readonly Snippet ConnectSubscribe = new(
        "src/SessionLab/Components/Pages/Home.razor", "ConnectAsync(): replay and subscribe",
        """
        feed = await Hub.AttachAsync(sessionId);
        // Replay what the host already holds, then receive new events as they arrive.
        foreach (var evt in feed.Subscribe(OnEvent)) ShowEvent(evt);
        """,
        "This replaces the browser EventSource of the Node.js version. The component subscribes directly to the in-memory feed on the server; Blazor sends the resulting UI changes to the browser over its SignalR connection.");

    public static readonly Snippet FeedSubscribe = new(
        "src/SessionLab/Services/SessionFeed.cs", "Subscribe(): snapshot and listen atomically",
        """
        public IReadOnlyList<JsonElement> Subscribe(Action<JsonElement> listener)
        {
            lock (gate)
            {
                listeners.Add(listener);
                return events.ToArray();
            }
        }
        """,
        "Taking the snapshot and registering the listener under the same lock means no event can fall between replay and live delivery, and none is delivered twice.");

    public static readonly Snippet HomeSend = new(
        "src/SessionLab/Components/Pages/Home.razor", "validate and submit the message",
        """
        var text = message?.Trim();
        if (string.IsNullOrEmpty(text) || currentId is null) return;
        if (text.Length > 20000)
        {
            Notice("Enter a message of up to 20,000 characters.", true);
            return;
        }
        sending = true;
        try
        {
            await Hub.SendMessageAsync(currentId, text);
            message = "";
        }
        """,
        "The form's submit handler runs on the server. It rejects empty or oversized text before asking the provider to do work, and the current session ID keeps the prompt in the same conversation.");

    public static readonly Snippet HubSend = new(
        "src/SessionLab/Services/SessionHub.cs", "attach before sending user.message",
        """
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
        """,
        "AttachAsync reuses an existing host reader or opens one before sending. The SDK serializes the params object as a user.message event containing the trimmed prompt.");

    public static readonly Snippet LiveLoop = new(
        "src/SessionLab/Services/SessionHub.cs", "consume live provider events",
        """
        await foreach (var evt in stream.Enumerate(stop.Token))
            feed.Emit(ToJson(evt));
        feed.Emit(AppEvent("app.stream_closed", "Live stream closed. Connect again to refresh."));
        """,
        "A background task consumes the SDK stream and sends each event through the same Emit method used for history. When the stream ends, the page is told so it can stop offering controls.");

    public static readonly Snippet OnEvent = new(
        "src/SessionLab/Components/Pages/Home.razor", "marshal events onto the renderer",
        """
        void OnEvent(JsonElement evt) => _ = InvokeAsync(() =>
        {
            ShowEvent(evt);
            StateHasChanged();
        });
        """,
        "Events arrive on the stream's background thread. InvokeAsync queues the update on this component's renderer, and StateHasChanged sends the new feed entry to the browser.");

    public static readonly Snippet RenderText = new(
        "src/SessionLab/Components/Pages/Home.razor", "render message text",
        """
        @if (item.Text is not null)
        {
            <p>@item.Text</p>
        }
        """,
        "Razor HTML-encodes @item.Text, so returned text is displayed as text rather than interpreted as HTML.",
        "cshtml");

    public static readonly Snippet ReadIdle = new(
        "src/SessionLab/Components/Pages/Home.razor", "read an idle event",
        """
        if (type == "session.status_idle")
        {
            var stopReason = evt.TryGetProperty("stop_reason", out var sr) ? sr : default;
            reason = (stopReason.ValueKind == JsonValueKind.Object ? Str(stopReason, "type") : null) ?? "unknown";
            status = "Idle";
        """,
        "The displayed stop reason comes from the event. The following code maps that reason to an explanation; idle alone does not mean the requested business result is verified.");

    public static readonly Snippet RememberSession = new(
        "src/SessionLab/Components/Pages/Home.razor", "remember the connected session",
        """
        await JS.InvokeVoidAsync("sessionLab.setLastSession", sessionId);
        """,
        "ConnectAsync calls a small JavaScript helper through IJSRuntime, because sessionStorage exists only in the browser. The helper tolerates storage failures so the lab still works when storage is unavailable.");

    public static readonly Snippet RestoreSession = new(
        "src/SessionLab/Components/Pages/Home.razor", "restore the ID on return",
        """
        var previousSession = await JS.InvokeAsync<string?>("sessionLab.getLastSession");
        if (previousSession is not null && SessionIdPattern.IsMatch(previousSession))
        {
            sessionIdInput = previousSession;
            Notice("Your previous session ID is ready. Click Connect to view its events again.");
        }
        """,
        "After the first interactive render, the page fills the session field and asks the student to click Connect. It does not create a session, reconnect, or send a prompt automatically.");

    public static readonly Snippet ConnectButton = new(
        "src/SessionLab/Components/Pages/Home.razor", "explicitly reconnect",
        """
        <button id="connect" class="secondary" @onclick="() => ConnectAsync(sessionIdInput)">Connect</button>
        """,
        "The student chooses when to reconnect. ConnectAsync validates the ID and subscribes the page to the host feed shown in step 3.",
        "cshtml");

    public static readonly Snippet ReuseReader = new(
        "src/SessionLab/Services/SessionHub.cs", "reuse an existing reader",
        """
        public Task<SessionFeed> AttachAsync(string id)
        {
            var lazy = sessions.GetOrAdd(id, key => new Lazy<Task<SessionFeed>>(() => OpenAsync(key)));
            return AwaitOrForget(id, lazy);
        }
        """,
        "If the host still has a reader for this session, every caller shares it. Otherwise exactly one caller runs the stream-before-history sequence from step 2, even if two tabs connect at once.");

    public static readonly Snippet ChooseAction = new(
        "src/SessionLab/Components/Pages/Home.razor", "choose the matching response",
        """
        if (result == "custom")
        {
            if (string.IsNullOrWhiteSpace(toolResult))
            {
                Notice("Enter the custom tool result.", true);
                return;
            }
            await Hub.SendCustomToolResultAsync(currentId!, id, toolResult);
        }
        else
            await Hub.ConfirmToolAsync(currentId!, id, allow: result == "allow");
        """,
        "The button pressed chooses between supplying a custom tool result and answering a tool confirmation. Both reference the matching pending event ID.");

    public static readonly Snippet ConfirmTool = new(
        "src/SessionLab/Services/SessionHub.cs", "send user.tool_confirmation",
        """
        new BetaManagedAgentsUserToolConfirmationEventParams
        {
            ToolUseID = toolUseId,
            Result = allow ? "allow" : "deny",
        },
        """,
        "Allow and Deny become a tool confirmation that references tool_use_id. This is a permission decision.");

    public static readonly Snippet CustomResult = new(
        "src/SessionLab/Services/SessionHub.cs", "send user.custom_tool_result",
        """
        new BetaManagedAgentsUserCustomToolResultEventParams
        {
            CustomToolUseID = customToolUseId,
            Content = [new BetaManagedAgentsTextBlock { Text = text }],
        },
        """,
        "Send result supplies the output of a custom tool, referencing custom_tool_use_id. Supplying a result and granting approval are different operations.");

    public static readonly Snippet Interrupt = new(
        "src/SessionLab/Services/SessionHub.cs", "send the interrupt request",
        """
        public Task InterruptAsync(string id) =>
            client.Beta.Sessions.Events.Send(id, new EventSendParams
            {
                Events = [new BetaManagedAgentsUserInterruptEventParams()],
            });
        """,
        "Completing this call means the interrupt event was sent. The hub does not poll or confirm that the remote session has stopped.");

    public static readonly Snippet Retrieve = new(
        "src/SessionLab/Services/SessionHub.cs", "inspect the remote session",
        """
        public async Task<JsonElement> RetrieveAsync(string id) =>
            ToJson(await client.Beta.Sessions.Retrieve(id));
        """,
        "Inspect session retrieves the current provider record and adds it to the feed. Read its remote state, or wait for an appropriate status event, before concluding that work has stopped.");

    public static IEnumerable<Snippet> All =>
        typeof(Snippets).GetFields().Where(f => f.FieldType == typeof(Snippet)).Select(f => (Snippet)f.GetValue(null)!);
}
