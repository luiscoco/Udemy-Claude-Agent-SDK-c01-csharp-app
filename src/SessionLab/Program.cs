using Anthropic;
using SessionLab.Components;
using SessionLab.Services;
using SessionLab.Setup;

// "dotnet run -- create-agent" (and friends) run a setup command instead of the web server.
var command = args.FirstOrDefault(SetupCommands.Names.Contains);
var builder = WebApplication.CreateBuilder(args.Where(arg => arg != command).ToArray());
var settings = LabSettings.From(builder.Configuration);
if (command is not null)
    return await SetupCommands.RunAsync(command, settings);

// The SDK client lives only in the server process; the browser never sees the API key.
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(_ => new AnthropicClient { ApiKey = settings.ApiKey ?? "not-configured" });
builder.Services.AddSingleton<SessionHub>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
return 0;
