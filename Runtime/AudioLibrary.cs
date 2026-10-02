using System;
using System.Collections.Generic;
using UnityEngine;

namespace kinatraa.AudioSystem
{
    /// <summary>A named, ordered list of music cue ids played by <see cref="Audio.PlayPlaylist"/>.</summary>
    [Serializable]
    public class AudioPlaylist
    {
        /// <summary>Playlist id used with <see cref="Audio.PlayPlaylist"/>.</summary>
        public string id = "";

        /// <summary>Ids of the cues to play, in order.</summary>
        [AudioCueId] public List<string> cueIds = new List<string>();

        /// <summary>Start over after the last track.</summary>
        public bool loop = true;
    }

    /// <summary>
    /// A collection of <see cref="AudioCue"/>s and <see cref="AudioPlaylist"/>s.
    /// Several libraries can be registered at the same time (e.g. a core library plus one per level).
    /// </summary>
    [CreateAssetMenu(menuName = "kinatraa/Audio/Audio Library", fileName = "AudioLibrary", order = 0)]
    public class AudioLibrary : ScriptableObject
    {
        /// <summary>Cues in this library.</summary>
        public List<AudioCue> cues = new List<AudioCue>();

        /// <summary>Playlists in this library.</summary>
        public List<AudioPlaylist> playlists = new List<AudioPlaylist>();

        /// <summary>Linear search for a cue by id. Use <see cref="Audio.TryGetCue"/> at runtime.</summary>
        /// <param name="cueId">Cue id.</param>
        /// <returns>The cue, or null.</returns>
        public AudioCue FindCue(string cueId)
        {
            for (int i = 0; i < cues.Count; i++)
            {
                if (cues[i] != null && cues[i].id == cueId) return cues[i];
            }
            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            Audio.NotifyLibraryChanged(this);
        }
#endif
    }
}
