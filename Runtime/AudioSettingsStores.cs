using System;
using UnityEngine;

namespace kinatraa.AudioSystem
{
    /// <summary>Default settings store backed by <see cref="PlayerPrefs"/>.</summary>
    public sealed class PlayerPrefsAudioSettingsStore : IAudioSettingsStore
    {
        private readonly string prefix;

        /// <summary>Creates the store.</summary>
        /// <param name="prefix">Prefix of every PlayerPrefs key, e.g. "kinatraa.audio.".</param>
        public PlayerPrefsAudioSettingsStore(string prefix = "kinatraa.audio.")
        {
            this.prefix = prefix ?? "";
        }

        /// <inheritdoc/>
        public bool TryLoad(string key, out float value)
        {
            string k = prefix + key;
            value = PlayerPrefs.GetFloat(k, 1f);
            return PlayerPrefs.HasKey(k);
        }

        /// <inheritdoc/>
        public void Save(string key, float value)
        {
            PlayerPrefs.SetFloat(prefix + key, value);
        }

        /// <inheritdoc/>
        public bool TryLoadMuted(string key, out bool muted)
        {
            string k = prefix + key + ".muted";
            muted = PlayerPrefs.GetInt(k, 0) != 0;
            return PlayerPrefs.HasKey(k);
        }

        /// <inheritdoc/>
        public void SaveMuted(string key, bool muted)
        {
            PlayerPrefs.SetInt(prefix + key + ".muted", muted ? 1 : 0);
        }
    }

    /// <summary>
    /// Settings store built from delegates, for hooking a save system without writing a class.
    /// Created by <see cref="Audio.BindSettings"/>.
    /// </summary>
    public sealed class DelegateAudioSettingsStore : IAudioSettingsStore
    {
        private readonly Func<string, float?> load;
        private readonly Action<string, float> save;
        private readonly Func<string, bool?> loadMuted;
        private readonly Action<string, bool> saveMuted;

        /// <summary>Creates the store. Mute delegates are optional.</summary>
        /// <param name="load">Returns the saved volume for a key, or null when none is saved.</param>
        /// <param name="save">Saves a volume for a key.</param>
        /// <param name="loadMuted">Returns the saved mute state for a key, or null when none is saved.</param>
        /// <param name="saveMuted">Saves a mute state for a key.</param>
        public DelegateAudioSettingsStore(Func<string, float?> load, Action<string, float> save,
            Func<string, bool?> loadMuted = null, Action<string, bool> saveMuted = null)
        {
            this.load = load;
            this.save = save;
            this.loadMuted = loadMuted;
            this.saveMuted = saveMuted;
        }

        /// <inheritdoc/>
        public bool TryLoad(string key, out float value)
        {
            float? v = load != null ? load(key) : null;
            value = v ?? 1f;
            return v.HasValue;
        }

        /// <inheritdoc/>
        public void Save(string key, float value)
        {
            if (save != null) save(key, value);
        }

        /// <inheritdoc/>
        public bool TryLoadMuted(string key, out bool muted)
        {
            bool? v = loadMuted != null ? loadMuted(key) : null;
            muted = v ?? false;
            return v.HasValue;
        }

        /// <inheritdoc/>
        public void SaveMuted(string key, bool muted)
        {
            if (saveMuted != null) saveMuted(key, muted);
        }
    }
}
