using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace kinatraa.AudioSystem.Editor
{
    /// <summary>
    /// Library inspector: search, channel/tag filters, drag-and-drop cue creation, bulk edit, preview,
    /// duplicate-id and missing-clip detection, sorting and reordering.
    /// </summary>
    [CustomEditor(typeof(AudioLibrary))]
    internal sealed class AudioLibraryEditor : UnityEditor.Editor
    {
        private const string GroupPrefKey = "kinatraa.audio.groupVariations";
        private const string AllLabel = "All";
        private static readonly Color DuplicateColor = new Color(1f, 0.5f, 0.5f);

        private SerializedProperty cuesProp;
        private SerializedProperty playlistsProp;
        private SearchField searchField;
        private string search = "";
        private string channelFilter = AllLabel;
        private string tagFilter = AllLabel;
        private readonly HashSet<int> selected = new HashSet<int>();
        private readonly List<int> visible = new List<int>();
        private string bulkChannel = AudioChannel.SfxName;
        private Vector2 bulkVolume = Vector2.one;
        private Vector2 bulkPitch = Vector2.one;
        private string bulkTag = "";
        private Action pending;
        private GUIStyle dropStyle;

        private AudioLibrary Library
        {
            get { return (AudioLibrary)target; }
        }

        private void OnEnable()
        {
            cuesProp = serializedObject.FindProperty("cues");
            playlistsProp = serializedObject.FindProperty("playlists");
            searchField = new SearchField();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            AudioLibrary lib = Library;

            DrawToolbar(lib);
            DrawDropArea();

            var localCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (AudioCue cue in lib.cues)
            {
                if (cue == null) continue;
                int n;
                localCounts.TryGetValue(cue.id ?? "", out n);
                localCounts[cue.id ?? ""] = n + 1;
            }
            DrawSummary(lib, localCounts);

            visible.Clear();
            for (int i = 0; i < lib.cues.Count; i++)
            {
                if (Matches(lib.cues[i])) visible.Add(i);
            }
            if (selected.Count > 0) DrawBulkBar();

            for (int v = 0; v < visible.Count; v++) DrawCue(lib, visible[v], localCounts);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Cue")) pending = () => Mutate("Add Audio Cue", l => l.cues.Add(new AudioCue { id = UniqueId(l, "new_cue") }));
            if (GUILayout.Button("Generate Ids")) pending = () => AudioIdsGenerator.GenerateAndReport();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(playlistsProp, true);

            if (serializedObject.ApplyModifiedProperties()) AudioEditorUtility.Invalidate();
            if (pending != null)
            {
                Action action = pending;
                pending = null;
                action();
            }
        }

        // ---------------------------------------------------------------- toolbar & filters

        private void DrawToolbar(AudioLibrary lib)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            search = searchField.OnToolbarGUI(search);

            var channels = new List<string> { AllLabel };
            channels.AddRange(AudioEditorUtility.ChannelNames);
            channelFilter = Popup(channelFilter, channels, 90f);

            var tags = new List<string> { AllLabel };
            foreach (AudioCue cue in lib.cues)
            {
                if (cue == null) continue;
                foreach (string tag in cue.tags)
                {
                    if (!string.IsNullOrEmpty(tag) && !tags.Contains(tag)) tags.Add(tag);
                }
            }
            tagFilter = Popup(tagFilter, tags, 90f);

            if (GUILayout.Button(new GUIContent("Sort", "Sort cues by id"), EditorStyles.toolbarButton, GUILayout.Width(40f)))
            {
                pending = () => Mutate("Sort Audio Cues", l => l.cues.Sort((a, b) => string.CompareOrdinal(a != null ? a.id : "", b != null ? b.id : "")));
            }
            EditorGUILayout.EndHorizontal();
        }

        private static string Popup(string current, List<string> options, float width)
        {
            int index = Mathf.Max(0, options.IndexOf(current));
            index = EditorGUILayout.Popup(index, options.ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(width));
            return options[index];
        }

        private bool Matches(AudioCue cue)
        {
            if (cue == null) return true;
            if (channelFilter != AllLabel && cue.channel.Name != channelFilter) return false;
            if (tagFilter != AllLabel && !cue.tags.Contains(tagFilter)) return false;
            if (string.IsNullOrEmpty(search)) return true;
            if (cue.id != null && cue.id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            foreach (string tag in cue.tags)
            {
                if (tag != null && tag.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private void DrawSummary(AudioLibrary lib, Dictionary<string, int> localCounts)
        {
            int duplicates = 0, missing = 0;
            foreach (KeyValuePair<string, int> pair in localCounts)
            {
                if (pair.Value > 1 || pair.Key.Length == 0) duplicates += pair.Value;
            }
            foreach (AudioCue cue in lib.cues)
            {
                if (cue != null && HasMissingClips(cue)) missing++;
            }
            if (duplicates > 0) EditorGUILayout.HelpBox(duplicates + " cue(s) have an empty or duplicate id.", MessageType.Error);
            if (missing > 0) EditorGUILayout.HelpBox(missing + " cue(s) have no clips or a missing clip.", MessageType.Warning);
        }

        private static bool HasMissingClips(AudioCue cue)
        {
            if (cue.clips.Count == 0) return true;
            foreach (AudioClip clip in cue.clips)
            {
                if (clip == null) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- drag & drop

        private void DrawDropArea()
        {
            if (dropStyle == null)
            {
                dropStyle = new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
            }
            Rect rect = GUILayoutUtility.GetRect(0f, 34f, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "Drop AudioClips or folders here to create cues", dropStyle);
            bool group = EditorPrefs.GetBool(GroupPrefKey, true);
            bool newGroup = EditorGUILayout.ToggleLeft("Group numbered variations (step_01, step_02 = one cue \"step\")", group);
            if (newGroup != group) EditorPrefs.SetBool(GroupPrefKey, newGroup);

            Event e = Event.current;
            if ((e.type != EventType.DragUpdated && e.type != EventType.DragPerform) || !rect.Contains(e.mousePosition)) return;
            List<AudioClip> clips = CollectClips(DragAndDrop.objectReferences);
            if (clips.Count == 0) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                pending = () => AddClips(clips, newGroup);
            }
            e.Use();
        }

        private static List<AudioClip> CollectClips(UnityEngine.Object[] objects)
        {
            var result = new List<AudioClip>();
            foreach (UnityEngine.Object obj in objects)
            {
                var clip = obj as AudioClip;
                if (clip != null)
                {
                    if (!result.Contains(clip)) result.Add(clip);
                    continue;
                }
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path)) continue;
                foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { path }))
                {
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                    if (clip != null && !result.Contains(clip)) result.Add(clip);
                }
            }
            result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return result;
        }

        private void AddClips(List<AudioClip> clips, bool groupVariations)
        {
            Mutate("Add Audio Cues", lib =>
            {
                foreach (AudioClip clip in clips)
                {
                    string baseName = groupVariations ? AudioEditorUtility.StripVariationSuffix(clip.name) : clip.name;
                    string id = AudioEditorUtility.ToSnakeCase(baseName);
                    if (id.Length == 0) id = "cue";
                    AudioCue cue = lib.FindCue(id);
                    if (cue == null)
                    {
                        cue = new AudioCue { id = id };
                        lib.cues.Add(cue);
                    }
                    if (!cue.clips.Contains(clip)) cue.clips.Add(clip);
                }
            });
        }

        // ---------------------------------------------------------------- bulk edit

        private void DrawBulkBar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Bulk edit: " + selected.Count + " selected", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            string[] names = AudioEditorUtility.ChannelNames;
            int ci = EditorGUILayout.Popup("Channel", Mathf.Max(0, Array.IndexOf(names, bulkChannel)), names);
            bulkChannel = names[ci];
            if (GUILayout.Button("Apply", GUILayout.Width(60f)))
            {
                ForSelected(p => p.FindPropertyRelative("channel").FindPropertyRelative("name").stringValue = bulkChannel);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            bulkVolume = AudioCueGUI.MinMax("Volume", bulkVolume, 0f, 1f);
            if (GUILayout.Button("Apply", GUILayout.Width(60f))) ForSelected(p => p.FindPropertyRelative("volumeRange").vector2Value = bulkVolume);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            bulkPitch = AudioCueGUI.MinMax("Pitch", bulkPitch, 0.1f, 3f);
            if (GUILayout.Button("Apply", GUILayout.Width(60f))) ForSelected(p => p.FindPropertyRelative("pitchRange").vector2Value = bulkPitch);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            bulkTag = EditorGUILayout.TextField("Tag", bulkTag);
            GUI.enabled = !string.IsNullOrEmpty(bulkTag);
            if (GUILayout.Button("Add", GUILayout.Width(40f))) ForSelected(p => SetTag(p.FindPropertyRelative("tags"), bulkTag, true));
            if (GUILayout.Button("Remove", GUILayout.Width(56f))) ForSelected(p => SetTag(p.FindPropertyRelative("tags"), bulkTag, false));
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All Visible")) selected.UnionWith(visible);
            if (GUILayout.Button("Clear Selection")) selected.Clear();
            if (GUILayout.Button("Delete Selected"))
            {
                var indices = new List<int>(selected);
                indices.Sort();
                pending = () => Mutate("Delete Audio Cues", l =>
                {
                    for (int i = indices.Count - 1; i >= 0; i--)
                    {
                        if (indices[i] < l.cues.Count) l.cues.RemoveAt(indices[i]);
                    }
                });
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void ForSelected(Action<SerializedProperty> apply)
        {
            foreach (int i in selected)
            {
                if (i < cuesProp.arraySize) apply(cuesProp.GetArrayElementAtIndex(i));
            }
        }

        private static void SetTag(SerializedProperty tags, string tag, bool add)
        {
            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue != tag) continue;
                if (!add) tags.DeleteArrayElementAtIndex(i);
                return;
            }
            if (!add) return;
            tags.arraySize++;
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        }

        // ---------------------------------------------------------------- cue rows

        private void DrawCue(AudioLibrary lib, int index, Dictionary<string, int> localCounts)
        {
            AudioCue cue = lib.cues[index];
            SerializedProperty prop = cuesProp.GetArrayElementAtIndex(index);
            string id = cue != null ? cue.id ?? "" : "";
            int count;
            bool duplicate = id.Length == 0 || (localCounts.TryGetValue(id, out count) && count > 1);
            string otherLibrary = OtherLibraryWithId(lib, id);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            bool isSelected = selected.Contains(index);
            if (EditorGUILayout.Toggle(isSelected, GUILayout.Width(16f)) != isSelected)
            {
                if (isSelected) selected.Remove(index);
                else selected.Add(index);
            }

            Color oldColor = GUI.color;
            if (duplicate) GUI.color = DuplicateColor;
            prop.isExpanded = EditorGUILayout.Foldout(prop.isExpanded, id.Length > 0 ? id : "<empty id>", true);
            GUI.color = oldColor;

            if (cue != null)
            {
                GUILayout.Label(cue.channel.Name, EditorStyles.miniLabel, GUILayout.Width(60f));
                GUIContent clipsLabel = HasMissingClips(cue)
                    ? new GUIContent(cue.clips.Count + " clip(s) (!)", "No clips or a missing clip")
                    : new GUIContent(cue.clips.Count + " clip(s)");
                GUILayout.Label(clipsLabel, EditorStyles.miniLabel, GUILayout.Width(70f));

                bool playing = AudioEditorUtility.IsPreviewing(cue);
                if (GUILayout.Button(playing ? "Stop" : "Play", EditorStyles.miniButton, GUILayout.Width(40f)))
                {
                    if (playing) AudioEditorUtility.StopPreview();
                    else AudioEditorUtility.Preview(cue);
                }
            }

            GUI.enabled = index > 0;
            if (GUILayout.Button(new GUIContent("↑", "Move up"), EditorStyles.miniButtonLeft, GUILayout.Width(22f))) Move(index, index - 1);
            GUI.enabled = index < lib.cues.Count - 1;
            if (GUILayout.Button(new GUIContent("↓", "Move down"), EditorStyles.miniButtonMid, GUILayout.Width(22f))) Move(index, index + 1);
            GUI.enabled = true;
            if (GUILayout.Button(new GUIContent("Dup", "Duplicate"), EditorStyles.miniButtonMid, GUILayout.Width(34f)))
            {
                pending = () => Mutate("Duplicate Audio Cue", l =>
                {
                    AudioCue copy = JsonUtility.FromJson<AudioCue>(JsonUtility.ToJson(l.cues[index]));
                    copy.id = UniqueId(l, copy.id);
                    l.cues.Insert(index + 1, copy);
                });
            }
            if (GUILayout.Button(new GUIContent("×", "Delete"), EditorStyles.miniButtonRight, GUILayout.Width(22f)))
            {
                pending = () => Mutate("Delete Audio Cue", l => l.cues.RemoveAt(index));
            }
            EditorGUILayout.EndHorizontal();

            if (duplicate) EditorGUILayout.HelpBox(id.Length == 0 ? "Empty id: this cue cannot be played." : "Duplicate id in this library.", MessageType.Error);
            if (otherLibrary != null) EditorGUILayout.HelpBox("Id also used in library '" + otherLibrary + "'. The library registered last wins.", MessageType.Warning);
            if (prop.isExpanded) AudioCueGUI.Draw(prop);
            EditorGUILayout.EndVertical();
        }

        private static string OtherLibraryWithId(AudioLibrary lib, string id)
        {
            if (id.Length == 0) return null;
            foreach (AudioEditorUtility.CueInfo info in AudioEditorUtility.Cues)
            {
                if (info.id == id && info.library != lib) return info.library.name;
            }
            return null;
        }

        private void Move(int from, int to)
        {
            cuesProp.MoveArrayElement(from, to);
            selected.Clear();
        }

        // ---------------------------------------------------------------- helpers

        private void Mutate(string undoName, Action<AudioLibrary> change)
        {
            AudioLibrary lib = Library;
            Undo.RecordObject(lib, undoName);
            change(lib);
            EditorUtility.SetDirty(lib);
            serializedObject.Update();
            selected.Clear();
            AudioEditorUtility.Invalidate();
            Audio.NotifyLibraryChanged(lib);
        }

        private static string UniqueId(AudioLibrary lib, string id)
        {
            if (lib.FindCue(id) == null) return id;
            int n = 2;
            while (lib.FindCue(id + "_" + n) != null) n++;
            return id + "_" + n;
        }
    }

    /// <summary>Layout of a single cue (the cue "inspector"), with min/max sliders and validation.</summary>
    internal static class AudioCueGUI
    {
        internal static void Draw(SerializedProperty cue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.DelayedTextField(cue.FindPropertyRelative("id"));

            SerializedProperty clips = cue.FindPropertyRelative("clips");
            EditorGUILayout.PropertyField(clips, true);
            if (clips.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No clips: this cue plays nothing.", MessageType.Warning);
            }
            else
            {
                for (int i = 0; i < clips.arraySize; i++)
                {
                    if (clips.GetArrayElementAtIndex(i).objectReferenceValue != null) continue;
                    EditorGUILayout.HelpBox("Clip " + i + " is missing.", MessageType.Warning);
                    break;
                }
            }

            EditorGUILayout.PropertyField(cue.FindPropertyRelative("playMode"));
            EditorGUILayout.PropertyField(cue.FindPropertyRelative("channel"));
            MinMax(cue.FindPropertyRelative("volumeRange"), "Volume", 0f, 1f);
            MinMax(cue.FindPropertyRelative("pitchRange"), "Pitch", 0.1f, 3f);
            EditorGUILayout.PropertyField(cue.FindPropertyRelative("loop"));

            EditorGUILayout.LabelField("Spatial", EditorStyles.boldLabel);
            SerializedProperty blend = cue.FindPropertyRelative("spatialBlend");
            EditorGUILayout.PropertyField(blend, new GUIContent("Spatial Blend (2D-3D)"));
            if (blend.floatValue > 0f)
            {
                SerializedProperty min = cue.FindPropertyRelative("minDistance");
                SerializedProperty max = cue.FindPropertyRelative("maxDistance");
                EditorGUILayout.PropertyField(min);
                EditorGUILayout.PropertyField(max);
                EditorGUILayout.PropertyField(cue.FindPropertyRelative("rolloff"));
                if (min.floatValue < 0f || max.floatValue <= min.floatValue)
                {
                    EditorGUILayout.HelpBox("Max distance must be greater than min distance (and both positive).", MessageType.Warning);
                }
            }

            EditorGUILayout.LabelField("Limits", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(cue.FindPropertyRelative("priority"));
            EditorGUILayout.PropertyField(cue.FindPropertyRelative("cooldown"));
            SerializedProperty maxInstances = cue.FindPropertyRelative("maxInstances");
            EditorGUILayout.PropertyField(maxInstances, new GUIContent("Max Instances (0 = no limit)"));
            if (maxInstances.intValue > 0) EditorGUILayout.PropertyField(cue.FindPropertyRelative("stealPolicy"));
            if (maxInstances.intValue < 0) maxInstances.intValue = 0;

            EditorGUILayout.LabelField("Timing", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(cue.FindPropertyRelative("startDelay"));
            EditorGUILayout.PropertyField(cue.FindPropertyRelative("fadeIn"));
            EditorGUILayout.PropertyField(cue.FindPropertyRelative("fadeOut"));

            EditorGUILayout.PropertyField(cue.FindPropertyRelative("tags"), true);
            EditorGUI.indentLevel--;
        }

        internal static void MinMax(SerializedProperty property, string label, float min, float max)
        {
            EditorGUI.BeginChangeCheck();
            Vector2 value = MinMax(label, property.vector2Value, min, max);
            if (EditorGUI.EndChangeCheck()) property.vector2Value = value;
        }

        internal static Vector2 MinMax(string label, Vector2 value, float min, float max)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            float a = EditorGUILayout.FloatField(value.x, GUILayout.Width(44f));
            float b = value.y;
            EditorGUILayout.MinMaxSlider(ref a, ref b, min, max);
            b = EditorGUILayout.FloatField(b, GUILayout.Width(44f));
            EditorGUI.indentLevel = indent;
            EditorGUILayout.EndHorizontal();
            a = Mathf.Clamp(a, min, max);
            b = Mathf.Clamp(b, a, max);
            return new Vector2(a, b);
        }
    }

    /// <summary>Dockable window hosting the library inspector, with a library picker.</summary>
    internal sealed class AudioLibraryWindow : EditorWindow
    {
        [SerializeField] private AudioLibrary library;
        private UnityEditor.Editor editor;
        private Vector2 scroll;

        [MenuItem(AudioEditorUtility.MenuRoot + "Library Window", false, 0)]
        internal static void Open()
        {
            GetWindow<AudioLibraryWindow>("Audio Library").Show();
        }

        private void OnSelectionChange()
        {
            var lib = Selection.activeObject as AudioLibrary;
            if (lib == null) return;
            library = lib;
            Repaint();
        }

        private void OnDisable()
        {
            if (editor != null) DestroyImmediate(editor);
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            library = (AudioLibrary)EditorGUILayout.ObjectField(library, typeof(AudioLibrary), false, GUILayout.MinWidth(160f));
            List<AudioLibrary> all = AudioEditorUtility.Libraries;
            if (GUILayout.Button("Pick", EditorStyles.toolbarDropDown, GUILayout.Width(50f)))
            {
                var menu = new GenericMenu();
                foreach (AudioLibrary lib in all)
                {
                    AudioLibrary captured = lib;
                    menu.AddItem(new GUIContent(lib.name), lib == library, () => library = captured);
                }
                if (all.Count == 0) menu.AddDisabledItem(new GUIContent("No libraries in project"));
                menu.ShowAsContext();
            }
            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(40f)))
            {
                string path = EditorUtility.SaveFilePanelInProject("New Audio Library", "AudioLibrary", "asset", "Create a new audio library");
                if (!string.IsNullOrEmpty(path))
                {
                    library = CreateInstance<AudioLibrary>();
                    AssetDatabase.CreateAsset(library, path);
                    AssetDatabase.SaveAssets();
                    AudioEditorUtility.Invalidate();
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (library == null && all.Count > 0) library = all[0];
            if (library == null)
            {
                EditorGUILayout.HelpBox("No audio library. Click New, or use " + AudioEditorUtility.MenuRoot + "Create Default Config.", MessageType.Info);
                return;
            }
            if (editor == null || editor.target != library)
            {
                if (editor != null) DestroyImmediate(editor);
                editor = UnityEditor.Editor.CreateEditor(library);
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            editor.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }
    }
}
