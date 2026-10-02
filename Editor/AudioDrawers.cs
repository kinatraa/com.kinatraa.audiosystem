using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace kinatraa.AudioSystem.Editor
{
    /// <summary>Dropdown of the channels defined in the config (plus built-ins).</summary>
    [CustomPropertyDrawer(typeof(AudioChannel))]
    internal sealed class AudioChannelDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty nameProp = property.FindPropertyRelative("name");
            string current = string.IsNullOrEmpty(nameProp.stringValue) ? AudioChannel.SfxName : nameProp.stringValue;
            string[] names = AudioEditorUtility.ChannelNames;
            int index = Array.IndexOf(names, current);
            int count = index < 0 ? names.Length + 1 : names.Length;
            var options = new GUIContent[count];
            for (int i = 0; i < names.Length; i++) options[i] = new GUIContent(names[i]);
            if (index < 0)
            {
                index = names.Length;
                options[index] = new GUIContent(current + " (not in config)");
            }

            label = EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUI.Popup(position, label, index, options);
            if (EditorGUI.EndChangeCheck() && selected < names.Length) nameProp.stringValue = names[selected];
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }

    /// <summary>Text field plus a searchable dropdown of every cue id in the project, for [AudioCueId] strings.</summary>
    [CustomPropertyDrawer(typeof(AudioCueIdAttribute))]
    internal sealed class AudioCueIdDrawer : PropertyDrawer
    {
        private static readonly Color UnknownColor = new Color(1f, 0.55f, 0.55f);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "[AudioCueId] requires a string field.");
                return;
            }

            label = EditorGUI.BeginProperty(position, label, property);
            string value = property.stringValue;
            bool known = string.IsNullOrEmpty(value) || AudioEditorUtility.HasCue(value);
            if (!known) label.tooltip = "No library in the project contains the cue '" + value + "'.";

            var field = new Rect(position.x, position.y, position.width - 22f, EditorGUIUtility.singleLineHeight);
            var button = new Rect(field.xMax + 2f, position.y, 20f, EditorGUIUtility.singleLineHeight);

            Color oldColor = GUI.color;
            if (!known) GUI.color = UnknownColor;
            EditorGUI.BeginChangeCheck();
            string edited = EditorGUI.DelayedTextField(field, label, value);
            if (EditorGUI.EndChangeCheck()) property.stringValue = edited;
            GUI.color = oldColor;

            if (EditorGUI.DropdownButton(button, GUIContent.none, FocusType.Keyboard))
            {
                UnityEngine.Object[] targets = property.serializedObject.targetObjects;
                string path = property.propertyPath;
                var dropdown = new CueIdDropdown(new AdvancedDropdownState(), id =>
                {
                    var so = new SerializedObject(targets);
                    SerializedProperty p = so.FindProperty(path);
                    if (p == null) return;
                    p.stringValue = id;
                    so.ApplyModifiedProperties();
                });
                dropdown.Show(field);
            }
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        private sealed class CueIdDropdown : AdvancedDropdown
        {
            private readonly Action<string> onPick;

            private sealed class Item : AdvancedDropdownItem
            {
                public readonly string cueId;

                public Item(string name, string cueId) : base(name)
                {
                    this.cueId = cueId;
                }
            }

            public CueIdDropdown(AdvancedDropdownState state, Action<string> onPick) : base(state)
            {
                this.onPick = onPick;
                minimumSize = new Vector2(260f, 320f);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem("Audio Cues");
                root.AddChild(new Item("(none)", ""));
                var groups = new Dictionary<string, AdvancedDropdownItem>();
                var seen = new HashSet<string>();
                foreach (AudioEditorUtility.CueInfo info in AudioEditorUtility.Cues)
                {
                    if (!seen.Add(info.id)) continue;
                    AdvancedDropdownItem group;
                    if (!groups.TryGetValue(info.channel, out group))
                    {
                        group = new AdvancedDropdownItem(info.channel);
                        groups.Add(info.channel, group);
                        root.AddChild(group);
                    }
                    group.AddChild(new Item(info.id, info.id));
                }
                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                var picked = item as Item;
                if (picked != null) onPick(picked.cueId);
            }
        }
    }
}
