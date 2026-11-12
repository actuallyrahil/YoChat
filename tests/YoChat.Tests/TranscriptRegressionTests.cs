using System.Text.Json;

static class TranscriptRegressionTests
{
    static string Snapshot(params (string Role, string Text)[] messages) => JsonSerializer.Serialize(new
    {
        version = 1,
        threads = new Dictionary<string, object> { ["synthetic"] = new { items = messages.Select(m => new { role = m.Role, text = m.Text }) } }
    });
    static ContinuityReader Read(params (string Role, string Text)[] messages)
    {
        var reader = new ContinuityReader();
        reader.Update(Snapshot());
        reader.Update(Snapshot(messages));
        return reader;
    }
    public static void Run()
    {
        var reader = Read(("user", "Hello"), ("user", "Hello"), ("assistant", "Welcome"), ("assistant", "Welcome"));
        if (reader.Rows.Length != 2) throw new Exception("Adjacent identical source entries must render once for each speaker");
        reader.Update(Snapshot(("user", "Hello"), ("assistant", "Welcome")));
        if (reader.Rows.Length != 2) throw new Exception("Source cleanup must preserve the conversation");
        var repeats = Read(("user", "Yes"), ("assistant", "Yes"), ("user", "Yes"));
        if (repeats.Rows.Length != 3) throw new Exception("Repeated words across turns or speakers must survive");
        var prefixes = Read(("user", "Go"), ("user", "Go ahead"), ("assistant", "Okay"), ("assistant", "Okay."));
        if (prefixes.Rows.Length != 4) throw new Exception("Different text must not be merged speculatively");
        var window = Read(("user", "One"), ("assistant", "Two"), ("user", "Three"), ("assistant", "Four"), ("user", "Five"), ("user", "Five"));
        if (window.Rows.Length != 5 || window.Rows[0].Text != "One") throw new Exception("Deduplicate before applying the five-message limit");
        reader.Reset(); reader.Update(Snapshot(("user", "Old")));
        if (reader.Rows.Length != 0) throw new Exception("Reset must still suppress history");
    }
}
