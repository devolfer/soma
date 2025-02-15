using System;
using UnityEngine;
using UnityEngine.Events;

namespace Devolfer.Soma
{
    /// <summary>
    /// Allows playback of an <see cref="AudioSource"/> through <see cref="Soma"/>.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SomaEmitter : MonoBehaviour
    {
        [Header("Configurations")]
        [Space]
        [SerializeField] private PlayConfiguration _play;

        [Space]
        [SerializeField] private StopConfiguration _stop;

        [Space]
        [SerializeField] private FadeConfiguration _fade;

        private AudioSource _source;
        private Transform _transform;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _transform = transform;

            if (_source.playOnAwake) Play();
        }

        /// <summary>
        /// Plays attached <see cref="AudioSource"/> with <see cref="Soma"/>.
        /// </summary>
        public void Play()
        {
            Soma.Play(
                _source,
                followTarget: _play.Follow ? _transform : null,
                position: _play.Position,
                fadeIn: _play.FadeIn,
                fadeInDuration: _play.FadeInDuration,
                fadeInEase: _play.FadeInEase,
                onComplete: _play.OnComplete.Invoke);
        }

        /// <summary>
        /// Pauses attached <see cref="AudioSource"/> with <see cref="Soma"/>.
        /// </summary>
        public void Pause()
        {
            Soma.Pause(_source);
        }

        /// <summary>
        /// Resumes attached <see cref="AudioSource"/> with <see cref="Soma"/>.
        /// </summary>
        public void Resume()
        {
            Soma.Resume(_source);
        }

        /// <summary>
        /// Stops attached <see cref="AudioSource"/> with <see cref="Soma"/>.
        /// </summary>
        public void Stop()
        {
            Soma.Stop(
                _source,
                fadeOut: _stop.FadeOut,
                fadeOutDuration: _stop.FadeOutDuration,
                fadeOutEase: _stop.FadeOutEase,
                onComplete: _stop.OnComplete.Invoke);
        }

        /// <summary>
        /// Fades attached <see cref="AudioSource"/> with <see cref="Soma"/>.
        /// </summary>
        /// <param name="targetVolume">The target volume the fade will reach at the end.</param>
        public void Fade(float targetVolume)
        {
            Soma.Fade(
                _source,
                targetVolume,
                _fade.FadeDuration,
                ease: _fade.FadeEase,
                onComplete: _fade.OnComplete.Invoke);
        }

        [Serializable]
        private class PlayConfiguration
        {
            [Tooltip("Should the sound follow the transform this script is attached to when playing?")]
            public bool Follow = true;

            [Tooltip(
                "Either the global position or, when following, the relative position offset at which the sound is played.")]
            public Vector3 Position;

            [Tooltip("Should the sound fade in when playing?")]
            public bool FadeIn;

            [ShowIf("FadeIn")]
            [Tooltip("The duration in seconds the fading in will take.")]
            public float FadeInDuration = .5f;

            [ShowIf("FadeIn")]
            [Tooltip("The easing applied when fading in.")]
            public Ease FadeInEase = Ease.Linear;

            [Space]
            [Tooltip("Event invoked once sound completes playing.")]
            public UnityEvent OnComplete;
        }

        [Serializable]
        private class StopConfiguration
        {
            [Tooltip("Should the sound fade out when stopping?")]
            public bool FadeOut = true;

            [ShowIf("FadeOut")]
            [Tooltip("The duration in seconds the fading out will take.")]
            public float FadeOutDuration = .5f;

            [ShowIf("FadeOut")]
            [Tooltip("The easing applied when fading out.")]
            public Ease FadeOutEase = Ease.Linear;

            [Space]
            [Tooltip("Event invoked once sound completes stopping.")]
            public UnityEvent OnComplete;
        }

        [Serializable]
        private class FadeConfiguration
        {
            [Tooltip("The duration in seconds the fade will take.")]
            public float FadeDuration = 1f;

            [Tooltip("The easing applied when fading.")]
            public Ease FadeEase = Ease.Linear;

            [Space]
            [Tooltip("Event invoked once sound completes fading.")]
            public UnityEvent OnComplete;
        }
    }
}