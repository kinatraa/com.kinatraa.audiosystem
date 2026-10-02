using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace kinatraa.AudioSystem
{
    /// <summary>
    /// Static entry point of the audio system. Initializes itself before the first scene loads
    /// (see <see cref="AudioSystemConfig"/>). All members are main-thread only.
    /// </summary>
    public static class Audio
    {
        internal const int WarnMissingId = 0;
        internal const int WarnNoClips = 1;
        internal const int WarnMissingClip = 2;
        internal const int WarnMixerParam = 3;
        internal const int WarnMissingPlaylist = 4;

        private static AudioRuntime runtime;
        private static bool quitting;
        private static bool customStore;
        private static IAudioSettingsStore settingsStore;
        private static IAudioClipProvider clipProvider = DefaultClipProvider.Instance;
        private static IAudioPolicy policy;
        private static readonly List<AudioLibrary> libraries = new List<AudioLibrary>();
        private static readonly Dictionary<string, CueState> cues = new Dictionary<string, CueState>(StringComparer.Ordinal);
        private static readonly Dictionary<string, AudioPlaylist> playlists = new Dictionary<string, AudioPlaylist>(StringComparer.Ordinal);
        private static readonly HashSet<string>[] warned =
        {
            new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>()
        };

        /// <summary>Raised when a channel volume changes: (channel, volume 0..1).</summary>
        public static event Action<AudioChannel, float> OnVolumeChanged;

        /// <summary>Raised when a channel is muted or unmuted: (channel, muted).</summary>
        public static event Action<AudioChannel, bool> OnMuteChanged;

        /// <summary>Raised when a cue starts playing (including music): (cue id, handle).</summary>
        public static event Action<string, AudioHandle> OnCuePlayed;

        /// <summary>Raised when the current music changes: new cue id, or null when music stops.</summary>
        public static event Action<string> OnMusicChanged;

        // ------------------------------------------------------------------ lifecycle

        /// <summary>True once the system is running.</summary>
        public static bool IsInitialized
        {
            get { return runtime != null; }
        }

        /// <summary>Active config, or null before initialization.</summary>
        public static AudioSystemConfig Config
        {
            get { return runtime != null ? runtime.config : null; }
        }

        internal static AudioRuntime Runtime
        {
            get { return runtime; }
        }

        internal static IAudioSettingsStore SettingsStore
        {
            get { return settingsStore; }
        }

        internal static IAudioClipProvider ClipProvider
        {
            get { return clipProvider; }
        }

        internal static IAudioPolicy Policy
        {
            get { return policy; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Needed when domain reload is disabled (Enter Play Mode Options).
            runtime = null;
            quitting = false;
            customStore = false;
            settingsStore = null;
            clipProvider = DefaultClipProvider.Instance;
            policy = null;
            libraries.Clear();
            cues.Clear();
            playlists.Clear();
            for (int i = 0; i < warned.Length; i++) warned[i].Clear();
            OnVolumeChanged = null;
            OnMuteChanged = null;
            OnCuePlayed = null;
            OnMusicChanged = null;
            AudioLog.Level = AudioLogLevel.Warning;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (runtime != null) return;
            AudioSystemConfig config = Resources.Load<AudioSystemConfig>(AudioSystemConfig.ResourcesPath);
            if (config != null && !config.autoInitialize) return;
            Initialize(config);
        }

        /// <summary>
        /// Starts (or restarts) the system with a config. Called automatically unless the config at
        /// <c>Resources/kinatraaAudioConfig</c> disables <see cref="AudioSystemConfig.autoInitialize"/>.
        /// </summary>
        /// <param name="config">Config to use; null uses built-in defaults.</param>
        public static void Initialize(AudioSystemConfig config = null)
        {
            if (!Application.isPlaying)
            {
                AudioLog.Warning("Audio.Initialize only works in play mode.");
                return;
            }
            if (runtime != null) Shutdown();
            quitting = false;
            Application.quitting -= HandleQuitting;
            Application.quitting += HandleQuitting;

            bool owns = config == null;
            if (owns)
            {
                config = ScriptableObject.CreateInstance<AudioSystemConfig>();
                config.name = "kinatraaAudioConfig (defaults)";
                config.hideFlags = HideFlags.DontSave;
            }
            AudioLog.Level = config.logLevel;
            if (!customStore) settingsStore = config.persistSettings ? new PlayerPrefsAudioSettingsStore(config.settingsKeyPrefix) : null;

            var go = new GameObject("[kinatraa Audio]");
            if (config.hideInHierarchy) go.hideFlags = HideFlags.HideInHierarchy;
            Object.DontDestroyOnLoad(go);
            runtime = go.AddComponent<AudioRuntime>();
            runtime.Setup(config, owns);

            for (int i = 0; i < config.libraries.Count; i++)
            {
                if (config.libraries[i] != null && !libraries.Contains(config.libraries[i])) libraries.Add(config.libraries[i]);
            }
            RebuildCues();
            AudioLog.Info("Initialized with " + config.name + " (" + cues.Count + " cues).");
        }

        /// <summary>Stops every sound, destroys the runtime object and unregisters all libraries. Hooks and events are kept.</summary>
        public static void Shutdown()
        {
            AudioRuntime r = runtime;
            runtime = null;
            if (r != null)
            {
                for (int i = r.active.Count - 1; i >= 0; i--) r.Release(r.active[i]);
                Object.Destroy(r.gameObject);
            }
            libraries.Clear();
            RebuildCues();
        }

        internal static void OnRuntimeDestroyed(AudioRuntime r)
        {
            if (runtime == r) runtime = null;
        }

        private static void HandleQuitting()
        {
            quitting = true;
        }

        private static bool EnsureRuntime()
        {
            if (runtime != null) return true;
            if (quitting || !Application.isPlaying) return false;
            Initialize(Resources.Load<AudioSystemConfig>(AudioSystemConfig.ResourcesPath));
            return runtime != null;
        }

        // ------------------------------------------------------------------ libraries

        /// <summary>Registers a library. Later registrations override cues with the same id.</summary>
        /// <param name="library">Library to add.</param>
        public static void RegisterLibrary(AudioLibrary library)
        {
            if (library == null || libraries.Contains(library)) return;
            libraries.Add(library);
            RebuildCues();
        }

        /// <summary>Unregisters a library (e.g. when its level unloads).</summary>
        /// <param name="library">Library to remove.</param>
        public static void UnregisterLibrary(AudioLibrary library)
        {
            if (libraries.Remove(library)) RebuildCues();
        }

        /// <summary>True when a cue with this id is registered.</summary>
        /// <param name="id">Cue id.</param>
        public static bool HasCue(string id)
        {
            return id != null && cues.ContainsKey(id);
        }

        /// <summary>Finds a registered cue.</summary>
        /// <param name="id">Cue id.</param>
        /// <param name="cue">The cue, or null.</param>
        /// <returns>True if found.</returns>
        public static bool TryGetCue(string id, out AudioCue cue)
        {
            CueState cs;
            cue = id != null && cues.TryGetValue(id, out cs) ? cs.cue : null;
            return cue != null;
        }

        internal static void NotifyLibraryChanged(AudioLibrary library)
        {
            if (libraries.Contains(library)) RebuildCues();
        }

        private static void RebuildCues()
        {
            cues.Clear();
            playlists.Clear();
            for (int l = 0; l < libraries.Count; l++)
            {
                AudioLibrary lib = libraries[l];
                if (lib == null) continue;
                for (int i = 0; i < lib.cues.Count; i++)
                {
                    AudioCue cue = lib.cues[i];
                    if (cue == null || string.IsNullOrEmpty(cue.id)) continue;
                    if (cues.ContainsKey(cue.id)) AudioLog.Info("Cue '" + cue.id + "' overridden by library " + lib.name + ".");
                    cues[cue.id] = new CueState(cue);
                }
                for (int i = 0; i < lib.playlists.Count; i++)
                {
                    AudioPlaylist p = lib.playlists[i];
                    if (p != null && !string.IsNullOrEmpty(p.id)) playlists[p.id] = p;
                }
            }
        }

        internal static CueState LookupCue(string id)
        {
            CueState cs;
            if (id != null && cues.TryGetValue(id, out cs)) return cs;
            if (FirstTime(WarnMissingId, id ?? ""))
            {
                AudioLog.Warning("Unknown audio cue id '" + id + "'. Is its library registered?");
            }
            return null;
        }

        internal static bool FirstTime(int category, string key)
        {
            return warned[category].Add(key ?? "");
        }

        // ------------------------------------------------------------------ playback

        /// <summary>Plays a cue in 2D.</summary>
        /// <param name="id">Cue id.</param>
        /// <returns>Handle to control the sound; invalid if it did not play.</returns>
        public static AudioHandle Play(string id)
        {
            return PlayInternal(id, Vector3.zero, null, false);
        }

        /// <summary>Plays a cue at a world position (3D according to the cue's spatial blend).</summary>
        /// <param name="id">Cue id.</param>
        /// <param name="position">World position.</param>
        /// <returns>Handle to control the sound; invalid if it did not play.</returns>
        public static AudioHandle Play(string id, Vector3 position)
        {
            return PlayInternal(id, position, null, true);
        }

        /// <summary>Plays a cue that follows a transform. Looping sounds stop when the transform is destroyed.</summary>
        /// <param name="id">Cue id.</param>
        /// <param name="follow">Transform to follow; null plays in 2D.</param>
        /// <returns>Handle to control the sound; invalid if it did not play.</returns>
        public static AudioHandle Play(string id, Transform follow)
        {
            if (follow == null) return Play(id);
            return PlayInternal(id, follow.position, follow, true);
        }

        private static AudioHandle PlayInternal(string id, Vector3 position, Transform follow, bool positional)
        {
            if (!EnsureRuntime()) return AudioHandle.Invalid;
            CueState cs = LookupCue(id);
            return cs != null ? runtime.PlayCue(cs, position, follow, positional) : AudioHandle.Invalid;
        }

        /// <summary>Plays a clip without a library entry, with default options (2D, SFX channel).</summary>
        /// <param name="clip">Clip to play.</param>
        /// <returns>Handle to control the sound; invalid if it did not play.</returns>
        public static AudioHandle PlayClip(AudioClip clip)
        {
            return PlayClip(clip, new AudioPlayOptions());
        }

        /// <summary>Plays a clip without a library entry.</summary>
        /// <param name="clip">Clip to play.</param>
        /// <param name="options">Playback options.</param>
        /// <returns>Handle to control the sound; invalid if it did not play.</returns>
        public static AudioHandle PlayClip(AudioClip clip, AudioPlayOptions options)
        {
            if (clip == null || !EnsureRuntime()) return AudioHandle.Invalid;
            return runtime.PlayClip(clip, ref options);
        }

        internal static void StopVoice(AudioVoice voice, float fadeSeconds)
        {
            if (runtime == null) return;
            bool currentMusic = runtime.musicHandle.Voice == voice;
            runtime.Stop(voice, fadeSeconds);
            if (currentMusic)
            {
                runtime.playlist = null;
                runtime.ClearMusic();
            }
        }

        internal static void SetPaused(AudioVoice voice, int flag, bool paused)
        {
            if (runtime != null) runtime.SetPaused(voice, flag, paused);
        }

        /// <summary>Stops every sound, including music.</summary>
        /// <param name="fadeSeconds">Fade-out duration; 0 stops immediately.</param>
        public static void StopAll(float fadeSeconds = 0f)
        {
            if (runtime == null) return;
            runtime.playlist = null;
            runtime.StopWhere(null, fadeSeconds);
            runtime.ClearMusic();
        }

        /// <summary>Stops every sound on a channel.</summary>
        /// <param name="channel">Channel to stop.</param>
        /// <param name="fadeSeconds">Fade-out duration; 0 stops immediately.</param>
        public static void StopChannel(AudioChannel channel, float fadeSeconds = 0f)
        {
            if (runtime == null) return;
            AudioVoice music = runtime.musicHandle.Voice;
            ChannelState c = runtime.GetChannel(channel.Name);
            bool stopsMusic = music != null && music.channel == c;
            runtime.StopWhere(c, fadeSeconds);
            if (!stopsMusic) return;
            runtime.playlist = null;
            runtime.ClearMusic();
        }

        /// <summary>Pauses every playing sound (e.g. for a pause menu). Sounds started afterwards play normally.</summary>
        public static void PauseAll()
        {
            if (runtime == null) return;
            runtime.globalPaused = true;
            runtime.SetPausedWhere(AudioVoice.PauseGlobal, true, false);
        }

        /// <summary>Resumes sounds paused by <see cref="PauseAll"/>.</summary>
        public static void ResumeAll()
        {
            if (runtime == null) return;
            runtime.globalPaused = false;
            runtime.SetPausedWhere(AudioVoice.PauseGlobal, false, false);
        }

        // ------------------------------------------------------------------ music

        /// <summary>Current music cue id, or null.</summary>
        public static string CurrentMusicId
        {
            get { return runtime != null ? runtime.musicId : null; }
        }

        /// <summary>Id of the playing playlist, or null.</summary>
        public static string CurrentPlaylistId
        {
            get { return runtime != null && runtime.playlist != null ? runtime.playlist.id : null; }
        }

        /// <summary>
        /// Plays a music cue on the Music channel, crossfading from the current music.
        /// Does nothing if the same cue already plays (see <see cref="AudioSystemConfig.restartSameMusic"/>).
        /// </summary>
        /// <param name="id">Music cue id.</param>
        /// <param name="fadeIn">Fade-in when no music plays; negative = config default.</param>
        /// <param name="crossfade">Crossfade from the current music; negative = config default.</param>
        /// <returns>Handle of the music voice.</returns>
        public static AudioHandle PlayMusic(string id, float fadeIn = -1f, float crossfade = -1f)
        {
            if (!EnsureRuntime()) return AudioHandle.Invalid;
            CueState cs = LookupCue(id);
            return cs != null ? runtime.PlayMusic(cs, fadeIn, crossfade, false) : AudioHandle.Invalid;
        }

        /// <summary>Stops the music and any playlist.</summary>
        /// <param name="fadeOut">Fade-out seconds; negative = config default.</param>
        public static void StopMusic(float fadeOut = -1f)
        {
            if (runtime != null) runtime.StopMusic(fadeOut);
        }

        /// <summary>Pauses the music.</summary>
        public static void PauseMusic()
        {
            if (runtime == null) return;
            runtime.musicPaused = true;
            runtime.SetPausedWhere(AudioVoice.PauseMusic, true, true);
        }

        /// <summary>Resumes music paused by <see cref="PauseMusic"/>.</summary>
        public static void ResumeMusic()
        {
            if (runtime == null) return;
            runtime.musicPaused = false;
            runtime.SetPausedWhere(AudioVoice.PauseMusic, false, true);
        }

        /// <summary>Plays a playlist from a registered library on the Music channel.</summary>
        /// <param name="playlistId">Playlist id.</param>
        /// <param name="shuffle">Shuffle the order (reshuffled on every loop).</param>
        public static void PlayPlaylist(string playlistId, bool shuffle = false)
        {
            if (!EnsureRuntime()) return;
            AudioPlaylist list;
            if (playlistId == null || !playlists.TryGetValue(playlistId, out list) || list.cueIds.Count == 0)
            {
                if (FirstTime(WarnMissingPlaylist, playlistId)) AudioLog.Warning("Unknown or empty playlist '" + playlistId + "'.");
                return;
            }
            runtime.PlayPlaylist(list, shuffle);
        }

        /// <summary>Skips to the next playlist track with a crossfade.</summary>
        public static void NextTrack()
        {
            if (runtime != null && runtime.playlist != null) runtime.AdvancePlaylist();
        }

        // ------------------------------------------------------------------ volume

        /// <summary>Sets a channel volume (0..1, linear). Saved through the settings store.</summary>
        /// <param name="channel">Channel.</param>
        /// <param name="volume">Volume 0..1.</param>
        public static void SetVolume(AudioChannel channel, float volume)
        {
            if (!EnsureRuntime()) return;
            volume = Mathf.Clamp01(volume);
            ChannelState c = runtime.GetChannel(channel.Name);
            if (c.volume == volume) return;
            runtime.SetVolume(c, volume);
            if (settingsStore != null) settingsStore.Save(c.name, volume);
            RaiseVolumeChanged(c.channel, volume);
        }

        /// <summary>Gets a channel volume (0..1).</summary>
        /// <param name="channel">Channel.</param>
        public static float GetVolume(AudioChannel channel)
        {
            return EnsureRuntime() ? runtime.GetChannel(channel.Name).volume : 1f;
        }

        /// <summary>Mutes or unmutes a channel. Saved through the settings store.</summary>
        /// <param name="channel">Channel.</param>
        /// <param name="muted">Mute state.</param>
        public static void SetMuted(AudioChannel channel, bool muted)
        {
            if (!EnsureRuntime()) return;
            ChannelState c = runtime.GetChannel(channel.Name);
            if (c.muted == muted) return;
            runtime.SetMuted(c, muted);
            if (settingsStore != null) settingsStore.SaveMuted(c.name, muted);
            RaiseMuteChanged(c.channel, muted);
        }

        /// <summary>True when a channel is muted.</summary>
        /// <param name="channel">Channel.</param>
        public static bool IsMuted(AudioChannel channel)
        {
            return EnsureRuntime() && runtime.GetChannel(channel.Name).muted;
        }

        /// <summary>Sets several channel volumes at once, keyed by channel name (for games that serialize their own settings).</summary>
        /// <param name="volumes">Channel name to volume 0..1.</param>
        public static void ApplyVolumes(IDictionary<string, float> volumes)
        {
            if (volumes == null) return;
            foreach (KeyValuePair<string, float> pair in volumes) SetVolume(new AudioChannel(pair.Key), pair.Value);
        }

        /// <summary>Returns every known channel volume keyed by channel name.</summary>
        public static Dictionary<string, float> GetAllVolumes()
        {
            var result = new Dictionary<string, float>(StringComparer.Ordinal);
            if (!EnsureRuntime()) return result;
            List<ChannelState> list = runtime.channels;
            for (int i = 0; i < list.Count; i++) result[list[i].name] = list[i].volume;
            return result;
        }

        /// <summary>
        /// Temporarily lowers a channel, e.g. music while a voice line plays. Overlapping ducks keep the lowest level
        /// and the longest duration.
        /// </summary>
        /// <param name="channel">Channel to duck.</param>
        /// <param name="level">Volume multiplier while ducked (0..1).</param>
        /// <param name="duration">Seconds before restoring; 0 or less = until <see cref="StopDuck"/>.</param>
        /// <param name="fade">Seconds to go down and back up.</param>
        public static void Duck(AudioChannel channel, float level, float duration, float fade = 0.25f)
        {
            if (!EnsureRuntime()) return;
            runtime.Duck(runtime.GetChannel(channel.Name), Mathf.Clamp01(level), duration, Mathf.Max(0f, fade));
        }

        /// <summary>Restores a ducked channel.</summary>
        /// <param name="channel">Channel.</param>
        /// <param name="fade">Seconds to restore.</param>
        public static void StopDuck(AudioChannel channel, float fade = 0.25f)
        {
            if (runtime != null) runtime.StopDuck(runtime.GetChannel(channel.Name), Mathf.Max(0f, fade));
        }

        // ------------------------------------------------------------------ hooks

        /// <summary>
        /// Replaces the settings store (PlayerPrefs by default) and immediately reloads every channel from it.
        /// Pass null to disable persistence.
        /// </summary>
        /// <param name="store">The game's store.</param>
        public static void SetSettingsStore(IAudioSettingsStore store)
        {
            settingsStore = store;
            customStore = true;
            if (runtime != null) runtime.LoadSettings();
        }

        /// <summary>Hooks a save system with delegates instead of an <see cref="IAudioSettingsStore"/> class.</summary>
        /// <param name="load">Returns the saved volume for a channel name, or null when none is saved.</param>
        /// <param name="save">Saves a volume for a channel name.</param>
        /// <param name="loadMuted">Optional: returns the saved mute state, or null.</param>
        /// <param name="saveMuted">Optional: saves a mute state.</param>
        public static void BindSettings(Func<string, float?> load, Action<string, float> save,
            Func<string, bool?> loadMuted = null, Action<string, bool> saveMuted = null)
        {
            SetSettingsStore(new DelegateAudioSettingsStore(load, save, loadMuted, saveMuted));
        }

        /// <summary>Sets how cue clips are resolved. Null restores the default (clips referenced by the cue).</summary>
        /// <param name="provider">Clip provider.</param>
        public static void SetClipProvider(IAudioClipProvider provider)
        {
            clipProvider = provider ?? DefaultClipProvider.Instance;
        }

        /// <summary>Sets a playback policy (veto, volume and pitch multipliers). Null removes it.</summary>
        /// <param name="audioPolicy">Policy.</param>
        public static void SetPolicy(IAudioPolicy audioPolicy)
        {
            policy = audioPolicy;
            if (runtime == null) return;
            runtime.enabled = true;
            if (policy != null) return;
            // Reset multipliers so the next frame restores normal volume and pitch.
            List<ChannelState> list = runtime.channels;
            for (int i = 0; i < list.Count; i++)
            {
                list[i].policyVolume = 1f;
                list[i].policyPitch = 1f;
            }
        }

        // ------------------------------------------------------------------ events

        internal static void RaiseVolumeChanged(AudioChannel channel, float volume)
        {
            if (OnVolumeChanged != null) OnVolumeChanged(channel, volume);
        }

        internal static void RaiseMuteChanged(AudioChannel channel, bool muted)
        {
            if (OnMuteChanged != null) OnMuteChanged(channel, muted);
        }

        internal static void RaiseCuePlayed(string id, AudioHandle handle)
        {
            if (OnCuePlayed != null) OnCuePlayed(id, handle);
        }

        internal static void RaiseMusicChanged(string id)
        {
            if (OnMusicChanged != null) OnMusicChanged(id);
        }
    }

    internal static class AudioLog
    {
        private const string Prefix = "[kinatraa Audio] ";
        public static AudioLogLevel Level = AudioLogLevel.Warning;

        public static void Info(string message)
        {
            if (Level >= AudioLogLevel.Info) Debug.Log(Prefix + message);
        }

        public static void Warning(string message)
        {
            if (Level >= AudioLogLevel.Warning) Debug.LogWarning(Prefix + message);
        }
    }
}
