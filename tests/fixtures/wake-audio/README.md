# Synthetic wake-recognition fixtures

Generated locally using Windows' Microsoft Zira Desktop text-to-speech voice.
No microphone recordings or private conversations are included.

| File | Spoken text | Expected wake |
| --- | --- | --- |
| 0.wav | Good morning | No |
| 1.wav | What is the weather today | No |
| 2.wav | Please open the document | No |
| 3.wav | Thank you very much | No |
| 4.wav | I am speaking about something else | No |
| 5.wav | No thanks | No |
| 6.wav | Yo Chat | Yes |
| 7.wav | Yo Chat please | No |
| 8.wav | Hey computer | No |

These regressions exercise actual audio decoding. They do not establish
false-activation rates for real voices, room noise, or playback audio.
