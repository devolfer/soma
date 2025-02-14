using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Devolfer.Soma
{
    /// <summary>
    /// Provides various functionality for handling the volume of an <see cref="AudioMixerGroup"/>.
    /// </summary>
    [Serializable]
    public class SomaVolumeMixerGroup
    {
        [Tooltip("The Audio Mixer of the group.")]
        [SerializeField] private AudioMixer _audioMixer;

        [Tooltip(
            "The name of the exposed parameter of the group, e.g. 'VolumeMusic'" +
            "\n\nYou will have to create an Audio Mixer Group and expose a parameter for it in the Editor yourself. " +
            "Check the documentation or any online-guides, if unsure how to do this.")]
        [SerializeField] private string _exposedParameter;

        [Tooltip(
            "The amount of segments the volume range of 0.0 - 1.0 will be split into. " +
            "This will allow increasing/decreasing the volume in steps (e.g. 10 segments = 0.1 steps)." +
            "\n\nSetting the volume to a specific value is of course still possible.")]
        [SerializeField, Min(1)] private int _volumeSegments;

        /// <summary>
        /// The Audio Mixer the group is a part of.
        /// </summary>
        public AudioMixer AudioMixer => _audioMixer;

        /// <summary>
        /// The name that exposes the Audio Mixer Groups' volume and allows manipulating it.
        /// </summary>
        public string ExposedParameter => _exposedParameter;

        /// <summary>
        /// The amount of volume segments the range 0.0 to 1.0 is split into.
        /// </summary>
        public int VolumeSegments => _volumeSegments;

        /// <summary>
        /// The current volume from 0.0 to 1.0.
        /// </summary>
        public float VolumeCurrent => _volumeCurrent;

        /// <summary>
        /// The current dB from -80 to 0.
        /// </summary>
        public float DecibelCurrent => _volumeCurrent != 0 ? Mathf.Log10(_volumeCurrent) * 20 : -80;

        /// <summary>
        /// Is the group muted?
        /// </summary>
        public bool Muted => _muted;

        private float _volumeCurrent = 1;
        private float _volumePrevious = 1;
        private bool _muted;

        /// <param name="audioMixer">The associated Audio Mixer of the group.</param>
        /// <param name="exposedParameter">The name that exposes the Audio Mixer Groups' volume and allows manipulating it.</param>
        /// <param name="volumeSegments">Splits the volume range into the specified amount of segments and allows setting volume in steps.</param>
        public SomaVolumeMixerGroup(AudioMixer audioMixer, string exposedParameter, int volumeSegments = 1)
        {
            _audioMixer = audioMixer;
            _exposedParameter = exposedParameter;
            _volumeSegments = volumeSegments;

            if (Application.isPlaying) Refresh();
        }

        internal void Set(float volume)
        {
            if (!_audioMixer.TrySetVolume(_exposedParameter, ref volume)) return;

            _volumePrevious = _volumeCurrent;
            _volumeCurrent = volume;

            _muted = _volumeCurrent == 0;
        }

        internal void Increase()
        {
            if (_volumeSegments <= 1) return;

            float segmentStep = 1f / _volumeSegments;
            float volumeCurrentRounded = Mathf.Round(_volumeCurrent / segmentStep) * segmentStep;
            Set(volumeCurrentRounded + segmentStep);
        }

        internal void Decrease()
        {
            if (_volumeSegments <= 1) return;

            float segmentStep = 1f / _volumeSegments;
            float volumeCurrentRounded = Mathf.Round(_volumeCurrent / segmentStep) * segmentStep;
            Set(volumeCurrentRounded - segmentStep);
        }

        internal void Mute(bool value)
        {
            if (_muted == value) return;

            _muted = value;

            Set(_muted ? 0 : _volumePrevious);
        }

        internal void Refresh()
        {
            if (_audioMixer.TryGetVolume(_exposedParameter, out _volumeCurrent))
            {
                _volumePrevious = _volumeCurrent;
                _muted = _volumeCurrent == 0;
            }
            else
            {
                Debug.LogError(
                    $"Exposed Parameter {_exposedParameter} not found in {nameof(AudioMixer)} {_audioMixer}");
            }
        }
    }
}