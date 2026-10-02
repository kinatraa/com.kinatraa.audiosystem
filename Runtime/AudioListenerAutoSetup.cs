using UnityEngine;
using UnityEngine.SceneManagement;

namespace kinatraa.AudioSystem
{
    /// <summary>
    /// Ensures exactly one enabled AudioListener: keeps (or adds) the one on this GameObject and disables the others,
    /// also after additive scene loads. Put it on your main camera.
    /// </summary>
    [AddComponentMenu("kinatraa/Audio/Audio Listener Auto Setup")]
    [DefaultExecutionOrder(-1000)]
    public class AudioListenerAutoSetup : MonoBehaviour
    {
        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Apply();
        }

        /// <summary>Enables this object's listener (adding one if needed) and disables all others.</summary>
        public void Apply()
        {
            AudioListener mine = GetComponent<AudioListener>();
            if (mine == null) mine = gameObject.AddComponent<AudioListener>();
            mine.enabled = true;
#if UNITY_2023_1_OR_NEWER
            AudioListener[] all = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
#else
            AudioListener[] all = FindObjectsOfType<AudioListener>();
#endif
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != mine && all[i].enabled) all[i].enabled = false;
            }
        }
    }
}
