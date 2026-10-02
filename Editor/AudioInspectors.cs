using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace kinatraa.AudioSystem.Editor
{
    /// <summary>Config inspector with validation and shortcuts.</summary>
    [CustomEditor(typeof(AudioSystemConfig))]
    internal sealed class AudioSystemConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var config = (AudioSystemConfig)target;
            if (AssetDatabase.GetAssetPath(config) != AudioEditorUtility.DefaultConfigPath)
            {
                EditorGUILayout.HelpBox("Only a config at Resources/" + AudioSystemConfig.ResourcesPath +
                    " is loaded automatically. Otherwise call Audio.Initialize(config).", MessageType.Info);
            }

            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            if (serializedObject.ApplyModifiedProperties()) AudioEditorUtility.Invalidate();

            foreach (AudioIssue issue in AudioValidator.ValidateConfig(config)) EditorGUILayout.HelpBox(issue.message, issue.type);

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate Setup")) AudioSetup.ValidateSetup();
            if (GUILayout.Button("Generate Ids")) AudioIdsGenerator.GenerateAndReport();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Library Window")) AudioLibraryWindow.Open();
            if (GUILayout.Button("Runtime Debugger")) AudioDebuggerWindow.Open();
            EditorGUILayout.EndHorizontal();
        }
    }

    /// <summary>Emitter inspector with cue validation, edit-mode preview and play-mode controls.</summary>
    [CustomEditor(typeof(AudioEmitter))]
    [CanEditMultipleObjects]
    internal sealed class AudioEmitterEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            var emitter = (AudioEmitter)target;
            if (string.IsNullOrEmpty(emitter.cueId))
            {
                EditorGUILayout.HelpBox("No cue selected.", MessageType.Warning);
            }
            else if (!AudioEditorUtility.HasCue(emitter.cueId))
            {
                EditorGUILayout.HelpBox("No library in the project contains the cue '" + emitter.cueId + "'.", MessageType.Error);
            }
            if (emitter.playOn == AudioEmitter.PlayTrigger.OnTriggerEnter && !HasTriggerCollider(emitter.gameObject))
            {
                EditorGUILayout.HelpBox("OnTriggerEnter needs a trigger Collider or Collider2D on this GameObject.", MessageType.Warning);
            }

            EditorGUILayout.BeginHorizontal();
            if (EditorApplication.isPlaying)
            {
                if (GUILayout.Button("Play")) emitter.Play();
                if (GUILayout.Button("Stop")) emitter.Stop();
            }
            else
            {
                AudioCue cue = AudioEditorUtility.FindCue(emitter.cueId);
                GUI.enabled = cue != null;
                bool previewing = AudioEditorUtility.IsPreviewing(cue);
                if (GUILayout.Button(previewing ? "Stop Preview" : "Preview"))
                {
                    if (previewing) AudioEditorUtility.StopPreview();
                    else AudioEditorUtility.Preview(cue);
                }
                GUI.enabled = true;
            }
            EditorGUILayout.EndHorizontal();
        }

        // Resolved by type name so the editor assembly does not need the physics modules.
        private static bool HasTriggerCollider(GameObject go)
        {
            foreach (Component c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                for (Type t = c.GetType(); t != null; t = t.BaseType)
                {
                    if (t.Name != "Collider" && t.Name != "Collider2D") continue;
                    var so = new SerializedObject(c);
                    SerializedProperty trigger = so.FindProperty("m_IsTrigger");
                    return trigger == null || trigger.boolValue;
                }
            }
            return false;
        }
    }

    internal struct AudioIssue
    {
        public MessageType type;
        public string message;
        public UnityEngine.Object context;

        public AudioIssue(MessageType type, string message, UnityEngine.Object context)
        {
            this.type = type;
            this.message = message;
            this.context = context;
        }
    }

    /// <summary>Checks configs and libraries for problems that would fail silently at runtime.</summary>
    internal static class AudioValidator
    {
        internal static List<AudioIssue> ValidateConfig(AudioSystemConfig config)
        {
            var issues = new List<AudioIssue>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            bool hasMaster = false;
            foreach (AudioChannelDefinition def in config.channels)
            {
                if (def == null || string.IsNullOrEmpty(def.name))
                {
                    issues.Add(new AudioIssue(MessageType.Error, "A channel has no name.", config));
                    continue;
                }
                if (!names.Add(def.name)) issues.Add(new AudioIssue(MessageType.Error, "Channel '" + def.name + "' is defined more than once.", config));
                if (def.name == AudioChannel.MasterName) hasMaster = true;
                if (string.IsNullOrEmpty(def.mixerVolumeParameter)) continue;

                UnityEngine.Audio.AudioMixer mixer = def.mixerGroup != null ? def.mixerGroup.audioMixer : config.mixer;
                if (mixer == null)
                {
                    issues.Add(new AudioIssue(MessageType.Error, "Channel '" + def.name + "' has a mixer parameter but no mixer group and the config has no mixer.", config));
                }
                else
                {
                    float unused;
                    if (!mixer.GetFloat(def.mixerVolumeParameter, out unused))
                    {
                        issues.Add(new AudioIssue(MessageType.Error, "Mixer '" + mixer.name + "' has no exposed parameter '" + def.mixerVolumeParameter + "' (channel " + def.name + ").", config));
                    }
                }
            }
            if (!hasMaster) issues.Add(new AudioIssue(MessageType.Info, "No Master channel defined; a default one (volume 1) is created at runtime.", config));
            if (config.poolMaxSize < 1) issues.Add(new AudioIssue(MessageType.Error, "Pool max size must be at least 1.", config));
            if (config.poolInitialSize > config.poolMaxSize) issues.Add(new AudioIssue(MessageType.Warning, "Pool initial size is larger than max size; it is clamped.", config));
            if (config.fullVolumeDecibels <= config.silenceDecibels) issues.Add(new AudioIssue(MessageType.Error, "Full volume dB must be greater than silence dB.", config));
            for (int i = 0; i < config.libraries.Count; i++)
            {
                if (config.libraries[i] == null) issues.Add(new AudioIssue(MessageType.Warning, "Library slot " + i + " is empty.", config));
            }
            return issues;
        }

        internal static List<AudioIssue> ValidateLibrary(AudioLibrary lib)
        {
            var issues = new List<AudioIssue>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (AudioCue cue in lib.cues)
            {
                if (cue == null) continue;
                string label = "'" + cue.id + "' in " + lib.name;
                if (string.IsNullOrEmpty(cue.id)) issues.Add(new AudioIssue(MessageType.Error, "A cue in " + lib.name + " has an empty id.", lib));
                else if (!ids.Add(cue.id)) issues.Add(new AudioIssue(MessageType.Error, "Duplicate cue id " + label + ".", lib));
                if (cue.clips.Count == 0) issues.Add(new AudioIssue(MessageType.Warning, "Cue " + label + " has no clips.", lib));
                else if (cue.clips.Contains(null)) issues.Add(new AudioIssue(MessageType.Warning, "Cue " + label + " has a missing clip.", lib));
                if (cue.volumeRange.x > cue.volumeRange.y || cue.pitchRange.x > cue.pitchRange.y)
                {
                    issues.Add(new AudioIssue(MessageType.Warning, "Cue " + label + " has a min value above its max (volume or pitch).", lib));
                }
                if (cue.pitchRange.x <= 0f) issues.Add(new AudioIssue(MessageType.Warning, "Cue " + label + " has a pitch of 0 or less.", lib));
                if (cue.spatialBlend > 0f && cue.maxDistance <= cue.minDistance)
                {
                    issues.Add(new AudioIssue(MessageType.Warning, "Cue " + label + " has max distance <= min distance.", lib));
                }
            }
            var playlistIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (AudioPlaylist list in lib.playlists)
            {
                if (list == null) continue;
                if (string.IsNullOrEmpty(list.id)) issues.Add(new AudioIssue(MessageType.Error, "A playlist in " + lib.name + " has an empty id.", lib));
                else if (!playlistIds.Add(list.id)) issues.Add(new AudioIssue(MessageType.Error, "Duplicate playlist id '" + list.id + "' in " + lib.name + ".", lib));
                foreach (string id in list.cueIds)
                {
                    if (!AudioEditorUtility.HasCue(id))
                    {
                        issues.Add(new AudioIssue(MessageType.Error, "Playlist '" + list.id + "' references unknown cue '" + id + "'.", lib));
                    }
                }
            }
            return issues;
        }

        internal static List<AudioIssue> ValidateProject()
        {
            AudioEditorUtility.Invalidate();
            var issues = new List<AudioIssue>();
            AudioSystemConfig config = AudioEditorUtility.FindConfig();
            if (config == null)
            {
                issues.Add(new AudioIssue(MessageType.Info, "No AudioSystemConfig found: built-in defaults are used and no library is registered automatically. Use " +
                    AudioEditorUtility.MenuRoot + "Create Default Config.", null));
            }
            else
            {
                if (AssetDatabase.GetAssetPath(config) != AudioEditorUtility.DefaultConfigPath)
                {
                    issues.Add(new AudioIssue(MessageType.Warning, "Config '" + AssetDatabase.GetAssetPath(config) + "' is not at " +
                        AudioEditorUtility.DefaultConfigPath + " and is not loaded automatically.", config));
                }
                issues.AddRange(ValidateConfig(config));
            }

            var seen = new Dictionary<string, AudioLibrary>(StringComparer.Ordinal);
            foreach (AudioLibrary lib in AudioEditorUtility.Libraries)
            {
                issues.AddRange(ValidateLibrary(lib));
                if (config != null && !config.libraries.Contains(lib))
                {
                    issues.Add(new AudioIssue(MessageType.Info, "Library " + lib.name + " is not in the config; register it with Audio.RegisterLibrary.", lib));
                }
                foreach (AudioCue cue in lib.cues)
                {
                    if (cue == null || string.IsNullOrEmpty(cue.id)) continue;
                    AudioLibrary other;
                    if (seen.TryGetValue(cue.id, out other) && other != lib)
                    {
                        issues.Add(new AudioIssue(MessageType.Warning, "Cue id '" + cue.id + "' exists in " + other.name + " and " + lib.name + "; the library registered last wins.", lib));
                    }
                    else
                    {
                        seen[cue.id] = lib;
                    }
                }
            }
            return issues;
        }
    }
}
