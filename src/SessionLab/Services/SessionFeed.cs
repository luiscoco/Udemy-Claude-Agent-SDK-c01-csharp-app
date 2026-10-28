using System.Text.Json;

namespace SessionLab.Services;

/// <summary>
/// One session's events held in host memory: deduplicated history plus live events,
/// replayed to each page that subscribes. This buffer is not a durable database.
/// </summary>
public sealed class SessionFeed
{
    readonly object gate = new();
    readonly List<JsonElement> events = [];
    readonly HashSet<string> seen = [];
    readonly List<Action<JsonElement>> listeners = [];

    public string SessionId { get; }

    public SessionFeed(string sessionId) => SessionId = sessionId;

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

    /// <summary>Returns the buffered events and registers the listener for new ones, atomically.</summary>
    public IReadOnlyList<JsonElement> Subscribe(Action<JsonElement> listener)
    {
        lock (gate)
        {
            listeners.Add(listener);
            return events.ToArray();
        }
    }

    public void Unsubscribe(Action<JsonElement> listener)
    {
        lock (gate) listeners.Remove(listener);
    }

    public static string? EventId(JsonElement evt) =>
        evt.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null;
}
