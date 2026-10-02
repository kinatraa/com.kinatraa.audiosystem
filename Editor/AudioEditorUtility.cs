using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace kinatraa.AudioSystem.Editor
{
    /// <summary>Shared editor helpers: cached library/cue lookups, id formatting and preview playback.</summary>
    [InitializeOnLoad]
    internal static class AudioEditorUtility
    {
        internal const string MenuRoot = "Tools/kinatraa/Audio/";
        internal const string DefaultConfigPath = "Assets/Resources/" + AudioSystemConfig.ResourcesPath + ".asset";

        internal struct CueInfo
        {
            public string id;
            public string channel;
            public AudioLibrary library;
        }

        private static List<AudioLibrary> libraries;
        private static List<CueInfo> cues;
        private static HashSet<string> cueIds;
        private static string[] channelNames;
        private static AudioSystemConfig config;
        private static bool configSearched;
        private static AudioSource previewSource;
        private static object previewOwner;

        static AudioEditorUtility()
        {
            EditorApplication.projectChanged += Invalidate;
            Undo.undoRedoPerformed += Invalidate;
            AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
            EditorApplication.playModeStateChanged += state => StopPreview();
        }

        /// <summary>Drops cached lookups; call after editing libraries or the config.</summary>
        internal static void Invalidate()
        {
            libraries = null;
            cues = null;
            cueIds = null;
            channelNames = null;
            config = null;
            configSearched = false;
        }

        internal static List<AudioLibrary> Libraries
        {
            get
            {
                if (libraries != null) return libraries;
                libraries = new List<AudioLibrary>();
                var paths = new List<string>();
                foreach (string guid in AssetDatabase.FindAssets("t:AudioLibrary")) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
                paths.Sort(StringComparer.Ordinal);
                foreach (string path in paths)
                {
                    var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(path);
                    if (lib != null && !libraries.Contains(lib)) libraries.Add(lib);
                }
                return libraries;
            }
        }

        /// <summary>Every cue in every library of the project, sorted by id (duplicates included).</summary>
        internal static List<CueInfo> Cues
        {
            get
            {
                if (cues != null) return cues;
                cues = new List<CueInfo>();
                foreach (AudioLibrary lib in Libraries)
                {
                    foreach (AudioCue cue in lib.cues)
                    {
                        if (cue == null || string.IsNullOrEmpty(cue.id)) continue;
                        cues.Add(new CueInfo { id = cue.id, channel = cue.channel.Name, library = lib });
                    }
                }
                cues.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
                return cues;
            }
        }

        internal static bool HasCue(string id)
        {
            if (cueIds == null)
            {
                cueIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (CueInfo info in Cues) cueIds.Add(info.id);
            }
            return id != null && cueIds.Contains(id);
        }

        internal static AudioCue FindCue(string id)
        {
            foreach (CueInfo info in Cues)
            {
                if (info.id == id) return info.library.FindCue(id);
            }
            return null;
        }

        /// <summary>The config at Resources/kinatraaAudioConfig, else the first config found (cached until <see cref="Invalidate"/>).</summary>
        internal static AudioSystemConfig FindConfig()
        {
            if (config != null || configSearched) return config;
            configSearched = true;
            config = AssetDatabase.LoadAssetAtPath<AudioSystemConfig>(DefaultConfigPath);
            if (config != null) return config;
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioSystemConfig")) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                if (path.EndsWith("/Resources/" + AudioSystemConfig.ResourcesPath + ".asset", StringComparison.Ordinal))
                {
                    config = AssetDatabase.LoadAssetAtPath<AudioSystemConfig>(path);
                    return config;
                }
            }
            config = paths.Count > 0 ? AssetDatabase.LoadAssetAtPath<AudioSystemConfig>(paths[0]) : null;
            return config;
        }

        /// <summary>Channel names from the config followed by any missing built-in channel.</summary>
        internal static string[] ChannelNames
        {
            get
            {
                if (channelNames != null) return channelNames;
                var names = new List<string>();
                AudioSystemConfig config = FindConfig();
                if (config != null)
                {
                    foreach (AudioChannelDefinition def in config.channels)
                    {
                        if (def != null && !string.IsNullOrEmpty(def.name) && !names.Contains(def.name)) names.Add(def.name);
                    }
                }
                foreach (string builtIn in AudioChannel.BuiltInNames)
                {
                    if (!names.Contains(builtIn)) names.Add(builtIn);
                }
                channelNames = names.ToArray();
                return channelNames;
            }
        }

        // ---------------------------------------------------------------- ids

        /// <summary>"UI Click", "UIClick" and "ui-click" all become "ui_click".</summary>
        internal static string ToSnakeCase(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var sb = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetterOrDigit(c))
                {
                    if (char.IsUpper(c) && i > 0 && sb.Length > 0 && sb[sb.Length - 1] != '_')
                    {
                        char prev = value[i - 1];
                        bool nextLower = i + 1 < value.Length && char.IsLower(value[i + 1]);
                        if (char.IsLower(prev) || char.IsDigit(prev) || (char.IsUpper(prev) && nextLower)) sb.Append('_');
                    }
                    sb.Append(char.ToLowerInvariant(c));
                }
                else if (sb.Length > 0 && sb[sb.Length - 1] != '_')
                {
                    sb.Append('_');
                }
            }
            return sb.ToString().Trim('_');
        }

        /// <summary>"footstep_01" becomes "footstep" so numbered variations share one cue.</summary>
        internal static string StripVariationSuffix(string name)
        {
            Match m = Regex.Match(name, @"^(.*?)[\s_\-\.]*\d+$");
            return m.Success && m.Groups[1].Length > 0 ? m.Groups[1].Value : name;
        }

        /// <summary>"ui_click" becomes "UiClick"; leading digits get an underscore.</summary>
        internal static string ToIdentifier(string id)
        {
            var sb = new StringBuilder(id.Length);
            bool upper = true;
            foreach (char c in id)
            {
                if (!char.IsLetterOrDigit(c))
                {
                    upper = true;
                    continue;
                }
                sb.Append(upper ? char.ToUpperInvariant(c) : c);
                upper = false;
            }
            if (sb.Length == 0 || char.IsDigit(sb[0])) sb.Insert(0, '_');
            return sb.ToString();
        }

        // ---------------------------------------------------------------- preview

        internal static bool IsPreviewing(object owner)
        {
            return previewSource != null && previewOwner == owner && previewSource.isPlaying;
        }

        /// <summary>Plays a random variation of a cue in 2D through a hidden editor-only AudioSource.</summary>
        internal static void Preview(AudioCue cue)
        {
            StopPreview();
            if (cue == null || cue.clips.Count == 0) return;
            AudioClip clip = cue.clips[UnityEngine.Random.Range(0, cue.clips.Count)];
            if (clip == null) return;
            if (previewSource == null)
            {
                GameObject go = EditorUtility.CreateGameObjectWithHideFlags("kinatraa Audio Preview", HideFlags.HideAndDontSave, typeof(AudioSource));
                previewSource = go.GetComponent<AudioSource>();
            }
            previewSource.clip = clip;
            previewSource.volume = UnityEngine.Random.Range(cue.volumeRange.x, cue.volumeRange.y);
            previewSource.pitch = UnityEngine.Random.Range(cue.pitchRange.x, cue.pitchRange.y);
            previewSource.loop = cue.loop;
            previewSource.spatialBlend = 0f;
            previewSource.Play();
            previewOwner = cue;
        }

        internal static void StopPreview()
        {
            previewOwner = null;
            if (previewSource == null) return;
            UnityEngine.Object.DestroyImmediate(previewSource.gameObject);
            previewSource = null;
        }
    }
}
