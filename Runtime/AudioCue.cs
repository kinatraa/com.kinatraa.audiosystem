using System;
using System.Collections.Generic;
using UnityEngine;

namespace kinatraa.AudioSystem
{
    /// <summary>How a cue picks a clip from its clip list.</summary>
    public enum AudioClipPlayMode
    {
        /// <summary>Any clip at random.</summary>
        Random,
        /// <summary>Random, but never the same clip twice in a row.</summary>
        RandomNoRepeat,
        /// <summary>Clips in list order, wrapping around.</summary>
        Sequential,
        /// <summary>Always the first clip.</summary>
        First
    }

    /// <summary>What happens when a cue already plays its maximum number of instances.</summary>
    public enum AudioStealPolicy
    {
        /// <summary>Stop the oldest instance and play the new one.</summary>
        Oldest,
        /// <summary>Stop the quietest instance and play the new one.</summary>
        Quietest,
        /// <summary>Do not play the new instance.</summary>
        Reject
    }

    /// <summary>
    /// A playable sound definition stored in an <see cref="AudioLibrary"/>, addressed by <see cref="id"/>.
    /// </summary>
    [Serializable]
    public class AudioCue
    {
        /// <summary>Unique id used with <see cref="Audio.Play(string)"/>. Snake_case is recommended.</summary>
        [Tooltip("Unique id used with Audio.Play(id).")]
        public string id = "";

        /// <summary>Clip variations. One is picked per play according to <see cref="playMode"/>.</summary>
        public List<AudioClip> clips = new List<AudioClip>();

        /// <summary>How a clip is picked from <see cref="clips"/>.</summary>
        public AudioClipPlayMode playMode = AudioClipPlayMode.RandomNoRepeat;

        /// <summary>Channel whose volume applies to this cue.</summary>
        public AudioChannel channel = AudioChannel.SFX;

        /// <summary>Random volume range (x = min, y = max), 0..1.</summary>
        public Vector2 volumeRange = new Vector2(1f, 1f);

        /// <summary>Random pitch range (x = min, y = max).</summary>
        public Vector2 pitchRange = new Vector2(1f, 1f);

        /// <summary>Loop the clip until stopped.</summary>
        public bool loop;

        /// <summary>0 = 2D, 1 = fully 3D. Only used when played at a position or following a transform.</summary>
        [Range(0f, 1f)] public float spatialBlend = 1f;

        /// <summary>3D: distance at which attenuation starts.</summary>
        public float minDistance = 1f;

        /// <summary>3D: distance at which attenuation stops.</summary>
        public float maxDistance = 50f;

        /// <summary>3D: attenuation curve.</summary>
        public AudioRolloffMode rolloff = AudioRolloffMode.Logarithmic;

        /// <summary>Unity voice priority: 0 = most important, 256 = least. Also used when the pool must steal a voice.</summary>
        [Range(0, 256)] public int priority = 128;

        /// <summary>Minimum seconds between two plays of this cue (anti-spam). 0 = none.</summary>
        public float cooldown;

        /// <summary>Maximum simultaneous instances. 0 = unlimited.</summary>
        public int maxInstances;

        /// <summary>What to do when <see cref="maxInstances"/> is reached.</summary>
        public AudioStealPolicy stealPolicy = AudioStealPolicy.Oldest;

        /// <summary>Seconds to wait before the sound starts.</summary>
        public float startDelay;

        /// <summary>Fade-in duration in seconds. 0 = none.</summary>
        public float fadeIn;

        /// <summary>Fade-out duration in seconds used by <see cref="AudioHandle.Stop()"/> and at the natural end of the clip. 0 = none.</summary>
        public float fadeOut;

        /// <summary>Free-form tags used for filtering in the editor.</summary>
        public List<string> tags = new List<string>();
    }
}
