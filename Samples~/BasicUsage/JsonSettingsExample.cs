using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace kinatraa.AudioSystem.Samples
{
    /// <summary>Installs <see cref="JsonFileAudioSettingsStore"/> so volumes live in the game's own JSON file instead of PlayerPrefs.</summary>
    public class JsonSettingsExample : MonoBehaviour
    {
        /// <summary>File name inside Application.persistentDataPath.</summary>
        public string fileName = "audio-settings.json";

        private void Awake()
        {
            // Volumes are reloaded from the file immediately.
            Audio.SetSettingsStore(new JsonFileAudioSettingsStore(Path.Combine(Application.persistentDataPath, fileName)));
        }
    }

    /// <summary>Example custom store: keeps channel volumes and mute states in a JSON file.</summary>
    public class JsonFileAudioSettingsStore : IAudioSettingsStore
    {
        [Serializable]
        private class Entry
        {
            public string key;
            public float volume = 1f;
            public bool hasVolume;
            public bool muted;
            public bool hasMuted;
        }

        [Serializable]
        private class Data
        {
            public List<Entry> channels = new List<Entry>();
        }

        private readonly string path;
        private readonly Data data;

        /// <summary>Creates the store and reads the file if it exists.</summary>
        /// <param name="path">Absolute file path.</param>
        public JsonFileAudioSettingsStore(string path)
        {
            this.path = path;
            data = File.Exists(path) ? JsonUtility.FromJson<Data>(File.ReadAllText(path)) ?? new Data() : new Data();
        }

        /// <inheritdoc/>
        public bool TryLoad(string key, out float value)
        {
            Entry e = Find(key, false);
            value = e != null ? e.volume : 1f;
            return e != null && e.hasVolume;
        }

        /// <inheritdoc/>
        public void Save(string key, float value)
        {
            Entry e = Find(key, true);
            e.volume = value;
            e.hasVolume = true;
            Write();
        }

        /// <inheritdoc/>
        public bool TryLoadMuted(string key, out bool muted)
        {
            Entry e = Find(key, false);
            muted = e != null && e.muted;
            return e != null && e.hasMuted;
        }

        /// <inheritdoc/>
        public void SaveMuted(string key, bool muted)
        {
            Entry e = Find(key, true);
            e.muted = muted;
            e.hasMuted = true;
            Write();
        }

        private Entry Find(string key, bool create)
        {
            foreach (Entry e in data.channels)
            {
                if (e.key == key) return e;
            }
            if (!create) return null;
            var entry = new Entry { key = key };
            data.channels.Add(entry);
            return entry;
        }

        // Writes on every change for simplicity; batch writes (e.g. on slider release) in a real game.
        private void Write()
        {
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
        }
    }
}
