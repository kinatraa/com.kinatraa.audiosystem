using System;
using UnityEngine;

namespace kinatraa.AudioSystem
{
    /// <summary>
    /// Lightweight identifier of an audio channel (a volume bus such as Music or SFX).
    /// Built-in channels are exposed as static fields; custom channels are created from any name,
    /// usually one defined in <see cref="AudioSystemConfig.channels"/>. An empty channel resolves to <see cref="SFX"/>.
    /// </summary>
    [Serializable]
    public struct AudioChannel : IEquatable<AudioChannel>
    {
        /// <summary>Name of the built-in Master channel.</summary>
        public const string MasterName = "Master";
        /// <summary>Name of the built-in Music channel.</summary>
        public const string MusicName = "Music";
        /// <summary>Name of the built-in SFX channel.</summary>
        public const string SfxName = "SFX";
        /// <summary>Name of the built-in UI channel.</summary>
        public const string UiName = "UI";
        /// <summary>Name of the built-in Voice channel.</summary>
        public const string VoiceName = "Voice";
        /// <summary>Name of the built-in Ambient channel.</summary>
        public const string AmbientName = "Ambient";

        /// <summary>Master channel. Its volume scales every other channel.</summary>
        public static readonly AudioChannel Master = new AudioChannel(MasterName);
        /// <summary>Music channel, used by <see cref="Audio.PlayMusic"/> and playlists.</summary>
        public static readonly AudioChannel Music = new AudioChannel(MusicName);
        /// <summary>Sound effects channel (the default channel).</summary>
        public static readonly AudioChannel SFX = new AudioChannel(SfxName);
        /// <summary>User interface sounds channel.</summary>
        public static readonly AudioChannel UI = new AudioChannel(UiName);
        /// <summary>Dialogue / voice-over channel.</summary>
        public static readonly AudioChannel Voice = new AudioChannel(VoiceName);
        /// <summary>Ambience channel.</summary>
        public static readonly AudioChannel Ambient = new AudioChannel(AmbientName);

        internal static readonly string[] BuiltInNames = { MasterName, MusicName, SfxName, UiName, VoiceName, AmbientName };

        [SerializeField] private string name;

        /// <summary>Creates a channel id from a name. Names are case-sensitive.</summary>
        /// <param name="name">Channel name, e.g. "Music" or a custom name defined in the config.</param>
        public AudioChannel(string name)
        {
            this.name = name;
        }

        /// <summary>Channel name. Returns "SFX" for a default/empty channel.</summary>
        public string Name
        {
            get { return string.IsNullOrEmpty(name) ? SfxName : name; }
        }

        /// <summary>Converts a channel name to an <see cref="AudioChannel"/>.</summary>
        /// <param name="name">Channel name.</param>
        public static implicit operator AudioChannel(string name)
        {
            return new AudioChannel(name);
        }

        /// <summary>Ordinal name comparison.</summary>
        /// <param name="other">Channel to compare with.</param>
        public bool Equals(AudioChannel other)
        {
            return string.Equals(Name, other.Name, StringComparison.Ordinal);
        }

        /// <summary>Ordinal name comparison.</summary>
        /// <param name="obj">Object to compare with.</param>
        public override bool Equals(object obj)
        {
            return obj is AudioChannel && Equals((AudioChannel)obj);
        }

        /// <summary>Hash of the channel name.</summary>
        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Name);
        }

        /// <summary>Equality operator.</summary>
        /// <param name="a">First channel.</param>
        /// <param name="b">Second channel.</param>
        public static bool operator ==(AudioChannel a, AudioChannel b)
        {
            return a.Equals(b);
        }

        /// <summary>Inequality operator.</summary>
        /// <param name="a">First channel.</param>
        /// <param name="b">Second channel.</param>
        public static bool operator !=(AudioChannel a, AudioChannel b)
        {
            return !a.Equals(b);
        }

        /// <summary>Returns the channel name.</summary>
        public override string ToString()
        {
            return Name;
        }
    }
}
