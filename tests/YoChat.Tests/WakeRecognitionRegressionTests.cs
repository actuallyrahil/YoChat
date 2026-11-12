using System.Text.Json;

using YoChat.Core;
using YoChat.Speech;

static class WakeRecognitionRegressionTests
{
    public static void GrammarAllowsUnrelatedSpeech()
    {
        using var grammar = JsonDocument.Parse(WindowsSpeechRecognizer.CreateListeningGrammar("Yo Chat"));
        var values = grammar.RootElement.EnumerateArray().Select(v => v.GetString()).ToArray();
        if (!values.SequenceEqual(new[] { "yo chat", "[unk]" }))
            throw new Exception("Wake recognizer must offer an unknown-speech alternative.");
        if (WindowsSpeechRecognizer.ParseResult("{\"text\":\"yo chat [unk]\",\"result\":[{\"conf\":1}]}") is not null)
            throw new Exception("Unknown speech must never become a wake event.");
        if (WindowsSpeechRecognizer.ParseResult("{\"text\":\"yo chat\"}") is not null)
            throw new Exception("Missing word confidence must reject the result.");
        var result = WindowsSpeechRecognizer.ParseResult("{\"text\":\"yo chat\",\"result\":[{\"conf\":0.9},{\"conf\":0.8}]}");
        if (result is null || Math.Abs(result.Confidence - .8f) > .001)
            throw new Exception("Use the least confident word, not an inflated phrase score.");
    }
    public static void ExactUtteranceOnly()
    {
        var gate = new WakePhraseGate("Yo Chat", .78f, TimeSpan.Zero);
        foreach (var text in new[] { "hello", "good morning", "no thanks", "your chat", "yo", "chat", "yo chat please", "did you say yo chat", "", "   " })
            if (gate.TryAccept(text, 1f, DateTimeOffset.UtcNow)) throw new Exception("Unrelated or longer utterance accepted: " + text);
        if (!gate.TryAccept("Yo, chat!", .95f, DateTimeOffset.UtcNow))
            throw new Exception("Dictation punctuation must not prevent an otherwise exact wake phrase.");
        foreach (var confidence in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -.1f, 1.1f })
            if (gate.TryAccept("Yo Chat", confidence, DateTimeOffset.UtcNow)) throw new Exception("Invalid confidence accepted.");
        if (new WakePhraseGate("", .78f, TimeSpan.Zero).TryAccept("", 1f, DateTimeOffset.UtcNow))
            throw new Exception("Empty wake phrase accepted.");
    }
}


