# Getting Started

## 1. Install

Use the Package Manager git URL `https://github.com/kinatraa/com.kinatraa.audiosystem.git#1.1.0`, or add the repository as a submodule at `Packages/com.kinatraa.audiosystem`.

## 2. Open the Library window

Open **Tools ▸ kinatraa ▸ Audio ▸ Library Window**. The first time, it offers **Create Audio Setup**, which creates:

- `Assets/Resources/kinatraaAudioConfig.asset`, which is loaded automatically before the first scene.
- `Assets/Audio/AudioLibrary.asset`, which is already registered in the config.

More libraries (for example one per level) come from the window's **Libraries ▾ ▸ New Library...**, which registers the new library in the config automatically. A library that is not registered shows a warning with a **Register in Config** button.

## 3. Add cues

Drag clips or whole folders onto one of the two drop zones:

| Zone | Result |
|---|---|
| **SFX** | One cue per file, on the SFX channel. With *Combine numbered SFX* on, `footstep_01` and `footstep_02` become one cue `footstep` with two random variations. |
| **Music** | One looping 2D cue per file on the Music channel. Numbered files stay separate, so `BGM_01` and `BGM_02` become `bgm_01` and `bgm_02`. |

Ids are snake_case versions of the file names (`UI Click.wav` becomes `ui_click`). Dropping a clip whose cue already exists adds it to that cue.

Working with the list:

- Click a cue to edit it in the detail panel. The common settings (clips, channel, volume, pitch, loop) are always visible. **3D Sound**, **Limits**, **Timing** and **Tags** are collapsed sections.
- **▶** previews a cue. Red ids are empty or duplicated, and a warning icon marks a cue with no clips or a missing clip.
- Right-click a cue to duplicate, delete or move it. The toolbar's search box also matches tags, and the channel popup filters the list.
- **More ▾** holds Sort by Id, Generate Audio Ids, Validate Setup and Select Config.

Music cues are always routed to the **Music** channel by `Audio.PlayMusic`, whatever channel the cue uses. To rotate tracks, add them to a playlist (the **Playlists** section under the cue editor) and call `Audio.PlayPlaylist`.

## 4. Play from code

```csharp
using kinatraa.AudioSystem;

public class Player : MonoBehaviour
{
    [AudioCueId] public string jumpCue = "jump";   // dropdown in the inspector

    void Jump()
    {
        Audio.Play(jumpCue, transform);              // follows the player
    }
}
```

Generate constants with **Tools ▸ kinatraa ▸ Audio ▸ Generate Audio Ids** to catch typos at compile time: `Audio.Play(AudioIds.Jump)`.

## 5. Play without code

Add **kinatraa ▸ Audio ▸ Audio Emitter** to a GameObject, pick a cue, and choose when it plays: on enable, on start, on trigger enter (needs a trigger collider), or manually from a UnityEvent through `AudioEmitter.Play()` or `PlayCue(string)`.

Put **Audio Listener Auto Setup** on your main camera to keep exactly one active listener.

## 6. Configure

Select `kinatraaAudioConfig` to edit:

- **Channels**: name, default volume, optional mixer group and exposed mixer volume parameter (dB). Without a mixer parameter, channel volume multiplies AudioSource volumes directly.
- **Pool**: initial and maximum AudioSource count. When the pool is full, the least important voice (by priority, then age) is stolen. Music is never stolen.
- **Music**: default fade-in, crossfade and fade-out, and whether replaying the current track restarts it.
- **Settings**: persist volumes and the PlayerPrefs key prefix.
- **Behaviour**: pause on application pause or focus loss, stop sounds on scene unload, hide the runtime object, log level.
- **Ids generator**: output path, class name and namespace.

Run **Validate Setup** to find duplicate ids, missing clips, broken mixer parameters and unregistered libraries.

## 7. Debug

**Tools ▸ kinatraa ▸ Audio ▸ Runtime Debugger** (play mode) shows active voices with time left, pool usage, channel sliders and mute toggles, ducking and music state.

## Manual initialization

Disable **Auto Initialize** in the config (or keep the config outside `Resources`), then:

```csharp
Audio.Initialize(myConfig);       // call again to restart with another config
Audio.RegisterLibrary(levelLibrary);
Audio.UnregisterLibrary(levelLibrary);
Audio.Shutdown();
```

## Threading

All APIs are main-thread only.
