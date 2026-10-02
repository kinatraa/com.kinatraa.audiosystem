# kinatraa Audio System

A dependency-free audio system for Unity, packaged for the Unity Package Manager.

- **One-line playback.** Call `Audio.Play("ui_click")` for a 2D sound, or pass a position or a transform for 3D.
- **Data-driven cues.** Cues live in `AudioLibrary` assets: clip variations, random volume/pitch, cooldowns, instance limits with steal policies, delays, fades and tags.
- **Pooled AudioSources.** No `Destroy` per sound, no GC allocations when playing, no `Update` while idle.
- **Music.** Two-source crossfades, fade in/out, pause/resume and playlists with shuffle.
- **Channels.** Master, Music, SFX, UI, Voice and Ambient, plus custom channels defined in the config. Volume and mute work with or without an `AudioMixer`, and channels support ducking.
- **Your settings, your save file.** Pluggable settings store (PlayerPrefs by default), clip provider and playback policy hooks.
- **Editor tooling.** Library editor and window, drag-and-drop cue creation, inline preview, `[AudioCueId]` dropdown, `AudioIds.cs` generator, runtime debugger, setup wizard and validator.

It uses only Unity's built-in audio module. There are no third-party, Addressables, TextMeshPro or Input System dependencies.

## Install

**Package Manager (git URL)**: *Window ▸ Package Manager ▸ + ▸ Add package from git URL…*

```
https://github.com/kinatraa/com.kinatraa.audiosystem.git#1.1.0
```

or add it to `Packages/manifest.json`:

```json
"com.kinatraa.audiosystem": "https://github.com/kinatraa/com.kinatraa.audiosystem.git#1.1.0"
```

**Git submodule** (embedded, editable):

```
git submodule add https://github.com/kinatraa/com.kinatraa.audiosystem.git Packages/com.kinatraa.audiosystem
```

## 60-second quick start

1. Open **Tools ▸ kinatraa ▸ Audio ▸ Library Window** and click **Create Audio Setup**. This creates `Assets/Resources/kinatraaAudioConfig.asset` and a registered `Assets/Audio/AudioLibrary.asset`.
2. Drag sound effects onto the **SFX** zone and music onto the **Music** zone. `UI Click.wav` becomes `ui_click`, numbered SFX like `step_01`/`step_02` become one cue `step`, and each music file becomes its own looping Music cue.
3. Play sounds from code. The system starts itself before the first scene loads.

```csharp
using kinatraa.AudioSystem;

Audio.Play("ui_click");                                  // 2D
Audio.Play("explosion", transform.position);             // 3D at a position
AudioHandle engine = Audio.Play("engine_loop", transform); // follows a transform
engine.Stop(1f);                                         // fade out over 1 s
Audio.PlayMusic("battle_theme", fadeIn: 1f, crossfade: 1.5f);
Audio.SetVolume(AudioChannel.Music, 0.5f);               // saved to PlayerPrefs
```

4. Optional: **Tools ▸ kinatraa ▸ Audio ▸ Generate Audio Ids** writes `AudioIds.cs`, so you can call `Audio.Play(AudioIds.UiClick)`.

## API cheat sheet

| Task | Call |
|---|---|
| Play a 2D sound | `Audio.Play(id)` |
| Play a 3D sound | `Audio.Play(id, position)` / `Audio.Play(id, transform)` |
| Play a clip with no cue | `Audio.PlayClip(clip, new AudioPlayOptions { Channel = AudioChannel.UI, Volume = 0.5f })` |
| Control a sound | `h.Stop()`, `h.Stop(fade)`, `h.SetVolume(x)`, `h.SetPitch(x)`, `h.Pause()`, `h.Resume()`, `h.IsPlaying`, `h.IsValid` |
| Music | `Audio.PlayMusic(id, fadeIn, crossfade)`, `StopMusic(fadeOut)`, `PauseMusic()`, `ResumeMusic()` |
| Playlists | `Audio.PlayPlaylist(id, shuffle: true)`, `Audio.NextTrack()` |
| Volume | `Audio.SetVolume(channel, 0..1)`, `GetVolume`, `SetMuted`, `IsMuted` |
| Pause menu | `Audio.PauseAll()`, `Audio.ResumeAll()` |
| Stop | `Audio.StopAll(fade)`, `Audio.StopChannel(channel, fade)` |
| Ducking | `Audio.Duck(AudioChannel.Music, 0.3f, duration, fade)`, `Audio.StopDuck(channel)` |
| Libraries | `Audio.RegisterLibrary(lib)`, `Audio.UnregisterLibrary(lib)`, `Audio.HasCue(id)` |
| Lifecycle | `Audio.Initialize(config)`, `Audio.Shutdown()`, `Audio.IsInitialized` |
| Events | `OnVolumeChanged`, `OnMuteChanged`, `OnCuePlayed`, `OnMusicChanged` |

Components: `AudioEmitter` plays a cue on enable, on start, on trigger enter or from a UnityEvent. `AudioListenerAutoSetup` keeps exactly one enabled listener.

Full reference: [Documentation~/API.md](Documentation~/API.md).

## Settings integration (summary)

The package never owns your save format. Pick one of these:

```csharp
// 1. Default: PlayerPrefs, nothing to do.
// 2. Your own store class:
Audio.SetSettingsStore(new MySaveSystemAudioStore());   // reloads volumes immediately
// 3. Delegates, no class needed:
Audio.BindSettings(load: key => save.GetFloat(key), save: (key, v) => save.SetFloat(key, v));
// 4. Your own settings object:
Audio.ApplyVolumes(mySettings.volumes);   // IDictionary<string, float>
mySettings.volumes = Audio.GetAllVolumes();
```

`IAudioClipProvider` lets you load clips through Addressables or asset bundles. `IAudioPolicy` can veto sounds (for example during cutscenes) and scale volume or pitch per channel (for example in slow motion). See [Documentation~/SettingsIntegration.md](Documentation~/SettingsIntegration.md) for complete PlayerPrefs, JSON file and ScriptableObject examples.

## Editor tools

All tools are under **Tools ▸ kinatraa ▸ Audio** and **Assets ▸ Create ▸ kinatraa ▸ Audio**:

- **Library Window** and library inspector: cue list with search, channel filter and preview, a detail panel for the selected cue, SFX/Music drop zones, duplicate-id and missing-clip warnings, and a right-click menu to duplicate, delete or move cues.
- **Runtime Debugger** (play mode): active voices, pool usage, channel sliders and mute toggles, ducking and music state.
- **Generate Audio Ids**: deterministic `AudioIds.cs`. The output path, class name and namespace are set in the config.
- **Create Default Config** (also offered by the Library window) and **Validate Setup**.
- `[AudioCueId]`: put it on any `string` field to get a searchable id dropdown.

## Notes

- The API is **main-thread only**.
- A missing id logs one warning per id and returns an invalid handle. It never throws.
- Play mode with domain reload disabled (Enter Play Mode Options) is supported.

## Supported Unity versions

The minimum is **2019.4 LTS**. The code targets C# 7.3 with no `UnityEngine.Pool`, and version-specific APIs are behind `#if UNITY_2023_1_OR_NEWER`. Version 1.1.0 was tested on Unity 6000.3 (6.3): editor, play mode and a macOS Mono player. The runtime and editor sources also compile at C# 7.3, which is the 2019.4 language level. Earlier editors (2019.4, 2021.3, 2022.3) have not been run yet; please report any issue.

## Roadmap

- Async clip-provider support (load on demand, then play)
- Snapshot transitions for AudioMixer
- Per-cue random start time and pitch-follows-time-scale option
- UI Toolkit versions of the editor windows

## License

MIT. See [LICENSE](LICENSE).
