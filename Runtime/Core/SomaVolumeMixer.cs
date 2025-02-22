using UnityEngine;

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

        private bool _hasRegistered;
        
        private void OnDestroy() => LazyUnregister();

        /// <summary>
        /// Sets volume of set <see cref="SomaVolumeMixerGroup"/> with <see cref="Soma"/>.
        /// </summary>
        /// <param name="volume">The volumes' new value.</param>
        public void Set(float volume)
        {
            LazyRegister();

            Soma.SetMixerGroupVolume(_group.ExposedParameter, volume);
        }

        /// <summary>
        /// Step-wise increases volume of the <see cref="SomaVolumeMixerGroup"/> with <see cref="Soma"/>.
        /// </summary>
        public void Increase()
        {
            LazyRegister();

            Soma.IncreaseMixerGroupVolume(_group.ExposedParameter);
        }

        /// <summary>
        /// Step-wise decreases volume of the <see cref="SomaVolumeMixerGroup"/> with <see cref="Soma"/>.
        /// </summary>
        public void Decrease()
        {
            LazyRegister();

            Soma.DecreaseMixerGroupVolume(_group.ExposedParameter);
        }

        /// <summary>
        /// Mutes/Un-mutes volume of the <see cref="SomaVolumeMixerGroup"/> with <see cref="Soma"/>.
        /// </summary>
        /// <param name="muted">True = muted, False = unmuted.</param>
        public void Mute(bool muted)
        {
            LazyRegister();

            Soma.MuteMixerGroupVolume(_group.ExposedParameter, muted);
        }

        /// <summary>
        /// Fades volume of the <see cref="SomaVolumeMixerGroup"/> with <see cref="Soma"/>.
        /// </summary>
        /// <param name="targetVolume">The target volume reached at the end of the fade.</param>
        public void Fade(float targetVolume)
        {
            LazyRegister();

            Soma.FadeMixerGroupVolume(
                _group.ExposedParameter,
                targetVolume,
                _fadeConfiguration.FadeDuration,
                ease: _fadeConfiguration.FadeEase,
                onComplete: _fadeConfiguration.OnComplete.Invoke);
        }

        private void LazyRegister()
        {
            if (_hasRegistered) return;

            _hasRegistered = true;
            Soma.RegisterMixerVolumeGroup(_group);
        }

        private void LazyUnregister()
        {
            if (!_hasRegistered) return;

            _hasRegistered = false;
            Soma.UnregisterMixerVolumeGroup(_group);
        }
    }
}