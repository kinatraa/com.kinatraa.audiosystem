using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace kinatraa.AudioSystem.Editor
{
    /// <summary>
    /// Library editor: a compact cue list (search, channel filter, preview) plus a detail panel for the selected cue.
    /// SFX and Music drop zones create cues from clips or folders. Also hosted by <see cref="AudioLibraryWindow"/>.
    /// </summary>
    [CustomEditor(typeof(AudioLibrary))]
    internal sealed class AudioLibraryEditor : UnityEditor.Editor
    {
        private const string GroupPrefKey = "kinatraa.audio.groupVariations";
        private const string PlaylistsFoldoutKey = "kinatraa.audio.foldout.playlists";
        private const string AllChannels = "All";
        private const float RowHeight = 20f;
        private static readonly Color SelectedColor = new Color(0.24f, 0.49f, 0.91f, 0.35f);

        private SerializedProperty cuesProp;
        private SerializedProperty playlistsProp;
        private SearchField searchField;
        private string search = "";
        private string channelFilter = AllChannels;
        private int selected = -1;
        private int shownInDetail = -1;
        private Vector2 listScroll;
        private Vector2 detailScroll;
        private Action pending;
        private readonly Dictionary<string, int> idCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private GUIStyle dropStyle;
        private GUIStyle errorLabel;
        private GUIStyle channelLabel;
        private GUIContent playIcon;
        private GUIContent stopIcon;
        private GUIContent warnIcon;

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
            DrawLayout(false);
        }

        /// <summary>Draws the editor. Wide = list and detail side by side (window), otherwise stacked (inspector).</summary>
        internal void DrawLayout(bool wide)
        {
            InitStyles();
            serializedObject.Update();
            AudioLibrary lib = Library;
            CountIds(lib);

            DrawRegistration(lib);
            DrawToolbar(lib, wide);
            DrawDropZones();

            if (wide)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical(GUILayout.Width(300f));
                listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.ExpandHeight(true));
                DrawList(lib);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                EditorGUILayout.BeginVertical();
                detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                DrawDetail(lib);
                DrawPlaylists();
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                int rows = Mathf.Max(1, CountVisible(lib));
                listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Height(Mathf.Min(rows * RowHeight + 6f, 260f)));
                DrawList(lib);
                EditorGUILayout.EndScrollView();
                DrawDetail(lib);
                DrawPlaylists();
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                AudioEditorUtility.Invalidate();
                Audio.NotifyLibraryChanged(lib);
            }
            if (pending != null)
            {
                Action action = pending;
                pending = null;
                action();
            }
        }

        private void InitStyles()
        {
            if (dropStyle != null) return;
            dropStyle = new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            errorLabel = new GUIStyle(EditorStyles.label);
            errorLabel.normal.textColor = new Color(0.9f, 0.3f, 0.3f);
            channelLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            playIcon = new GUIContent(EditorGUIUtility.IconContent("PlayButton").image, "Preview");
            stopIcon = new GUIContent(EditorGUIUtility.IconContent("PlayButton On").image, "Stop preview");
            warnIcon = new GUIContent(EditorGUIUtility.IconContent("console.warnicon.sml").image, "No clips or a missing clip");
        }

        private void CountIds(AudioLibrary lib)
        {
            idCounts.Clear();
            foreach (AudioCue cue in lib.cues)
            {
                if (cue == null) continue;
                int n;
                idCounts.TryGetValue(cue.id ?? "", out n);
                idCounts[cue.id ?? ""] = n + 1;
            }
        }

        private bool IsBadId(string id)
        {
            int n;
            return string.IsNullOrEmpty(id) || (idCounts.TryGetValue(id, out n) && n > 1);
        }

        // ---------------------------------------------------------------- header

        private void DrawRegistration(AudioLibrary lib)
        {
            AudioSystemConfig config = AudioEditorUtility.FindConfig();
            if (config == null)
            {
                EditorGUILayout.HelpBox("There is no audio config yet, so this library is not loaded when the game starts.", MessageType.Warning);
                if (GUILayout.Button("Create Config With This Library")) pending = () => AudioSetup.Register(lib);
            }
            else if (!config.libraries.Contains(lib))
            {
                EditorGUILayout.HelpBox("This library is not in " + config.name + ", so its cues won't play unless you call Audio.RegisterLibrary.", MessageType.Warning);
                if (GUILayout.Button("Register in Config")) pending = () => AudioSetup.Register(lib);
            }
        }

        private void DrawToolbar(AudioLibrary lib, bool wide)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            search = searchField.OnToolbarGUI(search);

            var channels = new List<string> { AllChannels };
            channels.AddRange(AudioEditorUtility.ChannelNames);
            int index = Mathf.Max(0, channels.IndexOf(channelFilter));
            index = EditorGUILayout.Popup(index, channels.ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(80f));
            channelFilter = channels[index];

            if (GUILayout.Button(new GUIContent("Add Cue", "Add an empty cue"), EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                pending = () => Mutate("Add Audio Cue", l =>
                {
                    l.cues.Add(new AudioCue { id = UniqueId(l, "new_cue") });
                    return l.cues.Count - 1;
                });
            }

            if (GUILayout.Button("More", EditorStyles.toolbarDropDown, GUILayout.Width(50f)))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Sort Cues by Id"), false, () => Mutate("Sort Audio Cues", l =>
                {
                    l.cues.Sort((a, b) => string.CompareOrdinal(a != null ? a.id : "", b != null ? b.id : ""));
                    return -1;
                }));
                menu.AddItem(new GUIContent("Generate Audio Ids"), false, AudioIdsGenerator.GenerateAndReport);
                menu.AddItem(new GUIContent("Validate Setup"), false, AudioSetup.ValidateSetup);
                menu.AddSeparator("");
                if (!wide) menu.AddItem(new GUIContent("Open in Library Window"), false, () => AudioLibraryWindow.Open(lib));
                menu.AddItem(new GUIContent("Select Config"), false, () =>
                {
                    AudioSystemConfig config = AudioEditorUtility.FindConfig();
                    if (config != null) Selection.activeObject = config;
                });
                menu.ShowAsContext();
            }
            EditorGUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------- drop zones

        private void DrawDropZones()
        {
            Rect area = GUILayoutUtility.GetRect(0f, 38f, GUILayout.ExpandWidth(true));
            var sfx = new Rect(area.x, area.y, area.width * 0.5f - 2f, area.height);
            var music = new Rect(sfx.xMax + 4f, area.y, area.width - sfx.width - 4f, area.height);
            DropZone(sfx, "Drop SFX clips or folders", false);
            DropZone(music, "Drop Music clips or folders\n(one looping cue per file)", true);

            bool group = EditorPrefs.GetBool(GroupPrefKey, true);
            bool newGroup = EditorGUILayout.ToggleLeft(new GUIContent("Combine numbered SFX into one cue (hit_01, hit_02 → hit)",
                "Each file becomes a random variation of the same cue."), group, EditorStyles.miniLabel);
            if (newGroup != group) EditorPrefs.SetBool(GroupPrefKey, newGroup);
        }

        private void DropZone(Rect rect, string label, bool music)
        {
            Event e = Event.current;
            bool hover = (e.type == EventType.DragUpdated || e.type == EventType.DragPerform || e.type == EventType.Repaint)
                         && DragAndDrop.objectReferences.Length > 0 && rect.Contains(e.mousePosition);
            Color old = GUI.backgroundColor;
            if (hover) GUI.backgroundColor = new Color(0.6f, 0.85f, 1f);
            GUI.Box(rect, label, dropStyle);
            GUI.backgroundColor = old;

            if ((e.type != EventType.DragUpdated && e.type != EventType.DragPerform) || !rect.Contains(e.mousePosition)) return;
            List<AudioClip> clips = CollectClips(DragAndDrop.objectReferences);
            if (clips.Count == 0) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                bool group = EditorPrefs.GetBool(GroupPrefKey, true);
                pending = () => AddClips(clips, music, group);
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

        /// <summary>
        /// Creates cues from clips. SFX: snake_case id, numbered files optionally combined. Music: one looping 2D cue per
        /// file on the Music channel. Clips whose cue already exists are added to it.
        /// </summary>
        private void AddClips(List<AudioClip> clips, bool music, bool groupVariations)
        {
            Mutate(music ? "Add Music Cues" : "Add SFX Cues", lib =>
            {
                int first = -1;
                foreach (AudioClip clip in clips)
                {
                    string baseName = !music && groupVariations ? AudioEditorUtility.StripVariationSuffix(clip.name) : clip.name;
                    string id = AudioEditorUtility.ToSnakeCase(baseName);
                    if (id.Length == 0) id = "cue";
                    AudioCue cue = lib.FindCue(id);
                    if (cue == null)
                    {
                        cue = new AudioCue { id = id };
                        if (music)
                        {
                            cue.channel = AudioChannel.Music;
                            cue.loop = true;
                            cue.spatialBlend = 0f;
                            cue.playMode = AudioClipPlayMode.First;
                            cue.priority = 0;
                        }
                        lib.cues.Add(cue);
                    }
                    if (!cue.clips.Contains(clip)) cue.clips.Add(clip);
                    if (first < 0) first = lib.cues.IndexOf(cue);
                }
                return first;
            });
            search = "";
            channelFilter = AllChannels;
        }

        // ---------------------------------------------------------------- list

        private bool Matches(AudioCue cue)
        {
            if (cue == null) return false;
            if (channelFilter != AllChannels && cue.channel.Name != channelFilter) return false;
            if (string.IsNullOrEmpty(search)) return true;
            if (cue.id != null && cue.id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            foreach (string tag in cue.tags)
            {
                if (tag != null && tag.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private int CountVisible(AudioLibrary lib)
        {
            int n = 0;
            foreach (AudioCue cue in lib.cues)
            {
                if (Matches(cue)) n++;
            }
            return n;
        }

        private void DrawList(AudioLibrary lib)
        {
            int shown = 0;
            for (int i = 0; i < lib.cues.Count; i++)
            {
                if (!Matches(lib.cues[i])) continue;
                DrawRow(lib, i);
                shown++;
            }
            if (lib.cues.Count == 0) EditorGUILayout.LabelField("No cues yet. Drop clips on the zones above.", EditorStyles.centeredGreyMiniLabel);
            else if (shown == 0) EditorGUILayout.LabelField("No cue matches the filter.", EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawRow(AudioLibrary lib, int index)
        {
            AudioCue cue = lib.cues[index];
            Rect row = GUILayoutUtility.GetRect(10f, RowHeight, GUILayout.ExpandWidth(true));
            Event e = Event.current;
            if (e.type == EventType.Repaint && index == selected) EditorGUI.DrawRect(row, SelectedColor);

            var play = new Rect(row.x + 2f, row.y + 1f, 24f, RowHeight - 2f);
            var channel = new Rect(row.xMax - 66f, row.y, 62f, RowHeight);
            var warn = new Rect(channel.x - 18f, row.y + 2f, 16f, 16f);
            var label = new Rect(play.xMax + 6f, row.y, warn.x - play.xMax - 8f, RowHeight);

            bool playing = AudioEditorUtility.IsPreviewing(cue);
            if (GUI.Button(play, playing ? stopIcon : playIcon, EditorStyles.miniButton))
            {
                if (playing) AudioEditorUtility.StopPreview();
                else AudioEditorUtility.Preview(cue);
            }
            GUI.Label(label, string.IsNullOrEmpty(cue.id) ? "<no id>" : cue.id, IsBadId(cue.id) ? errorLabel : EditorStyles.label);
            if (HasMissingClips(cue)) GUI.Label(warn, warnIcon);
            GUI.Label(channel, cue.channel.Name, channelLabel);

            if (!row.Contains(e.mousePosition) || play.Contains(e.mousePosition)) return;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                Select(index);
                e.Use();
            }
            else if (e.type == EventType.ContextClick || (e.type == EventType.MouseDown && e.button == 1))
            {
                Select(index);
                ShowRowMenu(lib, index);
                e.Use();
            }
        }

        private void ShowRowMenu(AudioLibrary lib, int index)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Duplicate"), false, () => Duplicate(index));
            menu.AddItem(new GUIContent("Delete"), false, () => Delete(index));
            menu.AddSeparator("");
            if (index > 0) menu.AddItem(new GUIContent("Move Up"), false, () => Move(index, index - 1));
            else menu.AddDisabledItem(new GUIContent("Move Up"));
            if (index < lib.cues.Count - 1) menu.AddItem(new GUIContent("Move Down"), false, () => Move(index, index + 1));
            else menu.AddDisabledItem(new GUIContent("Move Down"));
            menu.ShowAsContext();
        }

        private void Select(int index)
        {
            selected = index;
            GUI.FocusControl(null);
            Repaint();
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

        // ---------------------------------------------------------------- detail

        private void DrawDetail(AudioLibrary lib)
        {
            EditorGUILayout.Space();
            if (selected < 0 || selected >= lib.cues.Count || lib.cues[selected] == null)
            {
                EditorGUILayout.HelpBox(lib.cues.Count == 0 ? "Drop clips above to create your first cue." : "Select a cue to edit it.", MessageType.Info);
                return;
            }

            AudioCue cue = lib.cues[selected];
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(string.IsNullOrEmpty(cue.id) ? "<no id>" : cue.id, EditorStyles.boldLabel);
            int index = selected;
            if (GUILayout.Button("Duplicate", EditorStyles.miniButtonLeft, GUILayout.Width(70f))) pending = () => Duplicate(index);
            if (GUILayout.Button("Delete", EditorStyles.miniButtonRight, GUILayout.Width(55f))) pending = () => Delete(index);
            EditorGUILayout.EndHorizontal();

            if (string.IsNullOrEmpty(cue.id)) EditorGUILayout.HelpBox("Empty id: this cue cannot be played.", MessageType.Error);
            else if (IsBadId(cue.id)) EditorGUILayout.HelpBox("Another cue in this library has the same id.", MessageType.Error);
            string other = OtherLibraryWithId(lib, cue.id);
            if (other != null) EditorGUILayout.HelpBox("Id also used in library '" + other + "'. The library registered last wins.", MessageType.Warning);

            SerializedProperty cueProp = cuesProp.GetArrayElementAtIndex(selected);
            if (shownInDetail != selected)
            {
                // Clips are the most edited field: show them expanded whenever another cue is selected.
                cueProp.FindPropertyRelative("clips").isExpanded = true;
                shownInDetail = selected;
            }
            AudioCueGUI.Draw(cueProp);
        }

        private void DrawPlaylists()
        {
            EditorGUILayout.Space();
            bool open = SessionState.GetBool(PlaylistsFoldoutKey, false);
            bool newOpen = EditorGUILayout.Foldout(open, "Playlists (" + playlistsProp.arraySize + ")", true);
            if (newOpen != open) SessionState.SetBool(PlaylistsFoldoutKey, newOpen);
            if (!newOpen) return;
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(playlistsProp, GUIContent.none, true);
            EditorGUI.indentLevel--;
        }

        private static string OtherLibraryWithId(AudioLibrary lib, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (AudioEditorUtility.CueInfo info in AudioEditorUtility.Cues)
            {
                if (info.id == id && info.library != lib) return info.library.name;
            }
            return null;
        }

        // ---------------------------------------------------------------- edits

        private void Duplicate(int index)
        {
            Mutate("Duplicate Audio Cue", l =>
            {
                AudioCue copy = JsonUtility.FromJson<AudioCue>(JsonUtility.ToJson(l.cues[index]));
                copy.id = UniqueId(l, copy.id);
                l.cues.Insert(index + 1, copy);
                return index + 1;
            });
        }

        private void Delete(int index)
        {
            Mutate("Delete Audio Cue", l =>
            {
                l.cues.RemoveAt(index);
                return Mathf.Min(index, l.cues.Count - 1);
            });
        }

        private void Move(int from, int to)
        {
            Mutate("Move Audio Cue", l =>
            {
                AudioCue cue = l.cues[from];
                l.cues.RemoveAt(from);
                l.cues.Insert(to, cue);
                return to;
            });
        }

        /// <summary>Applies a change with undo; the change returns the index to select (-1 keeps the current cue).</summary>
        private void Mutate(string undoName, Func<AudioLibrary, int> change)
        {
            AudioLibrary lib = Library;
            AudioCue current = selected >= 0 && selected < lib.cues.Count ? lib.cues[selected] : null;
            Undo.RecordObject(lib, undoName);
            int select = change(lib);
            selected = select >= 0 ? select : lib.cues.IndexOf(current);
            EditorUtility.SetDirty(lib);
            serializedObject.Update();
            GUI.FocusControl(null);
            AudioEditorUtility.Invalidate();
            Audio.NotifyLibraryChanged(lib);
            Repaint();
        }

        private static string UniqueId(AudioLibrary lib, string id)
        {
            if (lib.FindCue(id) == null) return id;
            int n = 2;
            while (lib.FindCue(id + "_" + n) != null) n++;
            return id + "_" + n;
        }
    }

    /// <summary>Layout of a single cue: common settings first, advanced settings in collapsed sections.</summary>
    internal static class AudioCueGUI
    {
        internal static void Draw(SerializedProperty cue)
        {
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

            EditorGUILayout.PropertyField(cue.FindPropertyRelative("channel"));
            if (clips.arraySize > 1) EditorGUILayout.PropertyField(cue.FindPropertyRelative("playMode"), new GUIContent("Pick Clip"));
            MinMax(cue.FindPropertyRelative("volumeRange"), "Volume", 0f, 1f);
            MinMax(cue.FindPropertyRelative("pitchRange"), "Pitch", 0.1f, 3f);
            EditorGUILayout.PropertyField(cue.FindPropertyRelative("loop"));

            if (Section("3D Sound"))
            {
                SerializedProperty blend = cue.FindPropertyRelative("spatialBlend");
                EditorGUILayout.PropertyField(blend, new GUIContent("Spatial Blend", "0 = 2D, 1 = 3D. Only used when played at a position or on a transform."));
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
                EndSection();
            }

            if (Section("Limits"))
            {
                EditorGUILayout.PropertyField(cue.FindPropertyRelative("cooldown"), new GUIContent("Cooldown (s)", "Minimum time between two plays."));
                SerializedProperty maxInstances = cue.FindPropertyRelative("maxInstances");
                EditorGUILayout.PropertyField(maxInstances, new GUIContent("Max Instances", "0 = no limit."));
                if (maxInstances.intValue < 0) maxInstances.intValue = 0;
                if (maxInstances.intValue > 0) EditorGUILayout.PropertyField(cue.FindPropertyRelative("stealPolicy"), new GUIContent("When Full"));
                EditorGUILayout.PropertyField(cue.FindPropertyRelative("priority"));
                EndSection();
            }

            if (Section("Timing"))
            {
                EditorGUILayout.PropertyField(cue.FindPropertyRelative("startDelay"), new GUIContent("Start Delay (s)"));
                EditorGUILayout.PropertyField(cue.FindPropertyRelative("fadeIn"), new GUIContent("Fade In (s)"));
                EditorGUILayout.PropertyField(cue.FindPropertyRelative("fadeOut"), new GUIContent("Fade Out (s)"));
                EndSection();
            }

            if (Section("Tags"))
            {
                EditorGUILayout.PropertyField(cue.FindPropertyRelative("tags"), GUIContent.none, true);
                EndSection();
            }
        }

        private static bool Section(string title)
        {
            string key = "kinatraa.audio.foldout." + title;
            bool open = SessionState.GetBool(key, false);
            bool newOpen = EditorGUILayout.Foldout(open, title, true);
            if (newOpen != open) SessionState.SetBool(key, newOpen);
            if (newOpen) EditorGUI.indentLevel++;
            return newOpen;
        }

        private static void EndSection()
        {
            EditorGUI.indentLevel--;
        }

        internal static void MinMax(SerializedProperty property, string label, float min, float max)
        {
            Vector2 value = property.vector2Value;
            EditorGUI.BeginChangeCheck();
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
            if (!EditorGUI.EndChangeCheck()) return;
            a = Mathf.Clamp(a, min, max);
            property.vector2Value = new Vector2(a, Mathf.Clamp(b, a, max));
        }
    }

    /// <summary>Dockable library editor with a library picker and first-run setup.</summary>
    internal sealed class AudioLibraryWindow : EditorWindow
    {
        [SerializeField] private AudioLibrary library;
        private AudioLibraryEditor editor;

        [MenuItem(AudioEditorUtility.MenuRoot + "Library Window", false, 0)]
        internal static void Open()
        {
            GetWindow<AudioLibraryWindow>("Audio Library").Show();
        }

        internal static void Open(AudioLibrary lib)
        {
            AudioLibraryWindow window = GetWindow<AudioLibraryWindow>("Audio Library");
            window.library = lib;
            window.Show();
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
            List<AudioLibrary> all = AudioEditorUtility.Libraries;
            if (library == null && all.Count > 0) library = all[0];

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            library = (AudioLibrary)EditorGUILayout.ObjectField(library, typeof(AudioLibrary), false, GUILayout.MinWidth(160f));
            if (GUILayout.Button("Libraries", EditorStyles.toolbarDropDown, GUILayout.Width(70f)))
            {
                var menu = new GenericMenu();
                foreach (AudioLibrary lib in all)
                {
                    AudioLibrary captured = lib;
                    menu.AddItem(new GUIContent(lib.name), lib == library, () => library = captured);
                }
                if (all.Count > 0) menu.AddSeparator("");
                menu.AddItem(new GUIContent("New Library..."), false, NewLibrary);
                menu.ShowAsContext();
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Config", EditorStyles.toolbarButton, GUILayout.Width(50f)))
            {
                AudioSystemConfig config = AudioEditorUtility.FindConfig();
                if (config != null) Selection.activeObject = config;
            }
            EditorGUILayout.EndHorizontal();

            if (library == null)
            {
                DrawSetup();
                return;
            }
            if (editor == null || editor.target != library)
            {
                if (editor != null) DestroyImmediate(editor);
                editor = (AudioLibraryEditor)UnityEditor.Editor.CreateEditor(library, typeof(AudioLibraryEditor));
            }
            editor.DrawLayout(true);
        }

        private void DrawSetup()
        {
            EditorGUILayout.Space();
            if (AudioEditorUtility.FindConfig() == null)
            {
                EditorGUILayout.HelpBox("Audio is not set up in this project yet. This creates Assets/Resources/kinatraaAudioConfig " +
                    "(loaded automatically at startup) and an empty library at Assets/Audio/AudioLibrary.", MessageType.Info);
                if (GUILayout.Button("Create Audio Setup", GUILayout.Height(30f))) library = AudioSetup.CreateDefaultSetup();
            }
            else
            {
                EditorGUILayout.HelpBox("There is no audio library yet.", MessageType.Info);
                if (GUILayout.Button("Create Library", GUILayout.Height(30f))) library = AudioSetup.CreateLibrary("Assets/Audio/AudioLibrary.asset");
            }
        }

        private void NewLibrary()
        {
            string path = EditorUtility.SaveFilePanelInProject("New Audio Library", "AudioLibrary", "asset", "Create an audio library");
            if (!string.IsNullOrEmpty(path)) library = AudioSetup.CreateLibrary(path);
        }
    }
}
