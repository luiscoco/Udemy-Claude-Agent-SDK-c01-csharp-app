# Claude Managed Agents session lab (C# / Blazor)

This local app creates and observes Claude Managed Agents sessions. It streams session events, shows stop reasons, and lets you interrupt work or respond to tool actions. It is the .NET 10 / Blazor Server version of [`lesson-c01-app`](../lesson-c01-app), built with the official [Anthropic C# SDK](https://github.com/anthropics/anthropic-sdk-csharp) ([`Anthropic` 13.3.0 on NuGet](https://www.nuget.org/packages/Anthropic/13.3.0)).

The running app has three pages:

- `/` is the session lab.
- `/guide` has the learning goals and run instructions.
- `/architecture` has the UML sequence diagram and a code walkthrough.

More documentation:

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) explains how a request flows through the app.
- [docs/MIGRATION-FROM-NODE.md](docs/MIGRATION-FROM-NODE.md) maps each part of the Node.js app to its C# equivalent.

## Before you start

You need:

- The **.NET 10 SDK**. Run `dotnet --list-sdks` in a terminal; one line must start with `10.`.
- An Anthropic API key for a workspace with access to Managed Agents.
- The IDs of a managed agent and an environment in that workspace. You can reuse existing ones or create them with the commands below.

Run every command from this folder, the one that contains `SessionLab.slnx`.

## Project layout

```text
lesson-c01-csharp-app/
├── SessionLab.slnx              solution (app + tests)
├── nuget.config                 restores only from nuget.org
├── src/SessionLab/              Blazor Server web app (net10.0)
│   ├── Program.cs               host setup + setup-command dispatch
│   ├── Services/
│   │   ├── LabSettings.cs       reads the API key and resource IDs
│   │   ├── SessionHub.cs        all Anthropic SDK calls (create, stream, send, retrieve)
│   │   └── SessionFeed.cs       in-memory, deduplicated event buffer per session
│   ├── Setup/SetupCommands.cs   create-agent / create-environment / create-pending-demo-agent
│   ├── Components/
│   │   ├── Pages/Home.razor     the interactive session lab
│   │   ├── Pages/Guide.razor    student guide
│   │   ├── Pages/Architecture.razor  UML diagram + code walkthrough
│   │   ├── Docs/Snippets.cs     code excerpts shown on the guide pages
│   │   └── Layout/              shared header and navigation
│   └── wwwroot/                 styles, images, diagram, small JS helpers
├── tests/SessionLab.Tests/      xUnit tests (feed, settings, snippet drift)
├── scripts/build-sequence-diagram.py  regenerates the UML SVG/PNG
└── docs/                        architecture and migration notes
```

## Configure your API key and IDs

This app stores its configuration as .NET **user secrets** instead of a `.env` file. User secrets live in your user profile, outside the project folder, so they can't be committed by accident.

```powershell
dotnet build
dotnet user-secrets set ANTHROPIC_API_KEY your_api_key --project src/SessionLab
dotnet user-secrets set CLAUDE_MANAGED_AGENT_ID your_agent_id --project src/SessionLab
dotnet user-secrets set CLAUDE_MANAGED_ENVIRONMENT_ID your_environment_id --project src/SessionLab
```

| Name | Value |
|---|---|
| `ANTHROPIC_API_KEY` | Your workspace API key. Set it before running any of the creation commands. |
| `CLAUDE_MANAGED_AGENT_ID` | An existing agent ID, or the ID printed by `create-agent`. |
| `CLAUDE_MANAGED_ENVIRONMENT_ID` | An existing environment ID, or the ID printed by `create-environment`. |

To see what's stored, run `dotnet user-secrets list --project src/SessionLab`. Environment variables with the same names also work, and they override user secrets. Restart the server after you change a value. Values that still start with `your_` count as missing.

## Create an agent and environment

After setting `ANTHROPIC_API_KEY`, create only the resources you're missing:

```powershell
dotnet run --project src/SessionLab -- create-agent
dotnet run --project src/SessionLab -- create-environment
```

Each command prints the new ID and the `dotnet user-secrets set ...` line that stores it.

- The agent uses `claude-sonnet-4-6` with the built-in agent toolset. If your workspace can't use that model, change it in `Setup/SetupCommands.cs`.
- The environment is a cloud environment with limited networking that allows package managers.

Each run creates a new remote resource in your Anthropic workspace, so save the IDs and reuse them. Those resources may be subject to your workspace's terms and charges. For more options, see Anthropic's [agent setup](https://platform.claude.com/docs/en/managed-agents/agent-setup) and [cloud environment setup](https://platform.claude.com/docs/en/managed-agents/environments) documentation.

## Run the app

```powershell
dotnet run --project src/SessionLab
```

When the terminal prints `Now listening on: http://127.0.0.1:3000`, open that address in your browser. To use a different port, add `--urls http://127.0.0.1:3001` to the command.

Click **New session**, type a message, and click **Send message**. The activity feed shows the agent's messages and the session events. To reconnect to an existing session, enter its ID and click **Connect**.

To stop the local server, press **Ctrl+C** in the terminal. Closing the browser or stopping the local server does **not** stop remote agent work. To request a stop, click **Interrupt work**, then wait for an idle event or use **Inspect session** to confirm the remote state.

## Session behavior

- New sessions have a 50-cent USD list-cost budget. Anthropic enforces the cap between model requests, so the final cost can go slightly over it.
- The host opens the live stream before sending any message. On reconnect it opens the stream first, then fetches the history, then removes duplicate events by ID.
- When the live stream ends, the page shows **Disconnected**. Click **Connect** to load the history and resume.
- A `requires_action` stop can include several event IDs. Inspect the event details before you allow or deny a tool call, or before you supply a result for a custom tool.
- This app never runs custom tools automatically.
- Check any artifacts yourself. An agent's message alone doesn't prove that a file was saved.

## Try the human-in-the-loop panel

The default agent has no custom tool and doesn't ask for confirmation, so an ordinary conversation may never use this panel. To try it, create a **separate** agent:

```powershell
dotnet run --project src/SessionLab -- create-pending-demo-agent
```

Before you continue, write down your current `CLAUDE_MANAGED_AGENT_ID`. Then store the new ID with `dotnet user-secrets set CLAUDE_MANAGED_AGENT_ID ...` and restart the server. Keep the same environment ID. To go back to the original agent, restore its ID and restart.

This demo agent defines `get_demo_fact`, a custom tool whose result the application must supply. It also sets the built-in toolset's permission policy to `always_ask`. These are two different mechanisms:

1. **Custom result.** Create a session and send `Use get_demo_fact to get a demo fact about Mars.` Wait for `requires_action`, then open the `agent.custom_tool_use` event and inspect it. The first pending ID is filled in automatically; if there are several, pick the matching one. Enter `Mars is known as the Red Planet.` in **Custom tool result** and click **Send result**. The app doesn't look up the fact; it sends the text you typed.
2. **Approval.** Create another session and send `Use the bash tool to run exactly: printf 'Hello from the approval demo'`. Wait for `requires_action`, inspect the `agent.tool_use` event and the command it asks to run, then click **Allow** or **Deny**. Only approve commands you have inspected.

In `SessionHub.cs`:

- **Send result** becomes a `user.custom_tool_result` event with `custom_tool_use_id` and your text.
- **Allow** and **Deny** become a `user.tool_confirmation` event with `tool_use_id` and the result you chose.

For details, see Anthropic's [events documentation](https://platform.claude.com/docs/en/managed-agents/events-and-streaming) and [permission policies](https://platform.claude.com/docs/en/managed-agents/permission-policies).

## Tests

```powershell
dotnet test
```

The tests cover three things:

- How the event feed removes duplicates and replays events.
- How the configuration handles placeholder values.
- That every code excerpt on the guide and architecture pages still appears word for word in the source. Change `SessionHub.cs` without updating `Components/Docs/Snippets.cs`, and the tests fail.

None of these tests calls the Anthropic API.

## Regenerate the diagram

The UML sequence diagram comes from `scripts/build-sequence-diagram.py`. It writes the SVG to both `src/SessionLab/wwwroot` and `docs/images`. Add `--png` to also render PNGs; that needs Python with `playwright` and Chromium installed.

```powershell
python scripts/build-sequence-diagram.py --png
```

## Troubleshooting

- **The page shows a configuration message.** Run `dotnet user-secrets list --project src/SessionLab`, check that all three values are set and aren't placeholders, then restart the server.
- **API or session error.** Check the API key, agent ID, environment ID, and your workspace's Managed Agents access. The error notice and the activity feed show what the API returned.
- **`dotnet build` hangs or asks for credentials.** A private NuGet feed in your machine-wide configuration is blocking the restore. This folder's `nuget.config` already limits restores to nuget.org; make sure you're running the command from this folder.
- **Port already in use.** Find the process that's listening:

  ```powershell
  netstat -ano | findstr :3000
  ```

  If it's a server you started in another terminal, stop it there with **Ctrl+C**. Otherwise, run on another port with `dotnet run --project src/SessionLab --urls http://127.0.0.1:3001`.

- **The page shows "Attempting to reconnect".** Blazor lost its connection to the server, usually because the server stopped. Start the server again and reload the page. Your remote session is unaffected.

The server listens only on `127.0.0.1` and is meant for a trusted local user, not for public deployment.

## What students learn

This app shows the agent loop and how work is split between the hosted session and your application. You create a session, send messages, watch live events, reconnect to the saved history, and respond to actions. Your host stays responsible for user authorization and business decisions.
