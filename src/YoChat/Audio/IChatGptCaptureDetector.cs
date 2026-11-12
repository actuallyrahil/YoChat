using YoChat.Core;

namespace YoChat.Audio;

public interface IChatGptCaptureDetector
{
    ChatGptCaptureStatus GetStatus();
}

