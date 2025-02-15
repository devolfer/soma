using System;
using UnityEngine;
using UnityEngine.Events;

namespace Devolfer.Soma
{
    /// <summary>
    /// Allows volume mixing of the defined <see cref="SomaVolumeMixerGroup"/> through <see cref="Soma"/>.
    /// </summary>
    public class SomaVolumeMixer : MonoBehaviour
    {
        [Tooltip("Add the Audio Mixer Group you wish here, that Soma can change the respective volume of.")]
        [SerializeField] private SomaVolumeMixerGroup _group;

        [Space]
        [SerializeField] private FadeConfiguration _fadeConfiguration;

        private bool _registered;

        private void Awake()
        {
            RegisterIfNeeded();
        }

        private void OnDestroy()
        {
            UnregisterIfNeeded();
        }

        /// <summary>
        /// Sets volume of set <see cref="SomaVolumeMixerGroup"/> with <see cref="Soma"/>.
        /// </summary>
        /// <param name="volume">The volumes' new value.</param>
        public void Set(float volume)
        {
            RegisterIfNeeded();

            Soma.SetMixerGroupVolume(_group.ExposedParameter, volume);
        }

        /// <summary>
        /// Step-wise increases volume of the <see cref="SomaVolumeMixerGroup"/> with <see cref="Soma"/>.
        /// </summary>
        public void Increase()
        {
            RegisterIfNeeded();

            Soma.IncreaseMixerGroupVolume(_group.ExposedParameter);
        }

        /// <summary>
        /// Step-wise decreases volume of the <see cref="SomaVolumeMixerGroup"/> with <see cref="Soma"/>.
        /// </summary>
        public void Decrease()
        {
            RegisterIfNeeded();

            Soma.DecreaseMixerGroupVolume(_group.ExposedParameter);
        }

        /// <summary>
        /// Mutes/Un-mutes volume of the <see cref="SomaVolumeMixerGroup"/> with <see cref="Soma"/>.
        /// </summary>
        /// <param name="muted">True = muted, False = unmuted.</param>
        public void Mute(bool muted)
        {
            RegisterIfNeeded();

            Soma.MuteMixerGroupVolume(_group.ExposedParameter, muted);
        }

        /// <summary>
        /// Fades volume of the <see cref="SomaVolumeMixerGroup"/> with <see cref="Soma"/>.
        /// </summary>
        /// <param name="targetVolume">The target volume reached at the end of the fade.</param>
        public void Fade(float targetVolume)
        {
            RegisterIfNeeded();

            Soma.FadeMixerGroupVolume(
                _group.ExposedParameter,
                targetVolume,
                _fadeConfiguration.FadeDuration,
                ease: _fadeConfiguration.FadeEase,
                onComplete: _fadeConfiguration.OnComplete.Invoke);
        }

        private void RegisterIfNeeded()
        {
            if (_registered) return;

            _registered = true;
            Soma.RegisterMixerVolumeGroup(_group);
        }

        private void UnregisterIfNeeded()
        {
            if (!_registered) return;

            Soma.UnregisterMixerVolumeGroup(_group);
        }

        [Serializable]
        private class FadeConfiguration
        {
            [Tooltip("The duration in seconds the fade will take.")]
            public float FadeDuration = 1f;

            [Tooltip("The easing applied when fading.")]
            public Ease FadeEase = Ease.Linear;

            [Space]
            [Tooltip("Event invoked once volume completes fading.")]
            public UnityEvent OnComplete;
        }
    }
}