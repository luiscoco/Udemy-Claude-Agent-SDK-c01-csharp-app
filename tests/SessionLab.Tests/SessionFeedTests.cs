using System.Text.Json;
using SessionLab.Services;

namespace SessionLab.Tests;

public class SessionFeedTests
{
    static JsonElement Event(string type, string? id = null) =>
        JsonSerializer.SerializeToElement(id is null ? new { type } : (object)new { type, id });

    [Fact]
    public void Emit_skips_events_whose_id_was_already_seen()
    {
        var feed = new SessionFeed("sesn_test");
        feed.Emit(Event("agent.message", "sevt_1"));
        feed.Emit(Event("agent.message", "sevt_1"));
        feed.Emit(Event("app.stream_closed"));
        feed.Emit(Event("app.stream_closed"));

        Assert.Equal(3, feed.Subscribe(_ => { }).Count);
    }

    [Fact]
    public void Subscribe_replays_history_then_delivers_only_new_events()
    {
        var feed = new SessionFeed("sesn_test");
        feed.Emit(Event("user.message", "sevt_1"));
        var live = new List<JsonElement>();

        var replay = feed.Subscribe(live.Add);
        feed.Emit(Event("agent.message", "sevt_2"));

        Assert.Equal("sevt_1", SessionFeed.EventId(Assert.Single(replay)));
        Assert.Equal("sevt_2", SessionFeed.EventId(Assert.Single(live)));
    }

    [Fact]
    public void Unsubscribe_stops_delivery()
    {
        var feed = new SessionFeed("sesn_test");
        var live = new List<JsonElement>();
        feed.Subscribe(live.Add);
        feed.Unsubscribe(live.Add);

        feed.Emit(Event("agent.message", "sevt_1"));

        Assert.Empty(live);
    }
}
