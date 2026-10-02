using UnityEngine;

namespace kinatraa.AudioSystem.Samples
{
    /// <summary>
    /// A settings panel: one slider and mute toggle per channel, hooked to Audio.SetVolume / Audio.SetMuted.
    /// Values are saved by the active settings store (PlayerPrefs unless replaced).
    /// </summary>
    public class VolumeSettingsPanel : MonoBehaviour
    {
        private static readonly AudioChannel[] Channels =
        {
            AudioChannel.Master, AudioChannel.Music, AudioChannel.SFX, AudioChannel.UI, AudioChannel.Voice, AudioChannel.Ambient
        };

        private bool paused;

        private void OnEnable()
        {
            Audio.OnVolumeChanged += LogVolume;
        }

        private void OnDisable()
        {
            Audio.OnVolumeChanged -= LogVolume;
        }

        private static void LogVolume(AudioChannel channel, float volume)
        {
            Debug.Log("Volume " + channel + " = " + volume.ToString("0.00"));
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(550, 10, 300, 260), "Volume", GUI.skin.window);
            foreach (AudioChannel channel in Channels)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(channel.Name, GUILayout.Width(60));
                Audio.SetVolume(channel, GUILayout.HorizontalSlider(Audio.GetVolume(channel), 0f, 1f, GUILayout.Width(150)));
                Audio.SetMuted(channel, GUILayout.Toggle(Audio.IsMuted(channel), "Mute"));
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button(paused ? "Resume game audio" : "Pause game audio (pause menu)"))
            {
                paused = !paused;
                if (paused) Audio.PauseAll();
                else Audio.ResumeAll();
            }
            GUILayout.EndArea();
        }
    }
}
