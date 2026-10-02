using UnityEngine;

namespace kinatraa.AudioSystem
{
    /// <summary>
    /// Settings for <see cref="Audio.PlayClip(AudioClip, AudioPlayOptions)"/>. A default instance (<c>new AudioPlayOptions()</c>)
    /// is valid: volume 1, pitch 1, SFX channel, 2D, priority 128.
    /// </summary>
    public struct AudioPlayOptions
    {
        // Stored as offsets from the defaults so that default(AudioPlayOptions) is usable.
        private float volumeOffset;
        private float pitchOffset;
        private int priorityOffset;
        private float minDistanceOffset;
        private float maxDistanceOffset;
        private float spatialBlend;
        private bool spatialBlendSet;
        private Vector3 position;
        private bool hasPosition;
        private Transform follow;

        /// <summary>Default options.</summary>
        public static AudioPlayOptions Default
        {
            get { return new AudioPlayOptions(); }
        }

        /// <summary>Channel to play on. Default SFX.</summary>
        public AudioChannel Channel { get; set; }

        /// <summary>Volume 0..1. Default 1.</summary>
        public float Volume
        {
            get { return 1f - volumeOffset; }
            set { volumeOffset = 1f - value; }
        }

        /// <summary>Pitch. Default 1.</summary>
        public float Pitch
        {
            get { return 1f - pitchOffset; }
            set { pitchOffset = 1f - value; }
        }

        /// <summary>Loop until stopped. Default false.</summary>
        public bool Loop { get; set; }

        /// <summary>0 = 2D, 1 = 3D. Defaults to 1 when a position or follow target is set, otherwise 0.</summary>
        public float SpatialBlend
        {
            get { return spatialBlendSet ? spatialBlend : (hasPosition || follow != null ? 1f : 0f); }
            set { spatialBlend = value; spatialBlendSet = true; }
        }

        /// <summary>World position for 3D playback. Setting it enables positional playback.</summary>
        public Vector3 Position
        {
            get { return position; }
            set { position = value; hasPosition = true; }
        }

        /// <summary>True when <see cref="Position"/> was set.</summary>
        public bool HasPosition
        {
            get { return hasPosition; }
        }

        /// <summary>Transform the sound follows while playing.</summary>
        public Transform Follow
        {
            get { return follow; }
            set { follow = value; }
        }

        /// <summary>3D attenuation start distance. Default 1.</summary>
        public float MinDistance
        {
            get { return 1f - minDistanceOffset; }
            set { minDistanceOffset = 1f - value; }
        }

        /// <summary>3D attenuation end distance. Default 50.</summary>
        public float MaxDistance
        {
            get { return 50f - maxDistanceOffset; }
            set { maxDistanceOffset = 50f - value; }
        }

        /// <summary>3D attenuation curve. Default logarithmic.</summary>
        public AudioRolloffMode Rolloff { get; set; }

        /// <summary>Unity voice priority, 0 = most important. Default 128.</summary>
        public int Priority
        {
            get { return 128 - priorityOffset; }
            set { priorityOffset = 128 - value; }
        }

        /// <summary>Seconds before the sound starts. Default 0.</summary>
        public float Delay { get; set; }

        /// <summary>Fade-in seconds. Default 0.</summary>
        public float FadeIn { get; set; }
    }
}
