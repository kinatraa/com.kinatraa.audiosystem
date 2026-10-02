# Getting Started

## 1. Install

Use the Package Manager git URL `https://github.com/kinatraa/com.kinatraa.audiosystem.git#1.0.0`, or add the repository as a submodule at `Packages/com.kinatraa.audiosystem`.

## 2. Create the config and a library

Run **Tools ▸ kinatraa ▸ Audio ▸ Create Default Config**. It creates:

- `Assets/Resources/kinatraaAudioConfig.asset`, which is loaded automatically before the first scene.
- `Assets/Audio/AudioLibrary.asset`, which is already referenced by the config.

Without a config the system still starts with built-in defaults, but no library is registered until you call `Audio.RegisterLibrary`.

## 3. Add cues

Open the library (select it, or use **Tools ▸ kinatraa ▸ Audio ▸ Library Window**) and drop AudioClips or folders on the drop area.

- Ids are snake_case versions of the file names: `UI Click.wav` becomes `ui_click`.
- With *Group numbered variations* enabled, `footstep_01` and `footstep_02` become one cue `footstep` with two clips.
- Expand a cue to set its channel, volume and pitch ranges, loop, 3D settings, cooldown, instance limit, delay, fades and tags.
- **Play** previews a cue in the editor. Red rows have an empty or duplicate id. *(!)* marks a cue with no clips or a missing clip.
- Select several cues with the checkboxes to bulk-edit channel, volume, pitch and tags, or to delete them.

Music cues are always routed to the **Music** channel by `Audio.PlayMusic`, whatever channel the cue uses.

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
