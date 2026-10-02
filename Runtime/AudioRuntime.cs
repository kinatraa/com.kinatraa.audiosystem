using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace kinatraa.AudioSystem
{
    /// <summary>One pooled AudioSource plus its playback state. Reused; <see cref="generation"/> invalidates old handles.</summary>
    internal sealed class AudioVoice
    {
        public const int PauseUser = 1;
        public const int PauseGlobal = 2;
        public const int PauseMusic = 4;
        public const int PauseApp = 8;

        public AudioSource source;
        public Transform transform;
        public int generation = 1;
        public bool active;
        public int index;
        public CueState cue;
        public bool counted;
        public ChannelState channel;
        public bool isMusic;
        public bool loop;
        public int priority;
        public float baseVolume;
        public float basePitch;
        public float userVolume;
        public float userPitch;
        public float fade;
        public float fadeFrom;
        public float fadeTo;
        public float fadeTime;
        public float fadeDuration;
        public bool fading;
        public bool stopAfterFade;
        public bool stopping;
        public float defaultFadeOut;
        public float delayLeft;
        public bool started;
        public int pauseFlags;
        public Transform follow;
        public bool hasFollow;
        public float startTime;
        public float appliedVolume;
        public float appliedPitch;
    }

    /// <summary>Runtime state of a registered cue (sequence position, cooldown, instance count).</summary>
    internal sealed class CueState
    {
        public readonly AudioCue cue;
        public int lastIndex = -1;
        public float lastPlayTime = float.NegativeInfinity;
        public int instances;

        public CueState(AudioCue cue)
        {
            this.cue = cue;
        }
    }

    /// <summary>Runtime state of a channel: volume, mute, ducking and mixer binding.</summary>
    internal sealed class ChannelState
    {
        public AudioChannel channel;
        public string name;
        public float defaultVolume = 1f;
        public float volume = 1f;
        public bool muted;
        public float duck = 1f;
        public float duckTarget = 1f;
        public float duckFade;
        public float duckTimeLeft;
        public bool duckTimed;
        public AudioMixerGroup group;
        public AudioMixer mixer;
        public string mixerParam;
        public float appliedDb = float.NaN;
        public float gain = 1f;
        public float policyVolume = 1f;
        public float policyPitch = 1f;

        public float Effective
        {
            get { return muted ? 0f : volume * duck; }
        }

        public bool UsesMixer
        {
            get { return mixer != null && !string.IsNullOrEmpty(mixerParam); }
        }
    }

    /// <summary>Hidden DontDestroyOnLoad component that owns the pool and drives fades. Disabled while idle.</summary>
    [AddComponentMenu("")]
    internal sealed class AudioRuntime : MonoBehaviour
    {
        private struct Request
        {
            public AudioClip clip;
            public ChannelState channel;
            public CueState cue;
            public float volume;
            public float pitch;
            public bool loop;
            public float spatialBlend;
            public float minDistance;
            public float maxDistance;
            public AudioRolloffMode rolloff;
            public int priority;
            public float delay;
            public float fadeIn;
            public float fadeOut;
            public Vector3 position;
            public Transform follow;
            public bool isMusic;
        }

        internal AudioSystemConfig config;
        internal readonly List<AudioVoice> active = new List<AudioVoice>(32);
        internal readonly Stack<AudioVoice> free = new Stack<AudioVoice>(32);
        internal readonly List<ChannelState> channels = new List<ChannelState>(8);
        internal int created;
        internal ChannelState master;

        internal AudioHandle musicHandle;
        internal string musicId;
        internal bool musicPaused;
        internal AudioPlaylist playlist;
        internal bool globalPaused;

        private readonly Dictionary<string, ChannelState> channelMap = new Dictionary<string, ChannelState>(StringComparer.Ordinal);
        private bool ownsConfig;
        private bool gainsDirty = true;
        private bool appPaused;
        private bool focusLost;
        private int[] playlistOrder;
        private int playlistPos;
        private bool playlistShuffle;

        internal void Setup(AudioSystemConfig cfg, bool owns)
        {
            config = cfg;
            ownsConfig = owns;
            for (int i = 0; i < cfg.channels.Count; i++)
            {
                AudioChannelDefinition def = cfg.channels[i];
                if (def == null || string.IsNullOrEmpty(def.name)) continue;
                if (channelMap.ContainsKey(def.name))
                {
                    AudioLog.Warning("Channel '" + def.name + "' is defined twice in " + cfg.name + ". The second definition is ignored.");
                    continue;
                }
                AddChannel(def.name, def);
            }
            master = GetChannel(AudioChannel.MasterName);
            int prewarm = Mathf.Min(cfg.poolInitialSize, Mathf.Max(1, cfg.poolMaxSize));
            for (int i = 0; i < prewarm; i++) free.Push(CreateVoice());
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void Start()
        {
            // AudioMixer ignores SetFloat calls made before the first frame, so apply everything again here.
            for (int i = 0; i < channels.Count; i++)
            {
                channels[i].appliedDb = float.NaN;
                ApplyMixer(channels[i]);
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                active[i].active = false;
                active[i].generation++;
            }
            active.Clear();
            if (ownsConfig && config != null) Destroy(config);
            Audio.OnRuntimeDestroyed(this);
        }

        // ---------------------------------------------------------------- channels

        private ChannelState AddChannel(string channelName, AudioChannelDefinition def)
        {
            var c = new ChannelState { name = channelName, channel = new AudioChannel(channelName) };
            if (def != null)
            {
                c.defaultVolume = c.volume = Mathf.Clamp01(def.defaultVolume);
                c.group = def.mixerGroup;
                c.mixerParam = def.mixerVolumeParameter;
                c.mixer = def.mixerGroup != null ? def.mixerGroup.audioMixer : config.mixer;
            }
            channels.Add(c);
            channelMap.Add(channelName, c);
            LoadSetting(c);
            ApplyMixer(c);
            gainsDirty = true;
            return c;
        }

        internal ChannelState GetChannel(string channelName)
        {
            ChannelState c;
            return channelMap.TryGetValue(channelName, out c) ? c : AddChannel(channelName, null);
        }

        internal void LoadSettings()
        {
            for (int i = 0; i < channels.Count; i++) LoadSetting(channels[i]);
        }

        private void LoadSetting(ChannelState c)
        {
            IAudioSettingsStore store = Audio.SettingsStore;
            if (store == null) return;
            float v;
            float volume = store.TryLoad(c.name, out v) ? Mathf.Clamp01(v) : c.defaultVolume;
            bool m;
            bool muted = store.TryLoadMuted(c.name, out m) && m;
            if (volume != c.volume)
            {
                SetVolume(c, volume);
                Audio.RaiseVolumeChanged(c.channel, volume);
            }
            if (muted != c.muted)
            {
                SetMuted(c, muted);
                Audio.RaiseMuteChanged(c.channel, muted);
            }
        }

        internal void SetVolume(ChannelState c, float volume)
        {
            c.volume = volume;
            gainsDirty = true;
            ApplyMixer(c);
        }

        internal void SetMuted(ChannelState c, bool muted)
        {
            c.muted = muted;
            gainsDirty = true;
            ApplyMixer(c);
        }

        private void ApplyMixer(ChannelState c)
        {
            if (!c.UsesMixer) return;
            float db = config.ToDecibels(c.Effective);
            if (db == c.appliedDb) return;
            c.appliedDb = db;
            if (!c.mixer.SetFloat(c.mixerParam, db) && Audio.FirstTime(Audio.WarnMixerParam, c.mixerParam))
            {
                AudioLog.Warning("AudioMixer '" + c.mixer.name + "' has no exposed parameter '" + c.mixerParam + "' (channel " + c.name + ").");
            }
        }

        private void RecomputeGains()
        {
            float m = master.Effective;
            for (int i = 0; i < channels.Count; i++)
            {
                ChannelState c = channels[i];
                if (c == master)
                {
                    c.gain = master.UsesMixer ? 1f : m;
                    continue;
                }
                float own = c.UsesMixer ? 1f : c.Effective;
                // A master mixer parameter only reaches sources that are routed into the mixer.
                float masterGain = master.UsesMixer && c.group != null ? 1f : m;
                c.gain = own * masterGain;
            }
            gainsDirty = false;
        }

        private void UpdatePolicy(ChannelState c)
        {
            IAudioPolicy policy = Audio.Policy;
            if (policy == null)
            {
                c.policyVolume = 1f;
                c.policyPitch = 1f;
                return;
            }
            c.policyVolume = Mathf.Max(0f, policy.GetVolumeMultiplier(c.channel));
            c.policyPitch = policy.GetPitchMultiplier(c.channel);
        }

        internal void Duck(ChannelState c, float level, float duration, float fade)
        {
            bool alreadyDucked = c.duckTarget < 1f;
            c.duckTarget = alreadyDucked ? Mathf.Min(c.duckTarget, level) : level;
            c.duckFade = fade;
            if (duration > 0f)
            {
                // An open-ended duck stays open-ended; overlapping timed ducks extend each other.
                if (!alreadyDucked || c.duckTimed)
                {
                    c.duckTimeLeft = alreadyDucked ? Mathf.Max(c.duckTimeLeft, duration) : duration;
                    c.duckTimed = true;
                }
            }
            else
            {
                c.duckTimed = false;
            }
            enabled = true;
        }

        internal void StopDuck(ChannelState c, float fade)
        {
            c.duckTarget = 1f;
            c.duckTimed = false;
            c.duckFade = fade;
            enabled = true;
        }

        private bool UpdateDucks(float dt)
        {
            bool any = false;
            for (int i = 0; i < channels.Count; i++)
            {
                ChannelState c = channels[i];
                if (c.duckTimed)
                {
                    c.duckTimeLeft -= dt;
                    if (c.duckTimeLeft <= 0f)
                    {
                        c.duckTimed = false;
                        c.duckTarget = 1f;
                    }
                }
                if (c.duck != c.duckTarget)
                {
                    c.duck = c.duckFade > 0f ? Mathf.MoveTowards(c.duck, c.duckTarget, dt / c.duckFade) : c.duckTarget;
                    gainsDirty = true;
                    ApplyMixer(c);
                }
                // An idle open-ended duck needs no updates.
                if (c.duck != c.duckTarget || c.duckTimed) any = true;
            }
            return any;
        }

        // ---------------------------------------------------------------- pool

        private AudioVoice CreateVoice()
        {
            var go = new GameObject("Voice " + created);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            created++;
            return new AudioVoice { source = source, transform = go.transform };
        }

        private AudioVoice Acquire(int priority)
        {
            if (free.Count > 0) return free.Pop();
            if (created < Mathf.Max(1, config.poolMaxSize)) return CreateVoice();

            // Pool exhausted: steal the least important non-music voice, never a more important one.
            AudioVoice victim = null;
            for (int i = 0; i < active.Count; i++)
            {
                AudioVoice v = active[i];
                if (v.isMusic || v.priority < priority) continue;
                if (victim == null || v.priority > victim.priority || (v.priority == victim.priority && v.startTime < victim.startTime))
                {
                    victim = v;
                }
            }
            if (victim == null) return null;
            Release(victim);
            return free.Pop();
        }

        internal void Release(AudioVoice v)
        {
            if (!v.active) return;
            v.active = false;
            v.generation++;
            if (v.source != null)
            {
                v.source.Stop();
                v.source.clip = null;
            }
            int last = active.Count - 1;
            AudioVoice moved = active[last];
            active[v.index] = moved;
            moved.index = v.index;
            active.RemoveAt(last);
            if (v.counted) v.cue.instances--;
            v.counted = false;
            v.cue = null;
            v.channel = null;
            v.follow = null;
            v.hasFollow = false;
            free.Push(v);
        }

        // ---------------------------------------------------------------- playback

        internal AudioHandle PlayCue(CueState cs, Vector3 position, Transform follow, bool positional)
        {
            AudioCue cue = cs.cue;
            ChannelState ch = GetChannel(cue.channel.Name);
            if (Audio.Policy != null && !Audio.Policy.CanPlay(cue.id, ch.channel)) return AudioHandle.Invalid;

            float now = Time.unscaledTime;
            if (cue.cooldown > 0f && now - cs.lastPlayTime < cue.cooldown) return AudioHandle.Invalid;
            if (cue.maxInstances > 0 && cs.instances >= cue.maxInstances)
            {
                if (cue.stealPolicy == AudioStealPolicy.Reject) return AudioHandle.Invalid;
                AudioVoice victim = FindVictim(cs, cue.stealPolicy);
                if (victim == null) return AudioHandle.Invalid;
                Release(victim);
            }

            Request r;
            if (!BuildRequest(cs, ch, out r)) return AudioHandle.Invalid;
            r.position = position;
            r.follow = follow;
            if (!positional) r.spatialBlend = 0f;

            AudioHandle h = StartVoice(ref r);
            if (h.IsValid)
            {
                cs.lastPlayTime = now;
                Audio.RaiseCuePlayed(cue.id, h);
            }
            return h;
        }

        internal AudioHandle PlayClip(AudioClip clip, ref AudioPlayOptions o)
        {
            ChannelState ch = GetChannel(o.Channel.Name);
            if (Audio.Policy != null && !Audio.Policy.CanPlay(null, ch.channel)) return AudioHandle.Invalid;
            var r = new Request
            {
                clip = clip,
                channel = ch,
                volume = o.Volume,
                pitch = o.Pitch,
                loop = o.Loop,
                spatialBlend = o.SpatialBlend,
                minDistance = o.MinDistance,
                maxDistance = o.MaxDistance,
                rolloff = o.Rolloff,
                priority = o.Priority,
                delay = o.Delay,
                fadeIn = o.FadeIn,
                position = o.Follow != null ? o.Follow.position : o.Position,
                follow = o.Follow,
            };
            return StartVoice(ref r);
        }

        private bool BuildRequest(CueState cs, ChannelState ch, out Request r)
        {
            AudioCue cue = cs.cue;
            r = default(Request);
            AudioClip clip = PickClip(cs);
            if (clip == null) return false;
            r.clip = clip;
            r.cue = cs;
            r.channel = ch;
            r.volume = Random.Range(cue.volumeRange.x, cue.volumeRange.y);
            r.pitch = Random.Range(cue.pitchRange.x, cue.pitchRange.y);
            r.loop = cue.loop;
            r.spatialBlend = cue.spatialBlend;
            r.minDistance = cue.minDistance;
            r.maxDistance = cue.maxDistance;
            r.rolloff = cue.rolloff;
            r.priority = cue.priority;
            r.delay = cue.startDelay;
            r.fadeIn = cue.fadeIn;
            r.fadeOut = cue.fadeOut;
            return true;
        }

        private static AudioClip PickClip(CueState cs)
        {
            AudioCue cue = cs.cue;
            int n = cue.clips != null ? cue.clips.Count : 0;
            if (n == 0)
            {
                if (Audio.FirstTime(Audio.WarnNoClips, cue.id)) AudioLog.Warning("Audio cue '" + cue.id + "' has no clips.");
                return null;
            }
            int index;
            switch (cue.playMode)
            {
                case AudioClipPlayMode.First:
                    index = 0;
                    break;
                case AudioClipPlayMode.Sequential:
                    index = (cs.lastIndex + 1) % n;
                    break;
                case AudioClipPlayMode.RandomNoRepeat:
                    if (n == 1 || cs.lastIndex < 0 || cs.lastIndex >= n)
                    {
                        index = Random.Range(0, n);
                    }
                    else
                    {
                        index = Random.Range(0, n - 1);
                        if (index >= cs.lastIndex) index++;
                    }
                    break;
                default:
                    index = Random.Range(0, n);
                    break;
            }
            cs.lastIndex = index;
            AudioClip clip = Audio.ClipProvider.GetClip(cue, index);
            if (clip == null && Audio.FirstTime(Audio.WarnMissingClip, cue.id))
            {
                AudioLog.Warning("Audio cue '" + cue.id + "' has a missing clip (index " + index + ").");
            }
            return clip;
        }

        private AudioVoice FindVictim(CueState cs, AudioStealPolicy policy)
        {
            AudioVoice best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < active.Count; i++)
            {
                AudioVoice v = active[i];
                if (v.cue != cs || v.stopping) continue;
                float score = policy == AudioStealPolicy.Quietest ? v.appliedVolume : v.startTime;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = v;
                }
            }
            return best;
        }

        private AudioHandle StartVoice(ref Request r)
        {
            AudioVoice v = Acquire(r.priority);
            if (v == null)
            {
                AudioLog.Info("Audio pool exhausted; a sound was skipped. Increase poolMaxSize if this happens often.");
                return AudioHandle.Invalid;
            }

            AudioSource s = v.source;
            s.clip = r.clip;
            s.loop = r.loop;
            s.priority = r.priority;
            s.outputAudioMixerGroup = r.channel.group;
            s.spatialBlend = r.spatialBlend;
            s.minDistance = r.minDistance;
            s.maxDistance = r.maxDistance;
            s.rolloffMode = r.rolloff;
            v.transform.position = r.position;

            v.cue = r.cue;
            v.counted = r.cue != null;
            if (v.counted) r.cue.instances++;
            v.channel = r.channel;
            v.isMusic = r.isMusic;
            v.loop = r.loop;
            v.priority = r.priority;
            v.baseVolume = r.volume;
            v.basePitch = r.pitch;
            v.userVolume = 1f;
            v.userPitch = 1f;
            v.fade = 1f;
            v.fading = false;
            v.stopping = false;
            v.defaultFadeOut = r.fadeOut;
            v.pauseFlags = 0;
            v.follow = r.follow;
            v.hasFollow = r.follow != null;
            v.startTime = Time.unscaledTime;
            v.appliedVolume = -1f;
            v.appliedPitch = float.NaN;
            v.active = true;
            v.index = active.Count;
            active.Add(v);

            if (r.fadeIn > 0f)
            {
                v.fade = 0f;
                BeginFade(v, 1f, r.fadeIn, false);
            }
            if (gainsDirty) RecomputeGains();
            UpdatePolicy(r.channel);
            ApplyVoice(v);

            if (r.delay > 0f)
            {
                v.started = false;
                v.delayLeft = r.delay;
            }
            else
            {
                v.started = true;
                s.Play();
            }
            enabled = true;
            return new AudioHandle(v);
        }

        private void BeginFade(AudioVoice v, float to, float duration, bool stop)
        {
            v.fadeFrom = v.fade;
            v.fadeTo = to;
            v.fadeDuration = duration;
            v.fadeTime = 0f;
            v.fading = true;
            v.stopAfterFade = stop;
            enabled = true;
        }

        private void MarkStopping(AudioVoice v)
        {
            v.stopping = true;
            if (v.counted) v.cue.instances--;
            v.counted = false;
        }

        internal void Stop(AudioVoice v, float fadeSeconds)
        {
            if (!v.active) return;
            if (fadeSeconds <= 0f || !v.started || v.pauseFlags != 0)
            {
                Release(v);
                return;
            }
            // Already fading out faster than requested.
            if (v.stopping && v.fading && v.stopAfterFade && v.fadeDuration - v.fadeTime <= fadeSeconds) return;
            MarkStopping(v);
            BeginFade(v, 0f, fadeSeconds, true);
        }

        internal void SetPaused(AudioVoice v, int flag, bool paused)
        {
            int old = v.pauseFlags;
            v.pauseFlags = paused ? old | flag : old & ~flag;
            if (!v.started) return;
            if (old == 0 && v.pauseFlags != 0) v.source.Pause();
            else if (old != 0 && v.pauseFlags == 0) v.source.UnPause();
        }

        internal void SetPausedWhere(int flag, bool paused, bool musicOnly)
        {
            for (int i = 0; i < active.Count; i++)
            {
                if (!musicOnly || active[i].isMusic) SetPaused(active[i], flag, paused);
            }
        }

        internal void StopWhere(ChannelState channel, float fade)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (i >= active.Count) continue;
                AudioVoice v = active[i];
                if (channel == null || v.channel == channel) Stop(v, fade);
            }
        }

        private void ApplyVoice(AudioVoice v)
        {
            ChannelState c = v.channel;
            float volume = v.baseVolume * v.userVolume * v.fade * c.gain * c.policyVolume;
            if (volume != v.appliedVolume)
            {
                v.source.volume = volume;
                v.appliedVolume = volume;
            }
            float pitch = v.basePitch * v.userPitch * c.policyPitch;
            if (pitch != v.appliedPitch)
            {
                v.source.pitch = pitch;
                v.appliedPitch = pitch;
            }
        }

        private static float Remaining(AudioVoice v)
        {
            AudioSource s = v.source;
            float pitch = Mathf.Abs(s.pitch);
            if (s.clip == null || pitch < 0.01f) return float.MaxValue;
            return (s.clip.length - s.time) / pitch;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool ducking = UpdateDucks(dt);
            if (gainsDirty) RecomputeGains();
            if (Audio.Policy != null)
            {
                for (int i = 0; i < channels.Count; i++) UpdatePolicy(channels[i]);
            }

            for (int i = active.Count - 1; i >= 0; i--)
            {
                // Callbacks (music events) may stop other voices while iterating.
                if (i >= active.Count) continue;
                AudioVoice v = active[i];
                if (v.pauseFlags != 0) continue;

                if (!v.started)
                {
                    v.delayLeft -= dt;
                    if (v.delayLeft > 0f) continue;
                    v.started = true;
                    v.source.Play();
                }

                if (v.fading)
                {
                    v.fadeTime += dt;
                    float t = v.fadeTime >= v.fadeDuration ? 1f : v.fadeTime / v.fadeDuration;
                    v.fade = Mathf.Lerp(v.fadeFrom, v.fadeTo, t);
                    if (t >= 1f)
                    {
                        v.fading = false;
                        if (v.stopAfterFade)
                        {
                            Release(v);
                            continue;
                        }
                    }
                }

                if (v.hasFollow)
                {
                    if (v.follow != null)
                    {
                        v.transform.position = v.follow.position;
                    }
                    else
                    {
                        // Target destroyed: one-shots finish where they are, loops stop.
                        v.hasFollow = false;
                        if (v.loop)
                        {
                            Stop(v, v.defaultFadeOut);
                            if (!v.active) continue;
                        }
                    }
                }

                if (!v.source.isPlaying)
                {
                    bool wasCurrentMusic = v.isMusic && musicHandle.Voice == v;
                    Release(v);
                    if (wasCurrentMusic) OnMusicFinished();
                    continue;
                }

                if (!v.loop && !v.stopping)
                {
                    float remaining = Remaining(v);
                    if (playlist != null && v.isMusic && musicHandle.Voice == v && config.defaultCrossfade > 0f && remaining <= config.defaultCrossfade)
                    {
                        AdvancePlaylist();
                    }
                    else if (v.defaultFadeOut > 0f && remaining <= v.defaultFadeOut)
                    {
                        MarkStopping(v);
                        BeginFade(v, 0f, remaining, true);
                    }
                }

                ApplyVoice(v);
            }

            if (active.Count == 0 && !ducking) enabled = false;
        }

        // ---------------------------------------------------------------- music

        internal AudioHandle PlayMusic(CueState cs, float fadeIn, float crossfade, bool fromPlaylist)
        {
            if (!fromPlaylist) playlist = null;
            AudioVoice current = musicHandle.Voice;
            if (!fromPlaylist && !config.restartSameMusic && current != null && !current.stopping && musicId == cs.cue.id)
            {
                return musicHandle;
            }
            if (fadeIn < 0f) fadeIn = config.defaultMusicFadeIn;
            if (crossfade < 0f) crossfade = config.defaultCrossfade;

            ChannelState ch = GetChannel(AudioChannel.MusicName);
            if (Audio.Policy != null && !Audio.Policy.CanPlay(cs.cue.id, ch.channel)) return AudioHandle.Invalid;

            Request r;
            if (!BuildRequest(cs, ch, out r)) return AudioHandle.Invalid;
            if (current != null)
            {
                Stop(current, crossfade);
                fadeIn = crossfade;
            }
            r.spatialBlend = 0f;
            r.priority = 0;
            r.isMusic = true;
            r.fadeIn = Mathf.Max(fadeIn, cs.cue.fadeIn);
            if (fromPlaylist) r.loop = false;

            musicPaused = false;
            musicHandle = StartVoice(ref r);
            musicId = musicHandle.IsValid ? cs.cue.id : null;
            if (musicHandle.IsValid) Audio.RaiseCuePlayed(cs.cue.id, musicHandle);
            Audio.RaiseMusicChanged(musicId);
            return musicHandle;
        }

        internal void StopMusic(float fadeOut)
        {
            playlist = null;
            AudioVoice v = musicHandle.Voice;
            if (v != null) Stop(v, fadeOut < 0f ? config.defaultMusicFadeOut : fadeOut);
            ClearMusic();
        }

        internal void ClearMusic()
        {
            bool changed = musicId != null;
            musicHandle = AudioHandle.Invalid;
            musicId = null;
            musicPaused = false;
            if (changed) Audio.RaiseMusicChanged(null);
        }

        private void OnMusicFinished()
        {
            if (playlist != null) AdvancePlaylist();
            else ClearMusic();
        }

        internal void PlayPlaylist(AudioPlaylist list, bool shuffle)
        {
            int n = list.cueIds.Count;
            if (playlistOrder == null || playlistOrder.Length != n) playlistOrder = new int[n];
            for (int i = 0; i < n; i++) playlistOrder[i] = i;
            if (shuffle) Shuffle(-1);
            playlist = list;
            playlistShuffle = shuffle;
            playlistPos = -1;
            AdvancePlaylist();
        }

        internal void AdvancePlaylist()
        {
            AudioPlaylist list = playlist;
            int n = list.cueIds.Count;
            if (playlistOrder == null || playlistOrder.Length != n) return;
            for (int attempt = 0; attempt < n; attempt++)
            {
                playlistPos++;
                if (playlistPos >= n)
                {
                    if (!list.loop)
                    {
                        StopMusic(-1f);
                        return;
                    }
                    playlistPos = 0;
                    if (playlistShuffle) Shuffle(playlistOrder[n - 1]);
                }
                CueState cs = Audio.LookupCue(list.cueIds[playlistOrder[playlistPos]]);
                if (cs == null) continue;
                if (PlayMusic(cs, -1f, -1f, true).IsValid)
                {
                    playlist = list;
                    return;
                }
            }
            playlist = null;
        }

        private void Shuffle(int avoidFirst)
        {
            int[] order = playlistOrder;
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int t = order[i];
                order[i] = order[j];
                order[j] = t;
            }
            if (order.Length > 1 && order[0] == avoidFirst)
            {
                order[0] = order[1];
                order[1] = avoidFirst;
            }
        }

        // ---------------------------------------------------------------- application / scenes

        private void OnApplicationPause(bool paused)
        {
            if (config == null || !config.pauseOnApplicationPause) return;
            appPaused = paused;
            SetPausedWhere(AudioVoice.PauseApp, appPaused || focusLost, false);
        }

        private void OnApplicationFocus(bool focus)
        {
            if (config == null || !config.pauseOnFocusLost) return;
            focusLost = !focus;
            SetPausedWhere(AudioVoice.PauseApp, appPaused || focusLost, false);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (!config.stopSoundsOnSceneUnload) return;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (i < active.Count && !active[i].isMusic) Release(active[i]);
            }
        }
    }
}
