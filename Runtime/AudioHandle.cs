using System;

namespace kinatraa.AudioSystem
{
    /// <summary>
    /// Controls one playing sound. Handles are safe to keep: once the sound ends and its AudioSource is reused,
    /// the handle becomes invalid and every call on it is a no-op.
    /// </summary>
    public readonly struct AudioHandle : IEquatable<AudioHandle>
    {
        private readonly AudioVoice voice;
        private readonly int generation;

        /// <summary>A handle that refers to nothing.</summary>
        public static readonly AudioHandle Invalid = default(AudioHandle);

        internal AudioHandle(AudioVoice voice)
        {
            this.voice = voice;
            generation = voice.generation;
        }

        internal AudioVoice Voice
        {
            get { return IsValid ? voice : null; }
        }

        /// <summary>True while the sound exists (playing, paused, delayed or fading out).</summary>
        public bool IsValid
        {
            get { return voice != null && voice.active && voice.generation == generation; }
        }

        /// <summary>True while the sound exists and is not paused.</summary>
        public bool IsPlaying
        {
            get { return IsValid && voice.pauseFlags == 0; }
        }

        /// <summary>True while the sound is paused.</summary>
        public bool IsPaused
        {
            get { return IsValid && voice.pauseFlags != 0; }
        }

        /// <summary>Id of the cue, or null for clip playback / invalid handles.</summary>
        public string CueId
        {
            get { return IsValid && voice.cue != null ? voice.cue.cue.id : null; }
        }

        /// <summary>Stops the sound using the cue's fade-out time.</summary>
        public void Stop()
        {
            if (IsValid) Audio.StopVoice(voice, voice.defaultFadeOut);
        }

        /// <summary>Stops the sound.</summary>
        /// <param name="fadeSeconds">Fade-out duration; 0 stops immediately.</param>
        public void Stop(float fadeSeconds)
        {
            if (IsValid) Audio.StopVoice(voice, fadeSeconds);
        }

        /// <summary>Sets a volume multiplier on top of the cue and channel volume.</summary>
        /// <param name="volume">Multiplier, usually 0..1.</param>
        public void SetVolume(float volume)
        {
            if (IsValid) voice.userVolume = volume < 0f ? 0f : volume;
        }

        /// <summary>Sets a pitch multiplier on top of the cue pitch.</summary>
        /// <param name="pitch">Multiplier.</param>
        public void SetPitch(float pitch)
        {
            if (IsValid) voice.userPitch = pitch;
        }

        /// <summary>Pauses the sound.</summary>
        public void Pause()
        {
            if (IsValid) Audio.SetPaused(voice, AudioVoice.PauseUser, true);
        }

        /// <summary>Resumes a sound paused with <see cref="Pause"/>.</summary>
        public void Resume()
        {
            if (IsValid) Audio.SetPaused(voice, AudioVoice.PauseUser, false);
        }

        /// <summary>Same voice and generation.</summary>
        /// <param name="other">Handle to compare with.</param>
        public bool Equals(AudioHandle other)
        {
            return voice == other.voice && generation == other.generation;
        }

        /// <summary>Same voice and generation.</summary>
        /// <param name="obj">Object to compare with.</param>
        public override bool Equals(object obj)
        {
            return obj is AudioHandle && Equals((AudioHandle)obj);
        }

        /// <summary>Hash code.</summary>
        public override int GetHashCode()
        {
            return (voice != null ? voice.GetHashCode() : 0) * 397 ^ generation;
        }
    }
}
