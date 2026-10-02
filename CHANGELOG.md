# Changelog

All notable changes to this package are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-10-02

### Added
- `Audio` static API: 2D, 3D and follow playback, `PlayClip` with `AudioPlayOptions`, and `AudioHandle` control (stop with fade, volume, pitch, pause and resume) with protection against handles that refer to a recycled AudioSource.
- `AudioLibrary` and `AudioCue`: play modes (Random, RandomNoRepeat, Sequential, First), volume and pitch ranges, loop, spatial settings, priority, cooldown, max instances with Oldest/Quietest/Reject stealing, start delay, fade in/out and tags. Multiple libraries can be registered at once.
- Music: two-source crossfade, fade in/out, pause/resume, playlists with shuffle, `NextTrack`, and no restart when the same track is requested again (configurable).
- Channels: built-in Master, Music, SFX, UI, Voice and Ambient, plus custom channels from the config. Volume and mute work with or without an AudioMixer (dB mapping), and channels support ducking.
- `AudioSystemConfig` with auto-bootstrap from `Resources/kinatraaAudioConfig`, built-in defaults, and support for disabled domain reload.
- Pooled AudioSources with pre-warm, growth to a maximum and priority-based stealing. No GC allocations when playing; the update loop is disabled while idle.
- Extension points: `IAudioSettingsStore` (PlayerPrefs by default), `Audio.BindSettings`, `ApplyVolumes`/`GetAllVolumes`, `IAudioClipProvider`, `IAudioPolicy`, and events.
- Components: `AudioEmitter` and `AudioListenerAutoSetup`.
- Editor: library inspector and window, cue inspector with min/max sliders, channel and `[AudioCueId]` drawers, `AudioIds.cs` generator, runtime debugger, Create Default Config and Validate Setup.
- Basic Usage sample, plus Getting Started, Settings Integration and API documentation.

[1.0.0]: https://github.com/kinatraa/com.kinatraa.audiosystem/releases/tag/1.0.0
