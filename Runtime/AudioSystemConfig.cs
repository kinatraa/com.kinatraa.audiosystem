using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace kinatraa.AudioSystem
{
    /// <summary>Console verbosity of the audio system.</summary>
    public enum AudioLogLevel
    {
        /// <summary>Log nothing.</summary>
        None,
        /// <summary>Errors only.</summary>
        Error,
        /// <summary>Errors and warnings.</summary>
        Warning,
        /// <summary>Everything, including informational messages.</summary>
        Info
    }

    /// <summary>Definition of one channel (volume bus) in the config.</summary>
    [Serializable]
    public class AudioChannelDefinition
    {
        /// <summary>Channel name, e.g. "Music". Also used as the settings key.</summary>
        public string name = "";

        /// <summary>Volume (0..1) used when no saved setting exists.</summary>
        [Range(0f, 1f)] public float defaultVolume = 1f;

        /// <summary>Optional mixer group that sources on this channel are routed to.</summary>
        public AudioMixerGroup mixerGroup;

        /// <summary>
        /// Optional exposed AudioMixer parameter (in dB) that receives this channel's volume.
        /// When empty, the volume multiplies AudioSource volumes directly.
        /// </summary>
        public string mixerVolumeParameter = "";

        /// <summary>Creates an empty channel definition.</summary>
        public AudioChannelDefinition()
        {
        }

        /// <summary>Creates a channel definition.</summary>
        /// <param name="name">Channel name.</param>
        /// <param name="defaultVolume">Default volume 0..1.</param>
        public AudioChannelDefinition(string name, float defaultVolume)
        {
            this.name = name;
            this.defaultVolume = defaultVolume;
        }

        /// <summary>Creates the six built-in channel definitions.</summary>
        public static List<AudioChannelDefinition> CreateDefaults()
        {
            var list = new List<AudioChannelDefinition>();
            for (int i = 0; i < AudioChannel.BuiltInNames.Length; i++)
            {
                list.Add(new AudioChannelDefinition(AudioChannel.BuiltInNames[i], 1f));
            }
            return list;
        }
    }

    /// <summary>
    /// Global settings of the audio system. Put one at <c>Resources/kinatraaAudioConfig</c> for auto-bootstrap,
    /// or pass one to <see cref="Audio.Initialize"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "kinatraa/Audio/Audio System Config", fileName = "kinatraaAudioConfig", order = 1)]
    public class AudioSystemConfig : ScriptableObject
    {
        /// <summary>Resources path loaded by the auto-bootstrap.</summary>
        public const string ResourcesPath = "kinatraaAudioConfig";

        /// <summary>Initialize automatically before the first scene loads.</summary>
        [Header("Startup")]
        [Tooltip("Initialize automatically before the first scene loads. Disable to call Audio.Initialize yourself.")]
        public bool autoInitialize = true;

        /// <summary>Libraries registered on initialization.</summary>
        public List<AudioLibrary> libraries = new List<AudioLibrary>();

        /// <summary>Channel definitions. Channels not listed here still work with volume 1.</summary>
        [Header("Channels")]
        public List<AudioChannelDefinition> channels = AudioChannelDefinition.CreateDefaults();

        /// <summary>Fallback mixer for channels with a volume parameter but no mixer group.</summary>
        [Tooltip("Optional. Used for channels that have a mixer volume parameter but no mixer group.")]
        public AudioMixer mixer;

        /// <summary>dB value written to the mixer for volume 0.</summary>
        public float silenceDecibels = -80f;

        /// <summary>dB value written to the mixer for volume 1.</summary>
        public float fullVolumeDecibels = 0f;

        /// <summary>AudioSources created at startup.</summary>
        [Header("Pool")]
        [Min(0)] public int poolInitialSize = 8;

        /// <summary>Maximum AudioSources. When all are busy, the least important voice is stolen or the play is rejected.</summary>
        [Min(1)] public int poolMaxSize = 32;

        /// <summary>Default music fade-in in seconds when nothing else is playing.</summary>
        [Header("Music")]
        [Min(0f)] public float defaultMusicFadeIn = 0.5f;

        /// <summary>Default music crossfade in seconds.</summary>
        [Min(0f)] public float defaultCrossfade = 1f;

        /// <summary>Default music fade-out in seconds.</summary>
        [Min(0f)] public float defaultMusicFadeOut = 1f;

        /// <summary>Restart the music cue when <see cref="Audio.PlayMusic"/> is called with the cue already playing.</summary>
        public bool restartSameMusic;

        /// <summary>Load and save channel volumes through the settings store (PlayerPrefs by default).</summary>
        [Header("Settings")]
        public bool persistSettings = true;

        /// <summary>Key prefix used by the default PlayerPrefs store.</summary>
        public string settingsKeyPrefix = "kinatraa.audio.";

        /// <summary>Pause all sounds while the application is paused (mobile background).</summary>
        [Header("Behaviour")]
        public bool pauseOnApplicationPause = true;

        /// <summary>Pause all sounds while the application window has no focus.</summary>
        public bool pauseOnFocusLost;

        /// <summary>Stop every non-music sound when a scene is unloaded.</summary>
        public bool stopSoundsOnSceneUnload;

        /// <summary>Hide the runtime root object in the hierarchy.</summary>
        public bool hideInHierarchy = true;

        /// <summary>Console verbosity.</summary>
        public AudioLogLevel logLevel = AudioLogLevel.Warning;

        /// <summary>Asset path of the generated ids file.</summary>
        [Header("Editor: Ids Generator")]
        public string idsOutputPath = "Assets/Scripts/Generated/AudioIds.cs";

        /// <summary>Namespace of the generated ids class. Empty = global namespace.</summary>
        public string idsNamespace = "";

        /// <summary>Name of the generated ids class.</summary>
        public string idsClassName = "AudioIds";

        /// <summary>Converts a linear 0..1 volume to decibels using this config's range.</summary>
        /// <param name="volume">Linear volume.</param>
        public float ToDecibels(float volume)
        {
            if (volume <= 0.0001f) return silenceDecibels;
            return Mathf.Max(silenceDecibels, Mathf.Log10(volume) * 20f + fullVolumeDecibels);
        }
    }
}
