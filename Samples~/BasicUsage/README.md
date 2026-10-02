# Basic Usage sample

These scripts need no scene. Each one draws a small IMGUI panel, so they work without uGUI or the Input System.

| Script | Shows |
|---|---|
| `SfxExample` | 2D, 3D and follow playback, and a loop controlled through its `AudioHandle` (fade stop, pause, pitch). |
| `MusicExample` | `PlayMusic` crossfades, pause/resume/stop, a shuffled playlist, and ducking music under a voice line. |
| `VolumeSettingsPanel` | Per-channel sliders and mute toggles hooked to `Audio.SetVolume` / `Audio.SetMuted`, `OnVolumeChanged`, and `PauseAll` for a pause menu. |
| `JsonSettingsExample` | A custom `IAudioSettingsStore` that saves volumes in a JSON file instead of PlayerPrefs. |

## Try it

1. Open **Tools ▸ kinatraa ▸ Audio ▸ Library Window**, click **Create Audio Setup**, and drop a few clips on the SFX and Music zones.
2. Add the scripts to any GameObject in a scene that has an AudioListener (for example the Main Camera).
3. Pick cue ids in the inspector (the `[AudioCueId]` dropdown), add a playlist to the library if you want to try `MusicExample`, then enter Play Mode.
