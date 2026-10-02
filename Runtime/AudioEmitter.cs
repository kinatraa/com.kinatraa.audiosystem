using UnityEngine;

namespace kinatraa.AudioSystem
{
    /// <summary>
    /// Plays a cue from a GameObject: on enable, on start, on trigger enter, or from a UnityEvent
    /// (wire <see cref="Play"/>, <see cref="Stop"/> or <see cref="PlayCue"/> in the inspector).
    /// </summary>
    [AddComponentMenu("kinatraa/Audio/Audio Emitter")]
    public class AudioEmitter : MonoBehaviour
    {
        /// <summary>When the emitter plays automatically.</summary>
        public enum PlayTrigger
        {
            /// <summary>Only when <see cref="Play"/> is called (e.g. from a UnityEvent).</summary>
            Manual,
            /// <summary>Every time the component is enabled.</summary>
            OnEnable,
            /// <summary>Once, on Start.</summary>
            OnStart,
            /// <summary>When a collider enters this object's 3D or 2D trigger.</summary>
            OnTriggerEnter
        }

        /// <summary>Cue to play.</summary>
        [AudioCueId] public string cueId = "";

        /// <summary>When to play automatically.</summary>
        public PlayTrigger playOn = PlayTrigger.OnEnable;

        /// <summary>Play in 3D at this object (following it). When false the cue plays in 2D.</summary>
        public bool positional = true;

        /// <summary>Stop the sound when this component is disabled or destroyed.</summary>
        public bool stopOnDisable = true;

        private AudioHandle handle;

        /// <summary>Handle of the last sound played by this emitter.</summary>
        public AudioHandle Handle
        {
            get { return handle; }
        }

        private void OnEnable()
        {
            if (playOn == PlayTrigger.OnEnable) Play();
        }

        private void Start()
        {
            if (playOn == PlayTrigger.OnStart) Play();
        }

        private void OnDisable()
        {
            if (stopOnDisable) handle.Stop();
        }

        // Parameterless message signatures keep this package free of the physics modules.
        private void OnTriggerEnter()
        {
            if (playOn == PlayTrigger.OnTriggerEnter) Play();
        }

        private void OnTriggerEnter2D()
        {
            if (playOn == PlayTrigger.OnTriggerEnter) Play();
        }

        /// <summary>Plays <see cref="cueId"/>.</summary>
        public void Play()
        {
            PlayCue(cueId);
        }

        /// <summary>Plays any cue from this emitter.</summary>
        /// <param name="id">Cue id.</param>
        public void PlayCue(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            handle = positional ? Audio.Play(id, transform) : Audio.Play(id);
        }

        /// <summary>Stops the last sound played by this emitter (with the cue's fade-out).</summary>
        public void Stop()
        {
            handle.Stop();
        }
    }
}
