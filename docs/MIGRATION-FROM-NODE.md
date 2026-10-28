# Migrating from the Node.js app to C# / Blazor

This page maps each part of [`lesson-c01-app`](../../lesson-c01-app) (Node.js + `@anthropic-ai/sdk` 0.135 + vanilla JavaScript) to `lesson-c01-csharp-app` (.NET 10 + Blazor Server + `Anthropic` 13.3.0). The Node.js app is unchanged.

## Stack

| Concern | Node.js app | C# app |
|---|---|---|
| Runtime | Node.js 20+ | .NET 10 (`net10.0`) |
| Web host | `node:http` server in `server.js` | ASP.NET Core (`Program.cs`) |
| UI | `public/index.html` + `public/app.js` | `Components/Pages/Home.razor`, Interactive Server render mode |
| SDK | `@anthropic-ai/sdk` | [`Anthropic` 13.3.0](https://www.nuget.org/packages/Anthropic/13.3.0) |
| Configuration | `.env` + `dotenv` | `dotnet user-secrets` (environment variables also work) |
| Port | `PORT=3000` | `launchSettings.json` → `http://127.0.0.1:3000`, or `--urls` |
| Setup scripts | `npm run create-agent`, … | `dotnet run --project src/SessionLab -- create-agent`, … |
| Checks | `npm test` (`node --check`) | `dotnet test` (xUnit: logic + snippet drift) |
| Student pages | `public/guide.html`, `public/architecture.html` | `Pages/Guide.razor`, `Pages/Architecture.razor` |

## Architecture change

| Node.js | C# / Blazor | Why |
|---|---|---|
| `POST /api/sessions`, `/message`, `/interrupt`, `/action`, `GET /api/sessions/:id` | Direct method calls: `Hub.CreateSessionAsync()`, `SendMessageAsync`, `InterruptAsync`, `ConfirmToolAsync` / `SendCustomToolResultAsync`, `RetrieveAsync` | Component event handlers already run on the server, so no HTTP API is needed. |
| Browser `EventSource` → `GET /api/sessions/:id/stream` (SSE) | `feed.Subscribe(OnEvent)` + `StateHasChanged()`; Blazor pushes the changes over SignalR | Blazor Server keeps a live connection open already. |
| `sessions` `Map` + `attach(id)` | `ConcurrentDictionary<string, Lazy<Task<SessionFeed>>>` + `AttachAsync(id)` | Thread-safe, and two simultaneous connects share one stream. |
| `emit(state, event)` writing SSE frames | `SessionFeed.Emit` invoking listener delegates under a lock | Same deduplication, same replay buffer. |
| Replay buffered events to a new SSE response | `Subscribe` returns the buffered events and registers the listener inside one lock | No event is lost or duplicated between replay and live delivery. |
| `EventSource` auto-reconnect | Page shows **Disconnected**; the student clicks **Connect** | Reconnecting is an explicit, visible step. |
| `textContent` for message text | Razor `@item.Text` (HTML-encoded) | Agent output is never interpreted as HTML. |
| `sessionStorage` in `app.js` | `sessionLab.getLastSession` / `setLastSession` in `wwwroot/js/navigation.js`, called with `IJSRuntime` | Browser storage only exists in the browser. |

## SDK call mapping

| Node.js (`client.beta…`) | C# (`client.Beta…`) |
|---|---|
| `agents.create({ name, model, system, tools })` | `Agents.Create(new AgentCreateParams { Name, Model = BetaManagedAgentsModel.ClaudeSonnet4_6, System, Tools })` |
| `{ type: "agent_toolset_20260401" }` | `new BetaManagedAgentsAgentToolset20260401Params()` |
| `default_config: { permission_policy: { type: "always_ask" } }` | `DefaultConfig = new BetaManagedAgentsAgentToolsetDefaultConfigParams { PermissionPolicy = new BetaManagedAgentsAlwaysAskPolicy() }` |
| `{ type: "custom", name, description, input_schema }` | `new BetaManagedAgentsCustomToolParams { Name, Description, InputSchema = new BetaManagedAgentsCustomToolInputSchema { Properties, Required } }` |
| `environments.create({ name, config: { type: "cloud", networking: { type: "limited", allow_package_managers: true } } })` | `Environments.Create(new EnvironmentCreateParams { Name, Config = new BetaCloudConfigParams { Networking = new BetaLimitedNetworkParams { AllowPackageManagers = true } } })` |
| `sessions.create({ agent, environment_id, budget: { type: "limit", max_list_cost: { amount: "50", currency: "USD" } } })` | `Sessions.Create(new SessionCreateParams { Agent, EnvironmentID, Budget = new BetaManagedAgentsBudgetLimit { MaxListCost = new() { Amount = "50", Currency = "USD" } } })` |
| `sessions.retrieve(id)` | `Sessions.Retrieve(id)` |
| `await sessions.events.stream(id)` (resolves once the stream is open) | `await Sessions.Events.WithRawResponse.StreamStreaming(id)`, then `stream.Enumerate(token)` |
| `for await (… of sessions.events.list(id))` | `var page = await Sessions.Events.List(id); await foreach (… in page.Paginate())` |
| `stream.controller.abort()` | Cancel the `CancellationToken` and `Dispose()` the raw stream response |
| `events.send(id, { events: [{ type: "user.message", content: [{ type: "text", text }] }] })` | `Events.Send(id, new EventSendParams { Events = [new BetaManagedAgentsUserMessageEventParams { Content = [new BetaManagedAgentsTextBlock { Text = text }] }] })` |
| `{ type: "user.interrupt" }` | `new BetaManagedAgentsUserInterruptEventParams()` |
| `{ type: "user.tool_confirmation", tool_use_id, result }` | `new BetaManagedAgentsUserToolConfirmationEventParams { ToolUseID, Result = "allow" \| "deny" }` |
| `{ type: "user.custom_tool_result", custom_tool_use_id, content }` | `new BetaManagedAgentsUserCustomToolResultEventParams { CustomToolUseID, Content = [new BetaManagedAgentsTextBlock { Text }] }` |

C# property names are the PascalCase form of the wire field names (`environment_id` → `EnvironmentID`). Params types implicitly convert into the SDK's union types, so the `Events = [...]` and `Tools = [...]` lists need no wrappers.

### Why `WithRawResponse.StreamStreaming`?

In C#, `StreamStreaming(id)` returns an `IAsyncEnumerable`, and the HTTP request starts only when you begin enumerating it. So calling it alone doesn't open the stream before you read the history. Awaiting `WithRawResponse.StreamStreaming(id)` *does* return once the response headers arrive, which means the stream is open. That keeps the original app's "open the stream first, then read the history" rule, and it's why the code uses that call.

## Unchanged

- The page design and style sheet (`style.css`, `code-highlighting.css`, astronaut art, dark mode).
- The lesson's learning goals.
- The 50¢ session budget.
- Agent and environment settings (`claude-sonnet-4-6`, the agent toolset, limited networking with package managers).
- The pending-action demo agent.
- Every user-facing message and stop-reason explanation.
