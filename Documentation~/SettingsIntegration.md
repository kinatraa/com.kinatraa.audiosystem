# Settings Integration

The package never owns your game's settings or save format. Channel volumes (0..1) and mute states are read and written through an `IAudioSettingsStore`. Keys are channel names such as `"Master"`, `"Music"` and `"SFX"`.

```csharp
public interface IAudioSettingsStore
{
    bool TryLoad(string key, out float value);
    void Save(string key, float value);
    bool TryLoadMuted(string key, out bool muted);
    void SaveMuted(string key, bool muted);
}
```

- `Audio.SetVolume` / `Audio.SetMuted` save through the store.
- `Audio.SetSettingsStore(store)` swaps the store and **immediately reloads** every channel from it. Channels with no saved value go back to their default volume. Pass `null` to disable persistence.
- The store is used on every `Audio.Initialize`. To have values loaded before the first sound, set the store early, for example from a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` method or from your boot scene.
- `OnVolumeChanged` and `OnMuteChanged` fire for changes made through the API and for values reloaded from a store, so settings UIs stay in sync.

## Example 1: PlayerPrefs (default)

There's nothing to do. When **Persist Settings** is on in the config, a `PlayerPrefsAudioSettingsStore` with the config's key prefix (default `kinatraa.audio.`) is used.

```csharp
using kinatraa.AudioSystem;
using UnityEngine;
using UnityEngine.UI;

public class VolumeSlider : MonoBehaviour
{
    public Slider slider;
    public string channel = "Music";

    void Start()
    {
        slider.value = Audio.GetVolume(channel);              // restored from PlayerPrefs
        slider.onValueChanged.AddListener(v => Audio.SetVolume(channel, v));
    }
}
```

To use another prefix in code: `Audio.SetSettingsStore(new PlayerPrefsAudioSettingsStore("mygame.audio."));`

## Example 2: JSON save file

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using kinatraa.AudioSystem;
using UnityEngine;

public class JsonAudioSettingsStore : IAudioSettingsStore
{
    [Serializable] class Entry { public string key; public float volume = 1f; public bool hasVolume; public bool muted; public bool hasMuted; }
    [Serializable] class Data { public List<Entry> channels = new List<Entry>(); }

    readonly string path;
    readonly Data data;

    public JsonAudioSettingsStore(string path)
    {
        this.path = path;
        data = File.Exists(path) ? JsonUtility.FromJson<Data>(File.ReadAllText(path)) : new Data();
    }

    public bool TryLoad(string key, out float value)
    {
        Entry e = Find(key, false);
        value = e != null ? e.volume : 1f;
        return e != null && e.hasVolume;
    }

    public void Save(string key, float value)
    {
        Entry e = Find(key, true);
        e.volume = value;
        e.hasVolume = true;
        File.WriteAllText(path, JsonUtility.ToJson(data, true));
    }

    public bool TryLoadMuted(string key, out bool muted)
    {
        Entry e = Find(key, false);
        muted = e != null && e.muted;
        return e != null && e.hasMuted;
    }

    public void SaveMuted(string key, bool muted)
    {
        Entry e = Find(key, true);
        e.muted = muted;
        e.hasMuted = true;
        File.WriteAllText(path, JsonUtility.ToJson(data, true));
    }

    Entry Find(string key, bool create)
    {
        foreach (Entry e in data.channels) if (e.key == key) return e;
        if (!create) return null;
        var entry = new Entry { key = key };
        data.channels.Add(entry);
        return entry;
    }
}

public static class AudioBoot
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        Audio.SetSettingsStore(new JsonAudioSettingsStore(Path.Combine(Application.persistentDataPath, "audio.json")));
    }
}
```

If your game already has one save object, skip the store and copy volumes in and out:

```csharp
Audio.SetSettingsStore(null);              // the game persists volumes itself
Audio.ApplyVolumes(save.audioVolumes);     // IDictionary<string, float>, on load
save.audioVolumes = Audio.GetAllVolumes(); // before writing the save file
```

## Example 3: ScriptableObject settings asset

Handy for an existing settings asset, a debug menu, or tests. You can hook it up with delegates instead of writing a store class:

```csharp
using System.Collections.Generic;
using kinatraa.AudioSystem;
using UnityEngine;

[CreateAssetMenu(menuName = "MyGame/Game Settings")]
public class GameSettings : ScriptableObject
{
    [System.Serializable] public class ChannelSetting { public string channel; [Range(0, 1)] public float volume = 1f; public bool muted; }
    public List<ChannelSetting> audio = new List<ChannelSetting>();

    public ChannelSetting Get(string channel, bool create)
    {
        ChannelSetting s = audio.Find(x => x.channel == channel);
        if (s == null && create) audio.Add(s = new ChannelSetting { channel = channel });
        return s;
    }
}

public class GameSettingsAudioBinder : MonoBehaviour
{
    public GameSettings settings;

    void Awake()
    {
        Audio.BindSettings(
            load: key => { var s = settings.Get(key, false); return s != null ? s.volume : (float?)null; },
            save: (key, v) => settings.Get(key, true).volume = v,
            loadMuted: key => { var s = settings.Get(key, false); return s != null ? s.muted : (bool?)null; },
            saveMuted: (key, m) => settings.Get(key, true).muted = m);
    }
}
```

Changes made to a ScriptableObject at runtime are kept in the editor but not in builds. Serialize the asset's data into your save system if it must survive a restart.

## Clip providers (Addressables, bundles, Resources)

`IAudioClipProvider` returns the clip for a cue variation. The package itself never references Addressables:

```csharp
public class ResourcesClipProvider : IAudioClipProvider
{
    public AudioClip GetClip(AudioCue cue, int clipIndex)
    {
        AudioClip clip = cue.clips[clipIndex];
        return clip != null ? clip : Resources.Load<AudioClip>("Audio/" + cue.id);
    }
}

Audio.SetClipProvider(new ResourcesClipProvider());
```

Clips must be available synchronously, so preload Addressables or bundles before playing (for example, per level).

## Playback policy

`IAudioPolicy` applies game-wide rules. The multipliers are read every frame while sounds play:

```csharp
public class GamePolicy : IAudioPolicy
{
    public bool inCutscene;
    public bool CanPlay(string cueId, AudioChannel channel) => !(inCutscene && channel == AudioChannel.SFX);
    public float GetVolumeMultiplier(AudioChannel channel) => Application.isFocused ? 1f : 0f;
    public float GetPitchMultiplier(AudioChannel channel) => channel == AudioChannel.Music || channel == AudioChannel.UI ? 1f : Time.timeScale;
}

Audio.SetPolicy(new GamePolicy());
```

## Events

```csharp
Audio.OnVolumeChanged += (channel, volume) => ui.Refresh(channel, volume);
Audio.OnMuteChanged   += (channel, muted)  => ui.RefreshMute(channel, muted);
Audio.OnCuePlayed     += (id, handle)      => analytics.Count(id);
Audio.OnMusicChanged  += id                => nowPlaying.text = id ?? "";
```

With domain reload disabled, these events are cleared when play mode starts. Subscribe again from `Awake`/`OnEnable`.
