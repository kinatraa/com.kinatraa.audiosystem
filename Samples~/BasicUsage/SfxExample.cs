using UnityEngine;

namespace kinatraa.AudioSystem.Samples
{
    /// <summary>
    /// Sound effects: 2D one-shot, 3D one-shot at a position, a sound following this object,
    /// and a loop controlled through its handle.
    /// </summary>
    public class SfxExample : MonoBehaviour
    {
        /// <summary>A short UI sound.</summary>
        [AudioCueId] public string clickCue = "ui_click";

        /// <summary>A 3D one-shot.</summary>
        [AudioCueId] public string explosionCue = "explosion";

        /// <summary>A looping cue.</summary>
        [AudioCueId] public string loopCue = "engine_loop";

        private AudioHandle loop;
        private float pitch = 1f;

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 260, 220), "SFX", GUI.skin.window);
            if (GUILayout.Button("Play 2D")) Audio.Play(clickCue);
            if (GUILayout.Button("Play 3D at (5, 0, 0)")) Audio.Play(explosionCue, new Vector3(5f, 0f, 0f));
            if (GUILayout.Button("Play following this object")) Audio.Play(explosionCue, transform);

            if (!loop.IsValid)
            {
                if (GUILayout.Button("Start loop")) loop = Audio.Play(loopCue, transform);
            }
            else
            {
                if (GUILayout.Button("Stop loop (1 s fade)")) loop.Stop(1f);
                if (GUILayout.Button(loop.IsPaused ? "Resume loop" : "Pause loop"))
                {
                    if (loop.IsPaused) loop.Resume();
                    else loop.Pause();
                }
                GUILayout.Label("Pitch " + pitch.ToString("0.00"));
                pitch = GUILayout.HorizontalSlider(pitch, 0.5f, 1.5f);
                loop.SetPitch(pitch);
            }
            GUILayout.EndArea();
        }
    }
}
