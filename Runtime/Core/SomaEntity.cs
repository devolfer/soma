using System;
using UnityEngine;
using System.Threading;

#if UNITASK_INCLUDED
using Cysharp.Threading.Tasks;
using DynamicTask = Cysharp.Threading.Tasks.UniTask;
#endif

#if !UNITASK_INCLUDED
using System.Collections;
using DynamicTask = System.Threading.Tasks.Task;
#endif

namespace Devolfer.Soma
{
    /// <summary>
    /// An extended wrapper of an <see cref="AudioSource"/> that works with <see cref="Soma"/>. 
    /// </summary>
    [AddComponentMenu("")]
    public class SomaEntity : MonoBehaviour
    {
        /// <summary>
        /// Is the entity playing?
        /// </summary>
        public bool Playing { get; private set; }

        /// <summary>
        /// Is the entity paused?
        /// </summary>
        public bool Paused { get; private set; }

        /// <summary>
        /// Is the entity fading?
        /// </summary>
        public bool Fading { get; private set; }

        /// <summary>
        /// Is the entity stopping?
        /// </summary>
        public bool Stopping { get; private set; }
        
        /// <summary>
        /// The properties applied on this entity.
        /// </summary>
        public SomaProperties Properties => _properties;

        /// <summary>
        /// The AudioSource used for playing.
        /// </summary>
        public AudioSource AudioSource => _audioSource;

        /// <summary>
        /// Did the entity originate from an external AudioSource?
        /// </summary>
        /// <remarks>This means that the Play method of this entity was initiated via an AudioSource rather than from SomaProperties.</remarks>
        public bool FromExternalAudioSource { get; private set; }

        /// <summary>
        /// The external AudioSource this entity originated from.
        /// </summary>
        /// <remarks>Check <see cref="FromExternalAudioSource"/> to foresee a null reference.</remarks>
        public AudioSource ExternalAudioSource => _externalAudioSource;

        /// <summary>
        /// The global position of the entity.
        /// </summary>
        /// <remarks>Will be cheaper than calling 'this.transform.position'.</remarks>
        public Vector3 Position => _transform.position;

        /// <summary>
        /// Whether this entity is following the position of another transform.
        /// </summary>
        public bool HasFollowTarget => _hasFollowTarget;

        /// <summary>
        /// The transform this entity is following while playing.
        /// </summary>
        /// <remarks>Check <see cref="HasFollowTarget"/> to foresee a null reference.</remarks>
        public Transform FollowTarget => _followTarget;

        /// <summary>
        /// The position offset relative to the <see cref="FollowTarget"/>.
        /// </summary>
        public Vector3 FollowTargetOffset => _followTargetOffset;
        
        private Soma _manager;
        private SomaProperties _properties;
        private Transform _transform;
        private AudioSource _audioSource;
        private AudioSource _externalAudioSource;

        private bool _hasFollowTarget;
        private Transform _followTarget;
        private Vector3 _followTargetOffset;

        private bool _setup;

        private CancellationTokenSource _playCts;
        private CancellationTokenSource _fadeCts;
        private CancellationTokenSource _stopCts;
        private Func<bool> SourceIsPlayingOrPausedPredicate => () => (_setup && _audioSource.isPlaying) || Paused;
        private Func<bool> PausedPredicate => () => Paused;

#if !UNITASK_INCLUDED
        private Coroutine _playRoutine;
        private Coroutine _fadeRoutine;
        private Coroutine _stopRoutine;
        private WaitWhile _waitWhileSourceIsPlayingOrPaused;
        private WaitWhile _waitWhilePaused;
#endif

        internal void Setup(Soma manager)
        {
            _manager = manager;
            _properties = new SomaProperties(default(AudioClip));
            _transform = transform;
            if (!TryGetComponent(out _audioSource)) _audioSource = gameObject.AddComponent<AudioSource>();

#if !UNITASK_INCLUDED
            _waitWhileSourceIsPlayingOrPaused = new WaitWhile(SourceIsPlayingOrPausedPredicate);
            _waitWhilePaused = new WaitWhile(PausedPredicate);
#endif

            _setup = true;
        }

        private void LateUpdate()
        {
            if (!_setup) return;

            if (_hasFollowTarget && Playing)
            {
                _transform.position = _followTarget.position + _followTargetOffset;
            }
        }

        private void OnDestroy()
        {
            TaskHelper.Cancel(ref _playCts);
            TaskHelper.Cancel(ref _fadeCts);
            TaskHelper.Cancel(ref _stopCts);
        }

        internal SomaEntity Play(SomaProperties properties,
                                 Transform followTarget = null,
                                 Vector3 position = default,
                                 bool fadeIn = false,
                                 float fadeInDuration = .5f,
                                 Ease fadeInEase = Ease.Linear,
                                 Action onComplete = null)
        {
            SetProperties(properties, followTarget, position);

#if UNITASK_INCLUDED
            PlayTask(TaskHelper.CancelAndRefresh(ref _playCts)).Forget();

            return this;

            async UniTaskVoid PlayTask(CancellationToken cancellationToken)
            {
                Playing = true;

                _audioSource.Play();

                if (fadeIn)
                {
                    _audioSource.volume = 0;

                    await Soma.FadeTask(
                        _audioSource,
                        fadeInDuration,
                        properties.Volume,
                        fadeInEase,
                        PausedPredicate,
                        cancellationToken: cancellationToken);
                }

                await UniTask.WaitWhile(() => Playing || Paused, cancellationToken: cancellationToken);

                onComplete?.Invoke();

                await _manager.StopAsync_Entity(this, false, cancellationToken: cancellationToken);

                Playing = false;
            }
#else
            _playRoutine = _manager.StartCoroutine(PlayRoutine());
            
            return this;

            IEnumerator PlayRoutine()
            {
                Playing = true;
                _audioSource.Play();

                if (fadeIn)
                {
                    _audioSource.volume = 0;
                    yield return Soma.FadeRoutine(
                        _audioSource,
                        fadeInDuration,
                        properties.Volume,
                        fadeInEase,
                        _waitWhilePaused);
                }

                yield return _waitWhileSourceIsPlayingOrPaused;

                onComplete?.Invoke();

                _manager.Stop_Entity(this, false);
                Playing = false;
                _playRoutine = null;
            }
#endif
        }

        internal SomaEntity Play(AudioSource audioSource,
                                 Transform followTarget = null,
                                 Vector3 position = default,
                                 bool fadeIn = false,
                                 float fadeInDuration = .5f,
                                 Ease fadeInEase = Ease.Linear,
                                 Action onComplete = null)
        {
            _externalAudioSource = audioSource;
            FromExternalAudioSource = true;

            if (audioSource.isPlaying) audioSource.Stop();
            audioSource.enabled = false;

            SomaProperties properties = audioSource;

            return Play(properties, followTarget, position, fadeIn, fadeInDuration, fadeInEase, onComplete);
        }

        internal async DynamicTask PlayAsync(SomaProperties properties,
                                             Transform followTarget = null,
                                             Vector3 position = default,
                                             bool fadeIn = false,
                                             float fadeInDuration = .5f,
                                             Ease fadeInEase = Ease.Linear,
                                             CancellationToken cancellationToken = default)
        {
            if (cancellationToken == default)
            {
                cancellationToken = TaskHelper.CancelAndRefresh(ref _playCts);
            }
            else
            {
                TaskHelper.CancelAndRefresh(ref _playCts);
                TaskHelper.Link(ref cancellationToken, ref _playCts);
            }

            SetProperties(properties, followTarget, position);

            Playing = true;
            _audioSource.Play();

            if (fadeIn)
            {
                _audioSource.volume = 0;

                await Soma.FadeTask(
                    _audioSource,
                    fadeInDuration,
                    properties.Volume,
                    fadeInEase,
                    PausedPredicate,
                    cancellationToken);
            }

#if UNITASK_INCLUDED
            await UniTask.WaitWhile(SourceIsPlayingOrPausedPredicate, cancellationToken: cancellationToken);
#else
            await TaskHelper.WaitWhile(SourceIsPlayingOrPausedPredicate, cancellationToken: cancellationToken);
#endif

            await _manager.StopAsync_Entity(this, false, cancellationToken: cancellationToken);

            Playing = false;
        }

        internal DynamicTask PlayAsync(AudioSource audioSource,
                                       Transform followTarget = null,
                                       Vector3 position = default,
                                       bool fadeIn = false,
                                       float fadeInDuration = .5f,
                                       Ease fadeInEase = Ease.Linear,
                                       CancellationToken cancellationToken = default)
        {
            _externalAudioSource = audioSource;
            FromExternalAudioSource = true;

            if (audioSource.isPlaying) audioSource.Stop();
            audioSource.enabled = false;

            SomaProperties properties = audioSource;

            return PlayAsync(properties, followTarget, position, fadeIn, fadeInDuration, fadeInEase, cancellationToken);
        }

        internal void Pause()
        {
            if (Paused) return;

            Paused = true;
            _audioSource.Pause();
        }

        internal void Resume()
        {
            if (!Paused) return;

            _audioSource.UnPause();
            Paused = false;
        }

        internal void Stop(bool fadeOut = true,
                           float fadeOutDuration = .5f,
                           Ease fadeOutEase = Ease.Linear,
                           Action onComplete = null)
        {
            if (Stopping && fadeOut) return;

#if UNITASK_INCLUDED
            TaskHelper.Cancel(ref _stopCts);
            Stopping = false;
#else
            if (_stopRoutine != null) _manager.StopCoroutine(_stopRoutine);
            _stopRoutine = null;
#endif

            if (Playing)
            {
                Playing = false;
#if UNITASK_INCLUDED
                TaskHelper.Cancel(ref _playCts);
#else
                if (_playRoutine != null) _manager.StopCoroutine(_playRoutine);
                _playRoutine = null;
#endif
            }

            if (Fading)
            {
                Fading = false;
#if UNITASK_INCLUDED
                TaskHelper.Cancel(ref _fadeCts);
#else
                if (_fadeRoutine != null) _manager.StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
#endif
            }

            if (!fadeOut || Paused)
            {
                _audioSource.Stop();
                onComplete?.Invoke();

                ResetProperties();
            }
            else
            {
#if UNITASK_INCLUDED
                StopTask(TaskHelper.CancelAndRefresh(ref _stopCts)).Forget();

                return;

                async UniTaskVoid StopTask(CancellationToken cancellationToken)
                {
                    Stopping = true;

                    await Soma.FadeTask(
                        _audioSource,
                        fadeOutDuration,
                        0,
                        fadeOutEase,
                        cancellationToken: cancellationToken);

                    _audioSource.Stop();

                    onComplete?.Invoke();

                    Stopping = false;

                    ResetProperties();
                }
#else
                _stopRoutine = _manager.StartCoroutine(StopRoutine());

                return;

                IEnumerator StopRoutine()
                {
                    Stopping = true;

                    yield return Soma.FadeRoutine(_audioSource, fadeOutDuration, 0, fadeOutEase);

                    _audioSource.Stop();

                    onComplete?.Invoke();

                    Stopping = false;
                    _stopRoutine = null;
                    
                    ResetProperties();
                }
#endif
            }
        }

        internal async DynamicTask StopAsync(bool fadeOut = true,
                                             float fadeOutDuration = .5f,
                                             Ease fadeOutEase = Ease.Linear,
                                             CancellationToken cancellationToken = default)
        {
            if (Stopping && fadeOut) return;

            TaskHelper.Cancel(ref _stopCts);
            Stopping = false;
#if !UNITASK_INCLUDED
            if (_stopRoutine != null) _manager.StopCoroutine(_stopRoutine);
            _stopRoutine = null;
#endif

            if (Playing)
            {
                Playing = false;
                TaskHelper.Cancel(ref _playCts);
#if !UNITASK_INCLUDED
                if (_playRoutine != null) _manager.StopCoroutine(_playRoutine);
                _playRoutine = null;
#endif
            }

            if (Fading)
            {
                Fading = false;
                TaskHelper.Cancel(ref _fadeCts);
#if !UNITASK_INCLUDED
                if (_fadeRoutine != null) _manager.StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
#endif
            }

            if (!fadeOut || Paused)
            {
                _audioSource.Stop();
                ResetProperties();
            }
            else
            {
                if (cancellationToken == default)
                {
                    cancellationToken = TaskHelper.CancelAndRefresh(ref _stopCts);
                }
                else
                {
                    TaskHelper.CancelAndRefresh(ref _stopCts);
                    TaskHelper.Link(ref cancellationToken, ref _stopCts);
                }

                Stopping = true;

                await Soma.FadeTask(
                    _audioSource,
                    fadeOutDuration,
                    0,
                    fadeOutEase,
                    cancellationToken: cancellationToken);

                _audioSource.Stop();
                Stopping = false;

                ResetProperties();
            }
        }

        internal void Fade(float targetVolume, float duration, Ease ease = Ease.Linear, Action onComplete = null)
        {
            if (Fading)
            {
                Fading = false;
#if UNITASK_INCLUDED
                TaskHelper.Cancel(ref _fadeCts);
#else
                if (_fadeRoutine != null) _manager.StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
#endif
            }

#if UNITASK_INCLUDED
            FadeTask(TaskHelper.CancelAndRefresh(ref _fadeCts)).Forget();

            return;

            async UniTaskVoid FadeTask(CancellationToken cancellationToken)
            {
                Fading = true;

                await Soma.FadeTask(
                    _audioSource,
                    duration,
                    targetVolume,
                    ease,
                    PausedPredicate,
                    cancellationToken);

                onComplete?.Invoke();

                Fading = false;
            }

#else
            _fadeRoutine = _manager.StartCoroutine(FadeRoutine());

            return;

            IEnumerator FadeRoutine()
            {
                Fading = true;

                yield return Soma.FadeRoutine(_audioSource, duration, targetVolume, ease, _waitWhilePaused);

                onComplete?.Invoke();

                Fading = false;
                _fadeRoutine = null;
            }
#endif
        }

        internal async DynamicTask FadeAsync(float targetVolume,
                                             float duration,
                                             Ease ease = Ease.Linear,
                                             CancellationToken cancellationToken = default)
        {
            if (cancellationToken == default)
            {
                cancellationToken = TaskHelper.CancelAndRefresh(ref _fadeCts);
            }
            else
            {
                TaskHelper.CancelAndRefresh(ref _fadeCts);
                TaskHelper.Link(ref cancellationToken, ref _fadeCts);
            }

            Fading = true;

            await Soma.FadeTask(
                _audioSource,
                duration,
                targetVolume,
                ease,
                PausedPredicate,
                cancellationToken);

            Fading = false;
        }

        internal void SetProperties(SomaProperties properties, Transform followTarget, Vector3 position)
        {
            _properties = properties;
            _properties.ApplyOn(ref _audioSource);

            _followTarget = followTarget;
            _hasFollowTarget = _followTarget != null;

            if (_hasFollowTarget)
            {
                _followTargetOffset = position;
            }
            else
            {
                _transform.position = position;
            }
        }

        private void ResetProperties()
        {
            Paused = false;

            if (_hasFollowTarget)
            {
                _hasFollowTarget = false;
                _followTarget = null;
                _followTargetOffset = default;
            }

            _transform.position = default;

            SomaProperties.ResetOn(ref _audioSource);

            _externalAudioSource = null;
            FromExternalAudioSource = false;
        }
    }
}