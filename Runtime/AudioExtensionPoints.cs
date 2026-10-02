using UnityEngine;

namespace kinatraa.AudioSystem
{
    /// <summary>
    /// Persists channel volumes and mute states. Implement it to plug the audio system into a game's own save system,
    /// then call <see cref="Audio.SetSettingsStore"/>. Keys are channel names such as "Music" or "SFX".
    /// </summary>
    public interface IAudioSettingsStore
    {
        /// <summary>Loads a saved volume (0..1).</summary>
        /// <param name="key">Channel name.</param>
        /// <param name="value">Saved volume.</param>
        /// <returns>True if a value exists.</returns>
        bool TryLoad(string key, out float value);

        /// <summary>Saves a volume (0..1).</summary>
        /// <param name="key">Channel name.</param>
        /// <param name="value">Volume.</param>
        void Save(string key, float value);

        /// <summary>Loads a saved mute state.</summary>
        /// <param name="key">Channel name.</param>
        /// <param name="muted">Saved mute state.</param>
        /// <returns>True if a value exists.</returns>
        bool TryLoadMuted(string key, out bool muted);

        /// <summary>Saves a mute state.</summary>
        /// <param name="key">Channel name.</param>
        /// <param name="muted">Mute state.</param>
        void SaveMuted(string key, bool muted);
    }

    /// <summary>
    /// Resolves the clip to play for a cue. Implement it to load clips through Addressables, asset bundles,
    /// Resources or anything else, then call <see cref="Audio.SetClipProvider"/>. Clips must be available synchronously
    /// (preload them before playing).
    /// </summary>
    public interface IAudioClipProvider
    {
        /// <summary>Returns the clip for a cue variation.</summary>
        /// <param name="cue">Cue being played.</param>
        /// <param name="clipIndex">Index picked by the cue's play mode, in range of <see cref="AudioCue.clips"/>.</param>
        /// <returns>The clip to play, or null to skip playback.</returns>
        AudioClip GetClip(AudioCue cue, int clipIndex);
    }

    /// <summary>
    /// Game-wide playback rules, e.g. "no SFX during cutscenes", slow-motion pitch, or muting while unfocused.
    /// Set it with <see cref="Audio.SetPolicy"/>. The multipliers are read every frame while sounds play.
    /// </summary>
    public interface IAudioPolicy
    {
        /// <summary>Called before every play. Return false to veto it.</summary>
        /// <param name="cueId">Cue id, or null for <see cref="Audio.PlayClip(AudioClip)"/>.</param>
        /// <param name="channel">Channel the sound would play on.</param>
        bool CanPlay(string cueId, AudioChannel channel);

        /// <summary>Extra volume multiplier for a channel (1 = unchanged).</summary>
        /// <param name="channel">Channel.</param>
        float GetVolumeMultiplier(AudioChannel channel);

        /// <summary>Extra pitch multiplier for a channel (1 = unchanged), e.g. Time.timeScale for slow motion.</summary>
        /// <param name="channel">Channel.</param>
        float GetPitchMultiplier(AudioChannel channel);
    }

    internal sealed class DefaultClipProvider : IAudioClipProvider
    {
        public static readonly DefaultClipProvider Instance = new DefaultClipProvider();

        public AudioClip GetClip(AudioCue cue, int clipIndex)
        {
            return cue.clips[clipIndex];
        }
    }
}
