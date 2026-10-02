# API Reference

Namespace: `kinatraa.AudioSystem` (editor: `kinatraa.AudioSystem.Editor`). All APIs are main-thread only.

## `Audio` (static)

### Lifecycle
| Member | Description |
|---|---|
| `bool IsInitialized` | True once the runtime exists. |
| `AudioSystemConfig Config` | The active config. |
| `void Initialize(AudioSystemConfig config = null)` | Starts or restarts the system. `null` uses built-in defaults. Called automatically before the first scene unless the Resources config disables it. |
| `void Shutdown()` | Stops all sounds, destroys the runtime object and unregisters libraries. Hooks and events are kept. |

### Libraries
| Member | Description |
|---|---|
| `void RegisterLibrary(AudioLibrary)` | Adds a library. Later registrations override cues with the same id. |
| `void UnregisterLibrary(AudioLibrary)` | Removes a library. |
| `bool HasCue(string id)` / `bool TryGetCue(string id, out AudioCue)` | Lookup. |

### Playback
| Member | Description |
|---|---|
| `AudioHandle Play(string id)` | 2D. |
| `AudioHandle Play(string id, Vector3 position)` | 3D at a position (uses the cue's spatial blend). |
| `AudioHandle Play(string id, Transform follow)` | 3D, follows the transform. A loop stops if the transform is destroyed. |
| `AudioHandle PlayClip(AudioClip clip)` / `PlayClip(AudioClip, AudioPlayOptions)` | Plays a clip with no cue. |
| `void StopAll(float fade = 0)` / `void StopChannel(AudioChannel, float fade = 0)` | Stop. |
| `void PauseAll()` / `void ResumeAll()` | Pause everything currently playing (pause menus). Sounds started afterwards still play. |

An unknown id logs **one** warning per id and returns an invalid handle. Nothing throws.

### Music
| Member | Description |
|---|---|
| `AudioHandle PlayMusic(string id, float fadeIn = -1, float crossfade = -1)` | Plays on the Music channel and crossfades from the current track. Negative values use the config defaults. The same track is not restarted unless `restartSameMusic` is set. |
| `void StopMusic(float fadeOut = -1)` | Stops the music and playlist. |
| `void PauseMusic()` / `void ResumeMusic()` | |
| `void PlayPlaylist(string playlistId, bool shuffle = false)` | Playlists are defined in libraries. Tracks crossfade by `defaultCrossfade`. |
| `void NextTrack()` | Skips to the next track. |
| `string CurrentMusicId`, `string CurrentPlaylistId` | |

### Volume
| Member | Description |
|---|---|
| `void SetVolume(AudioChannel, float 0..1)` / `float GetVolume(AudioChannel)` | Linear volume. Saved through the settings store. |
| `void SetMuted(AudioChannel, bool)` / `bool IsMuted(AudioChannel)` | |
| `void ApplyVolumes(IDictionary<string, float>)` / `Dictionary<string, float> GetAllVolumes()` | Bulk access keyed by channel name. |
| `void Duck(AudioChannel, float level, float duration, float fade = 0.25f)` | Temporarily scales a channel. `duration <= 0` lasts until `StopDuck`. Overlapping ducks keep the lowest level and the longest duration. |
| `void StopDuck(AudioChannel, float fade = 0.25f)` | |

Volume math: the final AudioSource volume is cue volume × handle volume × fade × channel (volume × duck × mute) × Master × policy multiplier. When a channel has an exposed mixer parameter, its part is written to the mixer in dB instead (`20·log10(v)`, clamped at the config's silence dB, -80 by default) and not multiplied into the source. Master goes to the mixer only for channels routed to a mixer group.

### Hooks
| Member | Description |
|---|---|
| `void SetSettingsStore(IAudioSettingsStore)` | Replaces the store and reloads all channels. `null` disables persistence. |
| `void BindSettings(Func<string,float?> load, Action<string,float> save, Func<string,bool?> loadMuted = null, Action<string,bool> saveMuted = null)` | Delegate adapter. |
| `void SetClipProvider(IAudioClipProvider)` | `null` restores the default (clips referenced by the cue). |
| `void SetPolicy(IAudioPolicy)` | `null` removes it. |

### Events
`OnVolumeChanged(AudioChannel, float)`, `OnMuteChanged(AudioChannel, bool)`, `OnCuePlayed(string id, AudioHandle)`, `OnMusicChanged(string idOrNull)`.

## `AudioHandle` (struct)
`IsValid`, `IsPlaying`, `IsPaused`, `CueId`, `Stop()` (cue fade-out), `Stop(float fade)`, `SetVolume(float)`, `SetPitch(float)`, `Pause()`, `Resume()`, `AudioHandle.Invalid`.

A handle stores a generation number. Once its sound ends and the AudioSource is reused, the handle becomes invalid and every call is a no-op.

## `AudioPlayOptions` (struct)
`Channel` (SFX), `Volume` (1), `Pitch` (1), `Loop`, `SpatialBlend` (1 with a position or follow target, else 0), `Position`, `Follow`, `MinDistance` (1), `MaxDistance` (50), `Rolloff`, `Priority` (128), `Delay`, `FadeIn`. `new AudioPlayOptions()` is valid.

## `AudioChannel` (struct)
Built-ins: `Master`, `Music`, `SFX`, `UI`, `Voice`, `Ambient`. Custom: `new AudioChannel("Footsteps")`, or simply `"Footsteps"` through the implicit conversion. A default (empty) channel means SFX.

## Assets
- `AudioLibrary`: `List<AudioCue> cues`, `List<AudioPlaylist> playlists`, `AudioCue FindCue(string)`.
- `AudioCue`: `id`, `clips`, `playMode` (Random/RandomNoRepeat/Sequential/First), `channel`, `volumeRange`, `pitchRange`, `loop`, `spatialBlend`, `minDistance`, `maxDistance`, `rolloff`, `priority`, `cooldown`, `maxInstances`, `stealPolicy` (Oldest/Quietest/Reject), `startDelay`, `fadeIn`, `fadeOut`, `tags`.
- `AudioPlaylist`: `id`, `cueIds`, `loop`.
- `AudioSystemConfig`: `autoInitialize`, `libraries`, `channels` (`AudioChannelDefinition`: `name`, `defaultVolume`, `mixerGroup`, `mixerVolumeParameter`), `mixer`, `silenceDecibels`, `fullVolumeDecibels`, `poolInitialSize`, `poolMaxSize`, `defaultMusicFadeIn`, `defaultCrossfade`, `defaultMusicFadeOut`, `restartSameMusic`, `persistSettings`, `settingsKeyPrefix`, `pauseOnApplicationPause`, `pauseOnFocusLost`, `stopSoundsOnSceneUnload`, `hideInHierarchy`, `logLevel`, `idsOutputPath`, `idsNamespace`, `idsClassName`.

## Interfaces
- `IAudioSettingsStore`: `TryLoad`, `Save`, `TryLoadMuted`, `SaveMuted`. Implementations: `PlayerPrefsAudioSettingsStore(prefix)` and `DelegateAudioSettingsStore`.
- `IAudioClipProvider`: `AudioClip GetClip(AudioCue cue, int clipIndex)`.
- `IAudioPolicy`: `bool CanPlay(string cueId, AudioChannel)`, `float GetVolumeMultiplier(AudioChannel)`, `float GetPitchMultiplier(AudioChannel)`.

## Components
- `AudioEmitter`: `cueId` (`[AudioCueId]`), `playOn` (Manual/OnEnable/OnStart/OnTriggerEnter), `positional`, `stopOnDisable`. Methods: `Play()`, `PlayCue(string)`, `Stop()`, and the `Handle` property.
- `AudioListenerAutoSetup`: keeps (or adds) the listener on its GameObject and disables the others, including after additive scene loads.

## Attributes
- `[AudioCueId]` on a `string` (or `List<string>`) field gives a searchable cue-id dropdown in the inspector.

## Runtime behaviour
- **Idle cost**: the hidden runtime component disables itself when no sound plays and no duck is in progress.
- **Allocations**: playing and stopping sounds allocates nothing once the pool is warm.
- **Scenes**: the runtime object is `DontDestroyOnLoad`. Set `stopSoundsOnSceneUnload` to stop non-music sounds on scene unload.
- **Enter Play Mode Options**: static state and events reset at `SubsystemRegistration`, so disabled domain reload is supported.
