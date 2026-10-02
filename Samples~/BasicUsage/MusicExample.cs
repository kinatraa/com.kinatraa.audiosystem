using UnityEngine;

namespace kinatraa.AudioSystem.Samples
{
    /// <summary>Music: crossfade between two tracks, pause, stop, playlist and ducking for a voice line.</summary>
    public class MusicExample : MonoBehaviour
    {
        /// <summary>First music cue.</summary>
        [AudioCueId] public string calmMusic = "music_calm";

        /// <summary>Second music cue.</summary>
        [AudioCueId] public string battleMusic = "music_battle";

        /// <summary>A playlist id from a library.</summary>
        public string playlistId = "main_playlist";

        /// <summary>Voice line played while the music is ducked.</summary>
        [AudioCueId] public string voiceLine = "voice_hello";

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(280, 10, 260, 260), "Music", GUI.skin.window);
            GUILayout.Label("Now playing: " + (Audio.CurrentMusicId ?? "-"));
            if (GUILayout.Button("Calm (crossfade 1.5 s)")) Audio.PlayMusic(calmMusic, fadeIn: 1f, crossfade: 1.5f);
            if (GUILayout.Button("Battle (crossfade 1.5 s)")) Audio.PlayMusic(battleMusic, fadeIn: 1f, crossfade: 1.5f);
            if (GUILayout.Button("Pause")) Audio.PauseMusic();
            if (GUILayout.Button("Resume")) Audio.ResumeMusic();
            if (GUILayout.Button("Stop (1 s fade)")) Audio.StopMusic(fadeOut: 1f);
            if (GUILayout.Button("Shuffle playlist")) Audio.PlayPlaylist(playlistId, shuffle: true);
            if (GUILayout.Button("Voice line (duck music)"))
            {
                AudioHandle voice = Audio.Play(voiceLine);
                if (voice.IsValid) Audio.Duck(AudioChannel.Music, 0.3f, duration: 2f, fade: 0.25f);
            }
            GUILayout.EndArea();
        }
    }
}
