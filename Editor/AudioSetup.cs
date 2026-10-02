using UnityEditor;
using UnityEngine;

namespace kinatraa.AudioSystem.Editor
{
    /// <summary>Setup helpers: config/library creation, library registration and project validation.</summary>
    internal static class AudioSetup
    {
        private const string DefaultLibraryPath = "Assets/Audio/AudioLibrary.asset";

        [MenuItem(AudioEditorUtility.MenuRoot + "Create Default Config", false, 40)]
        internal static void CreateDefaultConfig()
        {
            AudioLibrary library = CreateDefaultSetup();
            if (library != null && !Application.isBatchMode) AudioLibraryWindow.Open(library);
        }

        /// <summary>
        /// Creates Resources/kinatraaAudioConfig plus an empty registered library. If the config already exists,
        /// returns its first library (creating one when it has none).
        /// </summary>
        internal static AudioLibrary CreateDefaultSetup()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioSystemConfig>(AudioEditorUtility.DefaultConfigPath);
            if (existing != null)
            {
                Debug.Log("[kinatraa Audio] " + AudioEditorUtility.DefaultConfigPath + " already exists.", existing);
                foreach (AudioLibrary lib in existing.libraries)
                {
                    if (lib != null) return lib;
                }
                return CreateLibrary(DefaultLibraryPath);
            }

            EnsureFolder("Assets/Audio");
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            AssetDatabase.CreateAsset(library, AssetDatabase.GenerateUniqueAssetPath(DefaultLibraryPath));
            CreateConfig(library);
            Debug.Log("[kinatraa Audio] Created " + AudioEditorUtility.DefaultConfigPath + " and " + AssetDatabase.GetAssetPath(library) + ".", library);
            return library;
        }

        /// <summary>Creates a library asset and registers it in the config (creating the config if needed).</summary>
        internal static AudioLibrary CreateLibrary(string path)
        {
            EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            AssetDatabase.CreateAsset(library, AssetDatabase.GenerateUniqueAssetPath(path));
            Register(library);
            return library;
        }

        /// <summary>Adds a library to the config so it loads at startup (creating the config if needed).</summary>
        internal static void Register(AudioLibrary library)
        {
            AudioSystemConfig config = AudioEditorUtility.FindConfig();
            if (config == null)
            {
                CreateConfig(library);
                return;
            }
            if (config.libraries.Contains(library)) return;
            Undo.RecordObject(config, "Register Audio Library");
            config.libraries.Add(library);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AudioEditorUtility.Invalidate();
        }

        private static void CreateConfig(AudioLibrary library)
        {
            EnsureFolder("Assets/Resources");
            var config = ScriptableObject.CreateInstance<AudioSystemConfig>();
            config.libraries.Add(library);
            AssetDatabase.CreateAsset(config, AudioEditorUtility.DefaultConfigPath);
            AssetDatabase.SaveAssets();
            AudioEditorUtility.Invalidate();
        }

        [MenuItem(AudioEditorUtility.MenuRoot + "Validate Setup", false, 41)]
        internal static void ValidateSetup()
        {
            int errors = 0, warnings = 0;
            foreach (AudioIssue issue in AudioValidator.ValidateProject())
            {
                string message = "[kinatraa Audio] " + issue.message;
                if (issue.type == MessageType.Error)
                {
                    errors++;
                    Debug.LogError(message, issue.context);
                }
                else if (issue.type == MessageType.Warning)
                {
                    warnings++;
                    Debug.LogWarning(message, issue.context);
                }
                else
                {
                    Debug.Log(message, issue.context);
                }
            }
            string summary = errors == 0 && warnings == 0
                ? "No problems found."
                : errors + " error(s), " + warnings + " warning(s). See the Console for details.";
            if (!Application.isBatchMode) EditorUtility.DisplayDialog("kinatraa Audio: Validate Setup", summary, "OK");
            Debug.Log("[kinatraa Audio] Validate Setup: " + summary);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
