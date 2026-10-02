using UnityEditor;
using UnityEngine;

namespace kinatraa.AudioSystem.Editor
{
    /// <summary>Setup menu items: default config creation and project validation.</summary>
    internal static class AudioSetup
    {
        [MenuItem(AudioEditorUtility.MenuRoot + "Create Default Config", false, 40)]
        internal static void CreateDefaultConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioSystemConfig>(AudioEditorUtility.DefaultConfigPath);
            if (existing != null)
            {
                Debug.Log("[kinatraa Audio] " + AudioEditorUtility.DefaultConfigPath + " already exists.", existing);
                Selection.activeObject = existing;
                return;
            }

            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Audio");
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            AssetDatabase.CreateAsset(library, AssetDatabase.GenerateUniqueAssetPath("Assets/Audio/AudioLibrary.asset"));
            var config = ScriptableObject.CreateInstance<AudioSystemConfig>();
            config.libraries.Add(library);
            AssetDatabase.CreateAsset(config, AudioEditorUtility.DefaultConfigPath);
            AssetDatabase.SaveAssets();
            AudioEditorUtility.Invalidate();
            Selection.activeObject = config;
            Debug.Log("[kinatraa Audio] Created " + AudioEditorUtility.DefaultConfigPath + " and " + AssetDatabase.GetAssetPath(library) + ".", config);
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
