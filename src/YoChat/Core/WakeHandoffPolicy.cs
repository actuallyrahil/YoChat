namespace YoChat.Core;

public enum ChatGptCaptureState
{
    NotCapturing,
    Capturing,
    Unavailable
}

public readonly record struct ChatGptCaptureStatus(ChatGptCaptureState State, string? Detail = null);

public enum WakeHandoffDecision
{
    SendHotkey,
    SuppressWhileChatGptCaptures,
    SuppressBecauseStateIsUnknown
}

public static class WakeHandoffPolicy
{
    public static WakeHandoffDecision Decide(ChatGptCaptureStatus status) => status.State switch
    {
        ChatGptCaptureState.NotCapturing => WakeHandoffDecision.SendHotkey,
        ChatGptCaptureState.Capturing => WakeHandoffDecision.SuppressWhileChatGptCaptures,
        _ => WakeHandoffDecision.SuppressBecauseStateIsUnknown
    };
}

