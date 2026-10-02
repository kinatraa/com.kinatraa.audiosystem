using System;
using UnityEditor;
using UnityEngine;

namespace kinatraa.AudioSystem.Editor
{
    /// <summary>Play-mode view of voices, pool usage, channel volumes, ducking and music state.</summary>
    internal sealed class AudioDebuggerWindow : EditorWindow
    {
        private Vector2 scroll;
        private Action pending;

        [MenuItem(AudioEditorUtility.MenuRoot + "Runtime Debugger", false, 1)]
        internal static void Open()
        {
            GetWindow<AudioDebuggerWindow>("Audio Debugger").Show();
        }

        private void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying) Repaint();
        }

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect the audio system.", MessageType.Info);
                return;
            }
            AudioRuntime rt = Audio.Runtime;
            if (rt == null)
            {
                EditorGUILayout.HelpBox("The audio system is not initialized.", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawPool(rt);
            DrawMusic(rt);
            DrawChannels(rt);
            DrawVoices(rt);
            EditorGUILayout.EndScrollView();

            if (pending != null)
            {
                Action action = pending;
                pending = null;
                action();
            }
        }

        private static void DrawPool(AudioRuntime rt)
        {
            EditorGUILayout.LabelField("Pool", EditorStyles.boldLabel);
            int max = Mathf.Max(1, rt.config.poolMaxSize);
            Rect bar = GUILayoutUtility.GetRect(18f, 18f, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(bar, (float)rt.active.Count / max,
                rt.active.Count + " active / " + rt.created + " created / " + max + " max (" + rt.free.Count + " free)");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(rt.globalPaused ? "Resume All" : "Pause All"))
            {
                if (rt.globalPaused) Audio.ResumeAll();
                else Audio.PauseAll();
            }
            if (GUILayout.Button("Stop All")) Audio.StopAll();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMusic(AudioRuntime rt)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Music", EditorStyles.boldLabel);
            string state = rt.musicId == null ? "stopped" : rt.musicId + (rt.musicPaused ? " (paused)" : "");
            EditorGUILayout.LabelField("Current", state);
            EditorGUILayout.LabelField("Playlist", rt.playlist != null ? rt.playlist.id : "-");
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = rt.musicId != null;
            if (GUILayout.Button(rt.musicPaused ? "Resume" : "Pause"))
            {
                if (rt.musicPaused) Audio.ResumeMusic();
                else Audio.PauseMusic();
            }
            if (GUILayout.Button("Stop")) pending = () => Audio.StopMusic();
            GUI.enabled = rt.playlist != null;
            if (GUILayout.Button("Next Track")) pending = Audio.NextTrack;
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawChannels(AudioRuntime rt)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Channels", EditorStyles.boldLabel);
            for (int i = 0; i < rt.channels.Count; i++)
            {
                ChannelState c = rt.channels[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(c.name, GUILayout.Width(80f));
                float volume = EditorGUILayout.Slider(c.volume, 0f, 1f);
                if (volume != c.volume) Audio.SetVolume(c.channel, volume);
                bool muted = GUILayout.Toggle(c.muted, "Mute", EditorStyles.miniButton, GUILayout.Width(44f));
                if (muted != c.muted) Audio.SetMuted(c.channel, muted);
                string info = c.duck < 1f || c.duckTarget < 1f
                    ? "duck " + c.duck.ToString("0.00") + (c.duckTimed ? " (" + c.duckTimeLeft.ToString("0.0") + "s)" : "")
                    : "";
                if (c.UsesMixer) info += (info.Length > 0 ? ", " : "") + c.mixerParam + " " + c.appliedDb.ToString("0.0") + "dB";
                EditorGUILayout.LabelField(info, EditorStyles.miniLabel, GUILayout.Width(150f));
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawVoices(AudioRuntime rt)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Active Voices (" + rt.active.Count + ")", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Cue", GUILayout.Width(150f));
            GUILayout.Label("Channel", GUILayout.Width(70f));
            GUILayout.Label("Volume", GUILayout.Width(50f));
            GUILayout.Label("Time Left", GUILayout.Width(70f));
            GUILayout.Label("State");
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < rt.active.Count; i++)
            {
                AudioVoice v = rt.active[i];
                AudioSource s = v.source;
                string name = v.cue != null ? v.cue.cue.id : (s.clip != null ? s.clip.name + " (clip)" : "?");
                string timeLeft = v.loop ? "loop" : s.clip != null ? ((s.clip.length - s.time) / Mathf.Max(0.01f, Mathf.Abs(s.pitch))).ToString("0.0") + "s" : "-";
                string state = !v.started ? "delayed" : v.pauseFlags != 0 ? "paused" : v.stopping ? "stopping" : v.fading ? "fading" : "playing";
                if (v.isMusic) state += ", music";

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(name, GUILayout.Width(150f));
                GUILayout.Label(v.channel != null ? v.channel.name : "-", GUILayout.Width(70f));
                GUILayout.Label(s.volume.ToString("0.00"), GUILayout.Width(50f));
                GUILayout.Label(timeLeft, GUILayout.Width(70f));
                GUILayout.Label(state);
                if (GUILayout.Button("Stop", EditorStyles.miniButton, GUILayout.Width(40f)))
                {
                    AudioVoice captured = v;
                    pending = () => Audio.StopVoice(captured, 0f);
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
