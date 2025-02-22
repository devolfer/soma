using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Pool;

#if UNITASK_INCLUDED
using Cysharp.Threading.Tasks;
using DynamicTask = Cysharp.Threading.Tasks.UniTask;
#endif

#if !UNITASK_INCLUDED
using System.Collections;
using System.Threading.Tasks;
using DynamicTask = System.Threading.Tasks.Task;
#endif

namespace Devolfer.Soma
{
    /// <summary>
    /// Manages sound playback and volume mixing.
    /// </summary>
    public class Soma : PersistentSingleton<Soma>
    {
        [Space]
        [Tooltip(
            "The number of entities pre-allocated in the pool." +
            "\n\nIdeally set this to the expected number of maximum simultaneously playing sounds.")]
        [SerializeField] private int _entityPoolSize = 32;

        [Space]
        [Tooltip(
            "Add any Audio Mixer Group you wish here, that Soma can change the respective volume of." +
            "\n\nIf none are provided, the default Audio Mixer and groups bundled with the package will be used.")]
        [SerializeField] private SomaVolumeMixerGroup[] _mixerVolumeGroupsDefault;

        private static Soma s_instance;
        private static bool s_setupSoma;

        private ObjectPool<SomaEntity> _entityPool;

        private Dictionary<SomaEntity, AudioSource> _entitiesPlaying;
        private Dictionary<AudioSource, SomaEntity> _sourcesPlaying;
        private Dictionary<SomaEntity, AudioSource> _entitiesPaused;
        private Dictionary<AudioSource, SomaEntity> _sourcesPaused;
        private Dictionary<SomaEntity, AudioSource> _entitiesStopping;
        private Dictionary<AudioSource, SomaEntity> _sourcesStopping;

        private Dictionary<string, SomaVolumeMixerGroup> _mixerVolumeGroups;
        private Dictionary<string, CancellationTokenSource> _mixerFadeCancellationTokenSources;
#if !UNITASK_INCLUDED
        private Dictionary<string, Coroutine> _mixerFadeRoutines;
#endif

        private void LateUpdate()
        {
            if (!s_setupSoma) return;

            foreach ((SomaEntity entity, AudioSource _) in _entitiesPlaying) entity.ProcessTargetFollowing();
            foreach ((SomaEntity entity, AudioSource _) in _entitiesStopping) entity.ProcessTargetFollowing();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (!MarkedForDestruction || !s_setupSoma) return;

            foreach ((SomaEntity entity, AudioSource _) in _entitiesPlaying) entity.Cleanup();
            foreach ((SomaEntity entity, AudioSource _) in _entitiesPaused) entity.Cleanup();
            foreach ((SomaEntity entity, AudioSource _) in _entitiesStopping) entity.Cleanup();

            s_setupSoma = false;
        }

        #region Setup

        protected override void LazySetup()
        {
            base.LazySetup();

            if (!TryGetInstance(out s_instance)) return;
            if (s_instance != this) return;
            if (s_setupSoma) return;

            SetupEntities();
            SetupMixers();

            s_setupSoma = true;
        }

        private void SetupEntities()
        {
            _entitiesPlaying = new Dictionary<SomaEntity, AudioSource>();
            _sourcesPlaying = new Dictionary<AudioSource, SomaEntity>();
            _entitiesPaused = new Dictionary<SomaEntity, AudioSource>();
            _sourcesPaused = new Dictionary<AudioSource, SomaEntity>();
            _entitiesStopping = new Dictionary<SomaEntity, AudioSource>();
            _sourcesStopping = new Dictionary<AudioSource, SomaEntity>();

            _entityPool = new ObjectPool<SomaEntity>(
                createFunc: () =>
                {
                    GameObject obj = new($"Soma-Entity-{_entityPool.CountAll}");
                    obj.transform.SetParent(transform);
                    SomaEntity entity = obj.AddComponent<SomaEntity>();
                    entity.Setup(this);

                    obj.SetActive(false);

                    return entity;
                },
                actionOnGet: entity => entity.gameObject.SetActive(true),
                actionOnRelease: entity => entity.gameObject.SetActive(false),
                actionOnDestroy: entity => Destroy(entity.gameObject),
                defaultCapacity: _entityPoolSize);

            _entityPool.PreAllocate(_entityPoolSize);
        }

        private void SetupMixers()
        {
            _mixerVolumeGroups = new Dictionary<string, SomaVolumeMixerGroup>();
            _mixerFadeCancellationTokenSources = new Dictionary<string, CancellationTokenSource>();
#if !UNITASK_INCLUDED
            _mixerFadeRoutines = new Dictionary<string, Coroutine>();
#endif

            if (_mixerVolumeGroupsDefault == null || _mixerVolumeGroupsDefault.Length == 0)
            {
                AudioMixer audioMixerDefault = Resources.Load<AudioMixer>("AudioMixerDefault");

                _mixerVolumeGroupsDefault = new SomaVolumeMixerGroup[3];
                _mixerVolumeGroupsDefault[0] = new SomaVolumeMixerGroup(audioMixerDefault, "VolumeMaster", 10);
                _mixerVolumeGroupsDefault[1] = new SomaVolumeMixerGroup(audioMixerDefault, "VolumeMusic", 10);
                _mixerVolumeGroupsDefault[2] = new SomaVolumeMixerGroup(audioMixerDefault, "VolumeSFX", 10);
            }

            foreach (SomaVolumeMixerGroup group in _mixerVolumeGroupsDefault)
            {
                RegisterMixerVolumeGroup(group);
                group.Refresh();
            }
        }

        #endregion

        #region Entity

        #region Play

        /// <summary>
        /// Plays a sound with the specified <see cref="SomaProperties"/>.
        /// </summary>
        /// <param name="properties">The properties that define the sound.</param>
        /// <param name="followTarget">Optional target the sound will follow while playing.</param>
        /// <param name="position">Either the global position or, when following, the position offset at which the sound is played.</param>
        /// <param name="fadeIn">Optional volume fade in at the start of play.</param>
        /// <param name="fadeInDuration">The duration in seconds the fading in will prolong.</param>
        /// <param name="fadeInEase">The easing applied when fading in.</param>
        /// <param name="onComplete">Optional callback once sound completes playing (not applicable for looping sounds).</param>
        /// <returns>The <see cref="SomaEntity"/> used for playback.</returns>
        public static SomaEntity Play(SomaProperties properties,
                                      Transform followTarget = null,
                                      Vector3 position = default,
                                      bool fadeIn = false,
                                      float fadeInDuration = .5f,
                                      Ease fadeInEase = Ease.Linear,
                                      Action onComplete = null)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return null;

            return s_instance.Play_Internal(
                properties,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                EasingFunctions.GetEasingFunction(fadeInEase),
                onComplete);
        }

        /// <summary>
        /// Plays a sound with the properties of an <see cref="AudioSource"/>.
        /// </summary>
        /// <param name="audioSource">The source of which the sound properties will be derived from.</param>
        /// <param name="followTarget">Optional target the sound will follow while playing.</param>
        /// <param name="position">Either the global position or, when following, the position offset at which the sound is played.</param>
        /// <param name="fadeIn">Optional volume fade in at the start of play.</param>
        /// <param name="fadeInDuration">The duration in seconds the fading in will prolong.</param>
        /// <param name="fadeInEase">The easing applied when fading in.</param>
        /// <param name="onComplete">Optional callback once sound completes playing (not applicable for looping sounds).</param>
        /// <returns>The <see cref="SomaEntity"/> used for playback.</returns>
        /// <remarks>The original <see cref="AudioSource"/> will be disabled.</remarks>
        public static SomaEntity Play(AudioSource audioSource,
                                      Transform followTarget = null,
                                      Vector3 position = default,
                                      bool fadeIn = false,
                                      float fadeInDuration = .5f,
                                      Ease fadeInEase = Ease.Linear,
                                      Action onComplete = null)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return null;

            return s_instance.Play_Internal(
                audioSource,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                EasingFunctions.GetEasingFunction(fadeInEase),
                onComplete);
        }

        /// <summary>
        /// Plays a sound with the specified <see cref="AudioClip"/>.
        /// </summary>
        /// <param name="audioClip">The clip to be played.</param>
        /// <param name="followTarget">Optional target the sound will follow while playing.</param>
        /// <param name="position">Either the global position or, when following, the position offset at which the sound is played.</param>
        /// <param name="fadeIn">Optional volume fade in at the start of play.</param>
        /// <param name="fadeInDuration">The duration in seconds the fading in will prolong.</param>
        /// <param name="fadeInEase">The easing applied when fading in.</param>
        /// <param name="onComplete">Optional callback once sound completes playing (not applicable for looping sounds).</param>
        /// <returns>The <see cref="SomaEntity"/> used for playback.</returns>
        public static SomaEntity Play(AudioClip audioClip,
                                      Transform followTarget = null,
                                      Vector3 position = default,
                                      bool fadeIn = false,
                                      float fadeInDuration = .5f,
                                      Ease fadeInEase = Ease.Linear,
                                      Action onComplete = null)
        {
            if (audioClip == null) return null;

            return Play(
                new SomaProperties(audioClip),
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                onComplete);
        }

        /// <summary>
        /// Asynchronously plays a sound with the specified <see cref="SomaProperties"/>.
        /// </summary>
        /// <param name="entity">The <see cref="SomaEntity"/> used for playback.</param>
        /// <param name="properties">The properties that define the sound.</param>
        /// <param name="followTarget">Optional target the sound will follow while playing.</param>
        /// <param name="position">Either the global position or, when following, the position offset at which the sound is played.</param>
        /// <param name="fadeIn">Optional volume fade in at the start of play.</param>
        /// <param name="fadeInDuration">The duration in seconds the fading in will prolong.</param>
        /// <param name="fadeInEase">The easing applied when fading in.</param>
        /// <param name="cancellationToken">Optional token for cancelling the playback.</param>
        /// <returns>The playback task.</returns>
        /// <remarks>Outs the playback <see cref="SomaEntity"/>.</remarks>
        public static DynamicTask PlayAsync(out SomaEntity entity,
                                            SomaProperties properties,
                                            Transform followTarget = null,
                                            Vector3 position = default,
                                            bool fadeIn = false,
                                            float fadeInDuration = .5f,
                                            Ease fadeInEase = Ease.Linear,
                                            CancellationToken cancellationToken = default)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma)
            {
                entity = null;
                return default;
            }

            return s_instance.PlayAsync_Internal(
                out entity,
                properties,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                EasingFunctions.GetEasingFunction(fadeInEase),
                cancellationToken);
        }

        /// <summary>
        /// Asynchronously plays a sound with the properties of an <see cref="AudioSource"/>.
        /// </summary>
        /// <param name="entity">The <see cref="SomaEntity"/> used for playback.</param>
        /// <param name="audioSource">The source of which the sound properties will be derived from.</param>
        /// <param name="followTarget">Optional target the sound will follow while playing.</param>
        /// <param name="position">Either the global position or, when following, the position offset at which the sound is played.</param>
        /// <param name="fadeIn">Optional volume fade in at the start of play.</param>
        /// <param name="fadeInDuration">The duration in seconds the fading in will prolong.</param>
        /// <param name="fadeInEase">The easing applied when fading in.</param>
        /// <param name="cancellationToken">Optional token for cancelling the playback.</param>
        /// <returns>The playback task.</returns>
        /// <remarks>Outs the playback <see cref="SomaEntity"/>.</remarks>
        public static DynamicTask PlayAsync(out SomaEntity entity,
                                            AudioSource audioSource,
                                            Transform followTarget = null,
                                            Vector3 position = default,
                                            bool fadeIn = false,
                                            float fadeInDuration = .5f,
                                            Ease fadeInEase = Ease.Linear,
                                            CancellationToken cancellationToken = default)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma)
            {
                entity = null;
                return default;
            }

            return s_instance.PlayAsync_Internal(
                out entity,
                audioSource,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                EasingFunctions.GetEasingFunction(fadeInEase),
                cancellationToken);
        }

        /// <summary>
        /// Asynchronously plays a sound with the specified <see cref="AudioClip"/>.
        /// </summary>
        /// <param name="entity">The <see cref="SomaEntity"/> used for playback.</param>
        /// <param name="audioClip">The clip to be played.</param>
        /// <param name="followTarget">Optional target the sound will follow while playing.</param>
        /// <param name="position">Either the global position or, when following, the position offset at which the sound is played.</param>
        /// <param name="fadeIn">Optional volume fade in at the start of play.</param>
        /// <param name="fadeInDuration">The duration in seconds the fading in will prolong.</param>
        /// <param name="fadeInEase">The easing applied when fading in.</param>
        /// <param name="cancellationToken">Optional token for cancelling the playback.</param>
        /// <returns>The playback task.</returns>
        /// <remarks>Outs the playback <see cref="SomaEntity"/>.</remarks>
        public static DynamicTask PlayAsync(out SomaEntity entity,
                                            AudioClip audioClip,
                                            Transform followTarget = null,
                                            Vector3 position = default,
                                            bool fadeIn = false,
                                            float fadeInDuration = .5f,
                                            Ease fadeInEase = Ease.Linear,
                                            CancellationToken cancellationToken = default)
        {
            if (audioClip == null)
            {
                entity = null;
                return default;
            }

            return PlayAsync(
                out entity,
                new SomaProperties(audioClip),
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                cancellationToken);
        }

        /// <summary>
        /// Asynchronously plays a sound with the specified <see cref="SomaProperties"/>.
        /// </summary>
        /// <param name="properties">The properties that define the sound.</param>
        /// <param name="followTarget">Optional target the sound will follow while playing.</param>
        /// <param name="position">Either the global position or, when following, the position offset at which the sound is played.</param>
        /// <param name="fadeIn">Optional volume fade in at the start of play.</param>
        /// <param name="fadeInDuration">The duration in seconds the fading in will prolong.</param>
        /// <param name="fadeInEase">The easing applied when fading in.</param>
        /// <param name="cancellationToken">Optional token for cancelling the playback.</param>
        /// <returns>The playback task.</returns>
        public static DynamicTask PlayAsync(SomaProperties properties,
                                            Transform followTarget = null,
                                            Vector3 position = default,
                                            bool fadeIn = false,
                                            float fadeInDuration = .5f,
                                            Ease fadeInEase = Ease.Linear,
                                            CancellationToken cancellationToken = default)
        {
            return PlayAsync(
                out _,
                properties,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                cancellationToken);
        }

        /// <summary>
        /// Asynchronously plays a sound with the properties of an <see cref="AudioSource"/>.
        /// </summary>
        /// <param name="audioSource">The source of which the sound properties will be derived from.</param>
        /// <param name="followTarget">Optional target the sound will follow while playing.</param>
        /// <param name="position">Either the global position or, when following, the position offset at which the sound is played.</param>
        /// <param name="fadeIn">Optional volume fade in at the start of play.</param>
        /// <param name="fadeInDuration">The duration in seconds the fading in will prolong.</param>
        /// <param name="fadeInEase">The easing applied when fading in.</param>
        /// <param name="cancellationToken">Optional token for cancelling the playback.</param>
        /// <returns>The playback task.</returns>
        public static DynamicTask PlayAsync(AudioSource audioSource,
                                            Transform followTarget = null,
                                            Vector3 position = default,
                                            bool fadeIn = false,
                                            float fadeInDuration = .5f,
                                            Ease fadeInEase = Ease.Linear,
                                            CancellationToken cancellationToken = default)
        {
            return PlayAsync(
                out _,
                audioSource,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                cancellationToken);
        }

        /// <summary>
        /// Asynchronously plays a sound with the specified <see cref="AudioClip"/>.
        /// </summary>
        /// <param name="audioClip">The clip to be played.</param>
        /// <param name="followTarget">Optional target the sound will follow while playing.</param>
        /// <param name="position">Either the global position or, when following, the position offset at which the sound is played.</param>
        /// <param name="fadeIn">Optional volume fade in at the start of play.</param>
        /// <param name="fadeInDuration">The duration in seconds the fading in will prolong.</param>
        /// <param name="fadeInEase">The easing applied when fading in.</param>
        /// <param name="cancellationToken">Optional token for cancelling the playback.</param>
        /// <returns>The playback task.</returns>
        public static DynamicTask PlayAsync(AudioClip audioClip,
                                            Transform followTarget = null,
                                            Vector3 position = default,
                                            bool fadeIn = false,
                                            float fadeInDuration = .5f,
                                            Ease fadeInEase = Ease.Linear,
                                            CancellationToken cancellationToken = default)
        {
            if (audioClip == null) return default;

            return PlayAsync(
                new SomaProperties(audioClip),
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                cancellationToken);
        }

        private SomaEntity Play_Internal(SomaProperties properties,
                                         Transform followTarget,
                                         Vector3 position,
                                         bool fadeIn,
                                         float fadeInDuration,
                                         Func<float, float> fadeInEaseFunction,
                                         Action onComplete)
        {
            SomaEntity entity = _entityPool.Get();

            entity.Play(properties, followTarget, position, fadeIn, fadeInDuration, fadeInEaseFunction, onComplete);
            AddPlaying(entity);

            return entity;
        }

        private SomaEntity Play_Internal(AudioSource audioSource,
                                         Transform followTarget,
                                         Vector3 position,
                                         bool fadeIn,
                                         float fadeInDuration,
                                         Func<float, float> fadeInEaseFunction,
                                         Action onComplete)
        {
            if (audioSource == null || audioSource.clip == null) return null;

            if (HasPlaying(audioSource, out SomaEntity playingEntity)) return playingEntity;

            if (HasPaused(audioSource, out SomaEntity pausedEntity))
            {
                Resume(pausedEntity);
                return pausedEntity;
            }

            SomaEntity entity = _entityPool.Get();

            entity.Play(audioSource, followTarget, position, fadeIn, fadeInDuration, fadeInEaseFunction, onComplete);
            AddPlaying(entity);

            return entity;
        }

        private DynamicTask PlayAsync_Internal(out SomaEntity entity,
                                               SomaProperties properties,
                                               Transform followTarget,
                                               Vector3 position,
                                               bool fadeIn,
                                               float fadeInDuration,
                                               Func<float, float> fadeInEaseFunction,
                                               CancellationToken cancellationToken)
        {
            entity = _entityPool.Get();

            DynamicTask task = entity.PlayAsync(
                properties,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEaseFunction,
                cancellationToken);

            AddPlaying(entity);

            return task;
        }

        private DynamicTask PlayAsync_Internal(out SomaEntity entity,
                                               AudioSource audioSource,
                                               Transform followTarget,
                                               Vector3 position,
                                               bool fadeIn,
                                               float fadeInDuration,
                                               Func<float, float> fadeInEaseFunction,
                                               CancellationToken cancellationToken)
        {
            if (audioSource == null || audioSource.clip == null)
            {
                entity = null;
                return default;
            }

            entity = _entityPool.Get();

            DynamicTask task = entity.PlayAsync(
                audioSource,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEaseFunction,
                cancellationToken);

            AddPlaying(entity);

            return task;
        }

        #endregion

        #region Pause

        /// <summary>
        /// Pauses a playing sound that is managed by Soma.
        /// </summary>
        /// <param name="entity">The entity that is currently playing.</param>
        /// <returns>True, if pausing was successful.</returns>
        /// <remarks>Has no effect if the entity is currently stopping.</remarks>
        public static bool Pause(SomaEntity entity)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return false;

            return s_instance.Pause_Internal(entity);
        }

        /// <summary>
        /// Pauses a playing sound that is managed by Soma.
        /// </summary>
        /// <param name="audioSource">The source of the sound.</param>
        /// <returns>True, if pausing was successful.</returns>
        /// <remarks>Has no effect if the source is currently stopping.</remarks>
        public static bool Pause(AudioSource audioSource)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return false;

            return s_instance.Pause_Internal(audioSource);
        }

        /// <summary>
        /// Pauses all currently playing sounds that are managed by Soma.
        /// </summary>
        /// <remarks>Has no effect on sounds currently stopping.</remarks>
        public static bool PauseAll()
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return false;

            return s_instance.PauseAll_Internal();
        }

        private bool Pause_Internal(SomaEntity entity)
        {
            if (entity == null) return false;

            if (!HasPlaying(entity)) return false;
            if (HasStopping(entity)) return false;

            RemovePlaying(entity);

            entity.Pause();

            AddPaused(entity);

            return true;
        }

        private bool Pause_Internal(AudioSource audioSource)
        {
            if (audioSource == null) return false;

            if (HasStopping(audioSource)) return false;
            if (!HasPlaying(audioSource, out SomaEntity entity)) return false;

            RemovePlaying(entity);

            entity.Pause();

            AddPaused(entity);

            return true;
        }

        private bool PauseAll_Internal()
        {
            bool successful = false;

            foreach ((SomaEntity entity, AudioSource _) in _entitiesPlaying)
            {
                if (HasStopping(entity)) continue;

                entity.Pause();
                AddPaused(entity);
                successful = true;
            }

            ClearPlayingEntities();

            return successful;
        }

        #endregion

        #region Resume

        /// <summary>
        /// Resumes a paused sound that is managed by Soma.
        /// </summary>
        /// <param name="entity">The entity that is currently paused.</param>
        /// <returns>True, if resuming was successful.</returns>
        /// <remarks>Has no effect if the entity is currently stopping.</remarks>
        public static bool Resume(SomaEntity entity)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return false;

            return s_instance.Resume_Internal(entity);
        }

        /// <summary>
        /// Resumes a paused sound that is managed by Soma.
        /// </summary>
        /// <param name="audioSource">The source of the sound.</param>
        /// <returns>True, if resuming was successful.</returns>
        /// <remarks>Has no effect if the source is currently stopping.</remarks>
        public static bool Resume(AudioSource audioSource)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return false;

            return s_instance.Resume_Internal(audioSource);
        }

        /// <summary>
        /// Resumes all currently paused sounds that are managed by Soma.
        /// </summary>
        public static bool ResumeAll()
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return false;

            return s_instance.ResumeAll_Internal();
        }

        private bool Resume_Internal(SomaEntity entity)
        {
            if (entity == null) return false;

            if (!HasPaused(entity)) return false;
            if (HasStopping(entity)) return false;

            RemovePaused(entity);

            entity.Resume();

            AddPlaying(entity);

            return true;
        }

        private bool Resume_Internal(AudioSource audioSource)
        {
            if (audioSource == null) return false;

            if (HasStopping(audioSource)) return false;
            if (!HasPaused(audioSource, out SomaEntity entity)) return false;

            RemovePaused(entity);

            entity.Resume();

            AddPlaying(entity);

            return true;
        }

        private bool ResumeAll_Internal()
        {
            bool successful = false;

            foreach ((SomaEntity entity, AudioSource _) in _entitiesPaused)
            {
                if (HasStopping(entity)) continue;

                entity.Resume();
                AddPlaying(entity);
                successful = true;
            }

            ClearPausedEntities();

            return successful;
        }

        #endregion

        #region Stop

        /// <summary>
        /// Stops playback of a playing/paused sound that is managed by Soma.
        /// </summary>
        /// <param name="entity">The entity that is either currently playing or paused.</param>
        /// <param name="fadeOut">True by default. Set this to false, if the volume should not fade out when stopping.</param>
        /// <param name="fadeOutDuration">The duration in seconds the fading out will prolong.</param>
        /// <param name="fadeOutEase">The easing applied when fading out.</param>
        /// <param name="onComplete">Optional callback once sound completes stopping.</param>
        /// <remarks>Paused sounds will be stopped without fade out regardless.</remarks>
        public static void Stop(SomaEntity entity,
                                bool fadeOut = true,
                                float fadeOutDuration = .5f,
                                Ease fadeOutEase = Ease.Linear,
                                Action onComplete = null)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return;

            s_instance.Stop_Internal(
                entity,
                fadeOut,
                fadeOutDuration,
                EasingFunctions.GetEasingFunction(fadeOutEase),
                onComplete);
        }

        /// <summary>
        /// Stops playback of a playing/paused sound that is managed by Soma.
        /// </summary>
        /// <param name="audioSource">The source that is either currently playing or paused.</param>
        /// <param name="fadeOut">True by default. Set this to false, if the volume should not fade out when stopping.</param>
        /// <param name="fadeOutDuration">The duration in seconds the fading out will prolong.</param>
        /// <param name="fadeOutEase">The easing applied when fading out.</param>
        /// <param name="onComplete">Optional callback once sound completes stopping.</param>
        /// <remarks>Paused sounds will be stopped without fade out regardless.</remarks>
        public static void Stop(AudioSource audioSource,
                                bool fadeOut = true,
                                float fadeOutDuration = .5f,
                                Ease fadeOutEase = Ease.Linear,
                                Action onComplete = null)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return;

            s_instance.Stop_Internal(
                audioSource,
                fadeOut,
                fadeOutDuration,
                EasingFunctions.GetEasingFunction(fadeOutEase),
                onComplete);
        }

        /// <summary>
        /// Asynchronously stops playback of a playing/paused sound that is managed by Soma.
        /// </summary>
        /// <param name="entity">The entity that is either currently playing or paused.</param>
        /// <param name="fadeOut">True by default. Set this to false, if the volume should not fade out when stopping.</param>
        /// <param name="fadeOutDuration">The duration in seconds the fading out will prolong.</param>
        /// <param name="fadeOutEase">The easing applied when fading out.</param>
        /// <param name="cancellationToken">Optional token for cancelling the stopping.</param>
        /// <returns>The stopping task.</returns>
        /// <remarks>Paused sounds will be stopped without fade out regardless.</remarks>
        public static DynamicTask StopAsync(SomaEntity entity,
                                            bool fadeOut = true,
                                            float fadeOutDuration = .5f,
                                            Ease fadeOutEase = Ease.Linear,
                                            CancellationToken cancellationToken = default)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return default;

            return s_instance.StopAsync_Internal(
                entity,
                fadeOut,
                fadeOutDuration,
                EasingFunctions.GetEasingFunction(fadeOutEase),
                cancellationToken);
        }

        /// <summary>
        /// Asynchronously stops playback of a playing/paused sound that is managed by Soma.
        /// </summary>
        /// <param name="audioSource">The source that is either currently playing or paused.</param>
        /// <param name="fadeOut">True by default. Set this to false, if the volume should not fade out when stopping.</param>
        /// <param name="fadeOutDuration">The duration in seconds the fading out will prolong.</param>
        /// <param name="fadeOutEase">The easing applied when fading out.</param>
        /// <param name="cancellationToken">Optional token for cancelling the stopping.</param>
        /// <returns>The stopping task.</returns>
        /// <remarks>Paused sounds will be stopped without fade out regardless.</remarks>
        public static DynamicTask StopAsync(AudioSource audioSource,
                                            bool fadeOut = true,
                                            float fadeOutDuration = .5f,
                                            Ease fadeOutEase = Ease.Linear,
                                            CancellationToken cancellationToken = default)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return default;

            return s_instance.StopAsync_Internal(
                audioSource,
                fadeOut,
                fadeOutDuration,
                EasingFunctions.GetEasingFunction(fadeOutEase),
                cancellationToken);
        }

        /// <summary>
        /// Stops all currently playing/paused sounds that are managed by Soma.
        /// </summary>
        /// <param name="fadeOut">True by default. Set this to false, if the volumes should not fade out when stopping.</param>
        /// <param name="fadeOutDuration">The duration in seconds the fading out will prolong.</param>
        /// <param name="fadeOutEase">The easing applied when fading out.</param>
        public static void StopAll(bool fadeOut = true,
                                   float fadeOutDuration = 1,
                                   Ease fadeOutEase = Ease.Linear)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return;

            s_instance.StopAll_Internal(fadeOut, fadeOutDuration, EasingFunctions.GetEasingFunction(fadeOutEase));
        }

        /// <summary>
        /// Asynchronously stops all currently playing/paused sounds that are managed by Soma.
        /// </summary>
        /// <param name="fadeOut">True by default. Set this to false, if the volumes should not fade out when stopping.</param>
        /// <param name="fadeOutDuration">The duration in seconds the fading out will prolong.</param>
        /// <param name="fadeOutEase">The easing applied when fading out.</param>
        /// <param name="cancellationToken">Optional token for cancelling the stopping.</param>
        /// <returns>The stopping task.</returns>
        public static DynamicTask StopAllAsync(bool fadeOut = true,
                                               float fadeOutDuration = 1,
                                               Ease fadeOutEase = Ease.Linear,
                                               CancellationToken cancellationToken = default)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return default;

            return s_instance.StopAllAsync_Internal(
                fadeOut,
                fadeOutDuration,
                EasingFunctions.GetEasingFunction(fadeOutEase),
                cancellationToken);
        }

        internal void Stop_Internal(SomaEntity entity,
                                    bool fadeOut,
                                    float fadeOutDuration,
                                    Func<float, float> fadeOutEaseFunction,
                                    Action onComplete)
        {
            if (entity == null) return;

            bool playingEntity = HasPlaying(entity);
            bool pausedEntity = HasPaused(entity);

            if (!playingEntity && !pausedEntity) return;

            if (playingEntity) RemovePlaying(entity);
            if (pausedEntity) RemovePaused(entity);

            AddStopping(entity);
            entity.Stop(fadeOut, fadeOutDuration, fadeOutEaseFunction, OnStopComplete);

            return;

            void OnStopComplete()
            {
                RemoveStopping(entity);

                _entityPool.Release(entity);

                onComplete?.Invoke();
            }
        }

        private void Stop_Internal(AudioSource audioSource,
                                   bool fadeOut,
                                   float fadeOutDuration,
                                   Func<float, float> fadeOutEaseFunction,
                                   Action onComplete)
        {
            if (audioSource == null) return;

            bool playingAudioSource = HasPlaying(audioSource, out SomaEntity entityPlaying);
            bool pausedAudioSource = HasPaused(audioSource, out SomaEntity entityPaused);

            if (!playingAudioSource && !pausedAudioSource) return;

            if (playingAudioSource) RemovePlaying(entityPlaying);
            if (pausedAudioSource) RemovePaused(entityPaused);

            SomaEntity entity = entityPlaying != null ? entityPlaying : entityPaused;
            AddStopping(entity);
            entity.Stop(fadeOut, fadeOutDuration, fadeOutEaseFunction, OnStopComplete);

            return;

            void OnStopComplete()
            {
                RemoveStopping(entity);

                _entityPool.Release(entity);

                onComplete?.Invoke();
            }
        }

        internal async DynamicTask StopAsync_Internal(SomaEntity entity,
                                                      bool fadeOut,
                                                      float fadeOutDuration,
                                                      Func<float, float> fadeOutEaseFunction,
                                                      CancellationToken cancellationToken)
        {
            if (entity == null) return;

            bool playingEntity = HasPlaying(entity);
            bool pausedEntity = HasPaused(entity);

            if (!playingEntity && !pausedEntity) return;

            if (playingEntity) RemovePlaying(entity);
            if (pausedEntity) RemovePaused(entity);

            AddStopping(entity);

            await entity.StopAsync(fadeOut, fadeOutDuration, fadeOutEaseFunction, cancellationToken);

            RemoveStopping(entity);

            _entityPool.Release(entity);
        }

        private async DynamicTask StopAsync_Internal(AudioSource audioSource,
                                                     bool fadeOut,
                                                     float fadeOutDuration,
                                                     Func<float, float> fadeOutEaseFunction,
                                                     CancellationToken cancellationToken)
        {
            if (audioSource == null) return;

            bool playingAudioSource = HasPlaying(audioSource, out SomaEntity entityPlaying);
            bool pausedAudioSource = HasPaused(audioSource, out SomaEntity entityPaused);

            if (!playingAudioSource && !pausedAudioSource) return;

            if (playingAudioSource) RemovePlaying(entityPlaying);
            if (pausedAudioSource) RemovePaused(entityPaused);

            SomaEntity entity = entityPlaying != null ? entityPlaying : entityPaused;
            AddStopping(entity);

            await entity.StopAsync(fadeOut, fadeOutDuration, fadeOutEaseFunction, cancellationToken);

            RemoveStopping(entity);

            _entityPool.Release(entity);
        }

        private void StopAll_Internal(bool fadeOut,
                                      float fadeOutDuration,
                                      Func<float, float> fadeOutEaseFunction)
        {
            foreach (SomaEntity entity in _entitiesPlaying.Keys.ToList())
            {
                Stop_Internal(entity, fadeOut, fadeOutDuration, fadeOutEaseFunction, null);
            }

            foreach (SomaEntity entity in _entitiesPaused.Keys.ToList())
            {
                entity.Stop(false);
                _entityPool.Release(entity);
            }

            ClearPausedEntities();
        }

        private async DynamicTask StopAllAsync_Internal(bool fadeOut,
                                                        float fadeOutDuration,
                                                        Func<float, float> fadeOutEaseFunction,
                                                        CancellationToken cancellationToken)
        {
            List<DynamicTask> stopTasks = new();

            foreach (SomaEntity entity in _entitiesPlaying.Keys.ToList())
            {
                stopTasks.Add(
                    StopAsync_Internal(entity, fadeOut, fadeOutDuration, fadeOutEaseFunction, cancellationToken));
            }

            foreach (SomaEntity entity in _entitiesPaused.Keys.ToList())
            {
                stopTasks.Add(entity.StopAsync(false, cancellationToken: cancellationToken));
                _entityPool.Release(entity);
            }

            ClearPausedEntities();

            await DynamicTask.WhenAll(stopTasks);
        }

        #endregion

        #region Set

        /// <summary>
        /// Sets the properties of a playing/paused sound.
        /// </summary>
        /// <param name="entity">The entity whose properties will be changed.</param>
        /// <param name="properties">The new properties.</param>
        /// <param name="followTarget">The target the sound will follow while playing (none if null).</param>
        /// <param name="position">Either the global position or, when following, the position offset of the sound.</param>
        /// <remarks>Will change ALL properties, the followTarget and the position.
        /// Be sure to retrieve the original properties (e.g. via copy constructor), if you only want to change certain properties.</remarks>
        public static void Set(SomaEntity entity,
                               SomaProperties properties,
                               Transform followTarget,
                               Vector3 position)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return;

            s_instance.Set_Internal(entity, properties, followTarget, position);
        }

        /// <summary>
        /// Sets the properties of a playing/paused AudioSource that is managed by Soma.
        /// </summary>
        /// <param name="audioSource">The source used to initiate playback.</param>
        /// <param name="properties">The new properties.</param>
        /// <param name="followTarget">The target the sound will follow while playing (none if null).</param>
        /// <param name="position">Either the global position or, when following, the position offset of the sound.</param>
        /// <remarks>Will change ALL properties, the followTarget and the position.
        /// Be sure to retrieve the original properties (e.g. via copy constructor), if you only want to change certain properties.</remarks>
        public static void Set(AudioSource audioSource,
                               SomaProperties properties,
                               Transform followTarget,
                               Vector3 position)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return;

            s_instance.Set_Internal(audioSource, properties, followTarget, position);
        }

        private void Set_Internal(SomaEntity entity,
                                  SomaProperties properties,
                                  Transform followTarget,
                                  Vector3 position)
        {
            if (entity == null) return;
            if (properties == null) return;

            entity.SetProperties(properties, followTarget, position);
        }

        private void Set_Internal(AudioSource audioSource,
                                  SomaProperties properties,
                                  Transform followTarget,
                                  Vector3 position)
        {
            if (audioSource == null) return;
            if (properties == null) return;

            if (HasPaused(audioSource, out SomaEntity entityPaused))
            {
                entityPaused.SetProperties(properties, followTarget, position);
                return;
            }

            if (HasPlaying(audioSource, out SomaEntity entityPlaying))
            {
                entityPlaying.SetProperties(properties, followTarget, position);
            }
        }

        #endregion

        #region Fade

        /// <summary>
        /// Fades the volume of a playing sound that is managed by Soma.
        /// </summary>
        /// <param name="entity">The entity that is currently playing or paused.</param>
        /// <param name="targetVolume">The target volume reached at the end of the fade.</param>
        /// <param name="duration">The duration in seconds the fade will prolong.</param>
        /// <param name="ease">The easing applied when fading.</param>
        /// <param name="onComplete">Optional callback once sound completes fading.</param>
        /// <remarks>Has no effect on sounds currently stopping.</remarks>
        public static void Fade(SomaEntity entity,
                                float targetVolume,
                                float duration,
                                Ease ease = Ease.Linear,
                                Action onComplete = null)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return;

            s_instance.Fade_Internal(
                entity,
                targetVolume,
                duration,
                EasingFunctions.GetEasingFunction(ease),
                onComplete);
        }

        /// <summary>
        /// Fades the volume of a playing sound that is managed by Soma.
        /// </summary>
        /// <param name="audioSource">The source that is currently playing or paused.</param>
        /// <param name="targetVolume">The target volume reached at the end of the fade.</param>
        /// <param name="duration">The duration in seconds the fade will prolong.</param>
        /// <param name="ease">The easing applied when fading.</param>
        /// <param name="onComplete">Optional callback once sound completes fading.</param>
        /// <remarks>Has no effect on sounds currently stopping.</remarks>
        public static void Fade(AudioSource audioSource,
                                float targetVolume,
                                float duration,
                                Ease ease = Ease.Linear,
                                Action onComplete = null)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return;

            s_instance.Fade_Internal(
                audioSource,
                targetVolume,
                duration,
                EasingFunctions.GetEasingFunction(ease),
                onComplete);
        }

        /// <summary>
        /// Asynchronously fades the volume of a playing sound that is managed by Soma.
        /// </summary>
        /// <param name="entity">The entity that is currently playing or paused.</param>
        /// <param name="targetVolume">The target volume reached at the end of the fade.</param>
        /// <param name="duration">The duration in seconds the fade will prolong.</param>
        /// <param name="ease">The easing applied when fading.</param>
        /// <param name="cancellationToken">Optional token for cancelling the fading.</param>
        /// <returns>The fading task.</returns>
        /// <remarks>Has no effect on sounds currently stopping.</remarks>
        public static DynamicTask FadeAsync(SomaEntity entity,
                                            float targetVolume,
                                            float duration,
                                            Ease ease = Ease.Linear,
                                            CancellationToken cancellationToken = default)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return default;

            return s_instance.FadeAsync_Internal(
                entity,
                targetVolume,
                duration,
                EasingFunctions.GetEasingFunction(ease),
                cancellationToken);
        }

        /// <summary>
        /// Asynchronously fades the volume of a playing sound that is managed by Soma.
        /// </summary>
        /// <param name="audioSource">The source that is currently playing or paused.</param>
        /// <param name="targetVolume">The target volume reached at the end of the fade.</param>
        /// <param name="duration">The duration in seconds the fade will prolong.</param>
        /// <param name="ease">The easing applied when fading.</param>
        /// <param name="cancellationToken">Optional token for cancelling the fading.</param>
        /// <returns>The fading task.</returns>
        /// <remarks>Has no effect on sounds currently stopping.</remarks>
        public DynamicTask FadeAsync(AudioSource audioSource,
                                     float targetVolume,
                                     float duration,
                                     Ease ease = Ease.Linear,
                                     CancellationToken cancellationToken = default)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return default;

            return s_instance.FadeAsync_Internal(
                audioSource,
                targetVolume,
                duration,
                EasingFunctions.GetEasingFunction(ease),
                cancellationToken);
        }

        private void Fade_Internal(SomaEntity entity,
                                   float targetVolume,
                                   float duration,
                                   Func<float, float> easeFunction,
                                   Action onComplete)
        {
            if (entity == null) return;

            if (!HasPlaying(entity)) return;
            if (HasStopping(entity)) return;

            if (HasPaused(entity)) Resume(entity);

            entity.Fade(targetVolume, duration, easeFunction, onComplete);
        }

        private void Fade_Internal(AudioSource audioSource,
                                   float targetVolume,
                                   float duration,
                                   Func<float, float> easeFunction,
                                   Action onComplete)
        {
            if (audioSource == null) return;

            if (!HasPlaying(audioSource, out SomaEntity entityPlaying)) return;
            if (HasStopping(audioSource)) return;

            if (HasPaused(audioSource, out SomaEntity entityPaused)) Resume(entityPaused);

            entityPlaying.Fade(targetVolume, duration, easeFunction, onComplete);
        }

        private DynamicTask FadeAsync_Internal(SomaEntity entity,
                                               float targetVolume,
                                               float duration,
                                               Func<float, float> easeFunction,
                                               CancellationToken cancellationToken)
        {
            if (entity == null) return default;

            if (HasStopping(entity)) return default;

            if (HasPaused(entity)) Resume(entity);

            return entity.FadeAsync(targetVolume, duration, easeFunction, cancellationToken);
        }

        private DynamicTask FadeAsync_Internal(AudioSource audioSource,
                                               float targetVolume,
                                               float duration,
                                               Func<float, float> easeFunction,
                                               CancellationToken cancellationToken)
        {
            if (audioSource == null) return default;

            if (!HasPlaying(audioSource, out SomaEntity entityPlaying)) return default;
            if (HasStopping(audioSource)) return default;

            if (HasPaused(audioSource, out SomaEntity entityPaused)) Resume(entityPaused);

            return entityPlaying.FadeAsync(targetVolume, duration, easeFunction, cancellationToken);
        }

#if !UNITASK_INCLUDED
        internal static IEnumerator FadeRoutine(AudioSource audioSource,
                                                float duration,
                                                float targetVolume,
                                                Func<float, float> easeFunction,
                                                WaitWhile waitWhilePredicate = null)
        {
            targetVolume = Mathf.Clamp01(targetVolume);

            if (duration <= 0)
            {
                audioSource.volume = targetVolume;
                yield break;
            }

            float deltaTime = 0;
            float startVolume = audioSource.volume;

            while (deltaTime < duration)
            {
                deltaTime += Time.deltaTime;
                audioSource.volume = Mathf.Lerp(startVolume, targetVolume, easeFunction(deltaTime / duration));

                yield return waitWhilePredicate;
            }

            audioSource.volume = targetVolume;
        }

#endif

        internal static async DynamicTask FadeTask(AudioSource audioSource,
                                                   float duration,
                                                   float targetVolume,
                                                   Func<float, float> easeFunction,
                                                   Func<bool> waitWhilePredicate = default,
                                                   CancellationToken cancellationToken = default)
        {
            targetVolume = Mathf.Clamp01(targetVolume);

            if (duration <= 0)
            {
                audioSource.volume = targetVolume;
                return;
            }

            float deltaTime = 0;
            float startVolume = audioSource.volume;

            while (deltaTime < duration)
            {
                if (cancellationToken.IsCancellationRequested) return;
                
                if (waitWhilePredicate != null)
                {
                    while (waitWhilePredicate())
                    {
                        if (cancellationToken.IsCancellationRequested) return;
                        
                        await DynamicTask.Yield();
                    }
                }
                
                deltaTime += Time.deltaTime;
                audioSource.volume = Mathf.Lerp(startVolume, targetVolume, easeFunction(deltaTime / duration));
                
                await DynamicTask.Yield();
            }

            audioSource.volume = targetVolume;
        }

        #endregion

        #region Cross-Fade

        /// <summary>
        /// Linearly cross-fades a playing sound that is managed by Soma and a new sound.
        /// The fading out sound will be stopped at the end.
        /// </summary>
        /// <param name="duration">The duration in seconds the cross-fade will prolong.</param>
        /// <param name="fadeOutEntity">The entity that will fade out and stop.</param>
        /// <param name="fadeInProperties">The properties that define the newly played sound.</param>
        /// <param name="followTarget">Optional target the new sound will follow while playing.</param>
        /// <param name="fadeInPosition">Either the global position or, when following, the position offset at which the new sound is played.</param>
        /// <param name="onComplete">Optional callback once sounds complete cross-fading.</param>
        /// <returns>The new <see cref="SomaEntity"/> fading in.</returns>
        /// <remarks>Simultaneously call Stop and Play methods for finer cross-fading control instead.</remarks>
        public static SomaEntity CrossFade(float duration,
                                           SomaEntity fadeOutEntity,
                                           SomaProperties fadeInProperties,
                                           Transform followTarget = null,
                                           Vector3 fadeInPosition = default,
                                           Action onComplete = null)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return null;

            return s_instance.CrossFade_Internal(
                duration,
                fadeOutEntity,
                fadeInProperties,
                followTarget,
                fadeInPosition,
                onComplete);
        }

        /// <summary>
        /// Linearly cross-fades a playing sound that is managed by Soma and a new sound.
        /// The fading out sound will be stopped at the end.
        /// </summary>
        /// <param name="duration">The duration in seconds the cross-fade will prolong.</param>
        /// <param name="fadeOutAudioSource">The source that will fade out and stop.</param>
        /// <param name="fadeInAudioSource">The source that defines the newly played sound.</param>
        /// <param name="followTarget">Optional target the new sound will follow while playing.</param>
        /// <param name="fadeInPosition">Either the global position or, when following, the position offset at which the new sound is played.</param>
        /// <param name="onComplete">Optional callback once sounds complete cross-fading.</param>
        /// <returns>The new <see cref="SomaEntity"/> fading in.</returns>
        /// <remarks>Simultaneously call Stop and Play methods for finer cross-fading control instead.</remarks>
        public static SomaEntity CrossFade(float duration,
                                           AudioSource fadeOutAudioSource,
                                           AudioSource fadeInAudioSource,
                                           Transform followTarget = null,
                                           Vector3 fadeInPosition = default,
                                           Action onComplete = null)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return null;

            return s_instance.CrossFade_Internal(
                duration,
                fadeOutAudioSource,
                fadeInAudioSource,
                followTarget,
                fadeInPosition,
                onComplete);
        }

        /// <summary>
        /// Asynchronously and linearly cross-fades a playing sound that is managed by Soma and a new sound. The fading out sound will be stopped at the end.
        /// </summary>
        /// <param name="entity">The <see cref="SomaEntity"/> used for playback of the new sound.</param>
        /// <param name="duration">The duration in seconds the cross-fade will prolong.</param>
        /// <param name="fadeOutEntity">The entity that will fade out and stop.</param>
        /// <param name="fadeInProperties">The properties that define the newly played sound.</param>
        /// <param name="followTarget">Optional target the new sound will follow while playing.</param>
        /// <param name="fadeInPosition">Either the global position or, when following, the position offset at which the new sound is played.</param>
        /// <param name="cancellationToken">Optional token for cancelling the cross-fading.</param>
        /// <returns>The cross-fading task.</returns>
        /// <remarks>Simultaneously call Stop and Play methods for finer cross-fading control instead.</remarks>
        public static DynamicTask CrossFadeAsync(out SomaEntity entity,
                                                 float duration,
                                                 SomaEntity fadeOutEntity,
                                                 SomaProperties fadeInProperties,
                                                 Transform followTarget = null,
                                                 Vector3 fadeInPosition = default,
                                                 CancellationToken cancellationToken = default)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma)
            {
                entity = null;
                return default;
            }

            return s_instance.CrossFadeAsync_Internal(
                out entity,
                duration,
                fadeOutEntity,
                fadeInProperties,
                followTarget,
                fadeInPosition,
                cancellationToken);
        }

        /// <summary>
        /// Asynchronously and linearly cross-fades a playing sound that is managed by Soma and a new sound. The fading out sound will be stopped at the end.
        /// </summary>
        /// <param name="duration">The duration in seconds the cross-fade will prolong.</param>
        /// <param name="fadeOutEntity">The entity that will fade out and stop.</param>
        /// <param name="fadeInProperties">The properties that define the newly played sound.</param>
        /// <param name="followTarget">Optional target the new sound will follow while playing.</param>
        /// <param name="fadeInPosition">Either the global position or, when following, the position offset at which the new sound is played.</param>
        /// <param name="cancellationToken">Optional token for cancelling the cross-fading.</param>
        /// <returns>The cross-fading task.</returns>
        /// <remarks>Simultaneously call Stop and Play methods for finer cross-fading control instead.</remarks>
        public static DynamicTask CrossFadeAsync(float duration,
                                                 SomaEntity fadeOutEntity,
                                                 SomaProperties fadeInProperties,
                                                 Transform followTarget = null,
                                                 Vector3 fadeInPosition = default,
                                                 CancellationToken cancellationToken = default)
        {
            return CrossFadeAsync(
                out _,
                duration,
                fadeOutEntity,
                fadeInProperties,
                followTarget,
                fadeInPosition,
                cancellationToken);
        }

        /// <summary>
        /// Asynchronously and linearly cross-fades a playing sound that is managed by Soma and a new sound. The fading out sound will be stopped at the end.
        /// </summary>
        /// <param name="entity">The <see cref="SomaEntity"/> used for playback of the new sound.</param>
        /// <param name="duration">The duration in seconds the cross-fade will prolong.</param>
        /// <param name="fadeOutAudioSource">The source that will fade out and stop.</param>
        /// <param name="fadeInAudioSource">The source that defines the newly played sound.</param>
        /// <param name="followTarget">Optional target the new sound will follow while playing.</param>
        /// <param name="fadeInPosition">Either the global position or, when following, the position offset at which the new sound is played.</param>
        /// <param name="cancellationToken">Optional token for cancelling the cross-fading.</param>
        /// <returns>The cross-fading task.</returns>
        /// <remarks>Simultaneously call Stop and Play methods for finer cross-fading control instead.</remarks>
        public static DynamicTask CrossFadeAsync(out SomaEntity entity,
                                                 float duration,
                                                 AudioSource fadeOutAudioSource,
                                                 AudioSource fadeInAudioSource,
                                                 Transform followTarget = null,
                                                 Vector3 fadeInPosition = default,
                                                 CancellationToken cancellationToken = default)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma)
            {
                entity = null;
                return default;
            }

            return s_instance.CrossFadeAsync_Internal(
                out entity,
                duration,
                fadeOutAudioSource,
                fadeInAudioSource,
                followTarget,
                fadeInPosition,
                cancellationToken);
        }

        /// <summary>
        /// Asynchronously and linearly cross-fades a playing sound that is managed by Soma and a new sound. The fading out sound will be stopped at the end.
        /// </summary>
        /// <param name="duration">The duration in seconds the cross-fade will prolong.</param>
        /// <param name="fadeOutAudioSource">The source that will fade out and stop.</param>
        /// <param name="fadeInAudioSource">The source that defines the newly played sound.</param>
        /// <param name="followTarget">Optional target the new sound will follow while playing.</param>
        /// <param name="fadeInPosition">Either the global position or, when following, the position offset at which the new sound is played.</param>
        /// <param name="cancellationToken">Optional token for cancelling the cross-fading.</param>
        /// <returns>The cross-fading task.</returns>
        /// <remarks>Simultaneously call Stop and Play methods for finer cross-fading control instead.</remarks>
        public static DynamicTask CrossFadeAsync(float duration,
                                                 AudioSource fadeOutAudioSource,
                                                 AudioSource fadeInAudioSource,
                                                 Transform followTarget = null,
                                                 Vector3 fadeInPosition = default,
                                                 CancellationToken cancellationToken = default)
        {
            return CrossFadeAsync(
                out _,
                duration,
                fadeOutAudioSource,
                fadeInAudioSource,
                followTarget,
                fadeInPosition,
                cancellationToken);
        }

        private SomaEntity CrossFade_Internal(float duration,
                                              SomaEntity fadeOutEntity,
                                              SomaProperties fadeInProperties,
                                              Transform followTarget,
                                              Vector3 fadeInPosition,
                                              Action onComplete)
        {
            if (fadeOutEntity == null) return null;

            Func<float, float> easeLinear = EasingFunctions.GetEasingFunction(Ease.Linear);

            Stop_Internal(fadeOutEntity, true, duration, easeLinear, null);

            return Play_Internal(
                fadeInProperties,
                followTarget,
                fadeInPosition,
                true,
                duration,
                easeLinear,
                onComplete);
        }

        private SomaEntity CrossFade_Internal(float duration,
                                              AudioSource fadeOutAudioSource,
                                              AudioSource fadeInAudioSource,
                                              Transform followTarget,
                                              Vector3 fadeInPosition,
                                              Action onComplete)
        {
            if (fadeOutAudioSource == null) return null;

            Func<float, float> easeLinear = EasingFunctions.GetEasingFunction(Ease.Linear);

            Stop_Internal(fadeOutAudioSource, true, duration, easeLinear, null);

            return Play_Internal(
                fadeInAudioSource,
                followTarget,
                fadeInPosition,
                true,
                duration,
                easeLinear,
                onComplete);
        }

        private DynamicTask CrossFadeAsync_Internal(out SomaEntity entity,
                                                    float duration,
                                                    SomaEntity fadeOutEntity,
                                                    SomaProperties fadeInProperties,
                                                    Transform followTarget,
                                                    Vector3 fadeInPosition,
                                                    CancellationToken cancellationToken)
        {
            if (fadeOutEntity == null)
            {
                entity = null;
                return default;
            }

            Func<float, float> easeLinear = EasingFunctions.GetEasingFunction(Ease.Linear);

            _ = StopAsync_Internal(fadeOutEntity, true, duration, easeLinear, cancellationToken);

            return PlayAsync_Internal(
                out entity,
                fadeInProperties,
                followTarget,
                fadeInPosition,
                true,
                duration,
                easeLinear,
                cancellationToken);
        }

        private DynamicTask CrossFadeAsync_Internal(out SomaEntity entity,
                                                    float duration,
                                                    AudioSource fadeOutAudioSource,
                                                    AudioSource fadeInAudioSource,
                                                    Transform followTarget,
                                                    Vector3 fadeInPosition,
                                                    CancellationToken cancellationToken)
        {
            if (fadeOutAudioSource == null)
            {
                entity = null;
                return default;
            }

            Func<float, float> easeLinear = EasingFunctions.GetEasingFunction(Ease.Linear);

            _ = StopAsync_Internal(fadeOutAudioSource, true, duration, easeLinear, cancellationToken);

            return PlayAsync_Internal(
                out entity,
                fadeInAudioSource,
                followTarget,
                fadeInPosition,
                true,
                duration,
                easeLinear,
                cancellationToken);
        }

        #endregion

        #region Management

        private static void AddToDictionaries(SomaEntity entity,
                                              ref Dictionary<SomaEntity, AudioSource> entityDictionary,
                                              ref Dictionary<AudioSource, SomaEntity> sourceDictionary)
        {
            AudioSource source = entity.FromExternalAudioSource ? entity.ExternalAudioSource : entity.AudioSource;

            entityDictionary.TryAdd(entity, source);
            sourceDictionary.TryAdd(source, entity);
        }

        private static void RemoveFromDictionaries(SomaEntity entity,
                                                   ref Dictionary<SomaEntity, AudioSource> entityDictionary,
                                                   ref Dictionary<AudioSource, SomaEntity> sourceDictionary)
        {
            AudioSource source = entity.FromExternalAudioSource ? entity.ExternalAudioSource : entity.AudioSource;

            entityDictionary.Remove(entity);
            sourceDictionary.Remove(source);
        }

        private void AddPlaying(SomaEntity entity)
        {
            AddToDictionaries(entity, ref _entitiesPlaying, ref _sourcesPlaying);
        }

        private void RemovePlaying(SomaEntity entity)
        {
            RemoveFromDictionaries(entity, ref _entitiesPlaying, ref _sourcesPlaying);
        }

        private void AddPaused(SomaEntity entity)
        {
            AddToDictionaries(entity, ref _entitiesPaused, ref _sourcesPaused);
        }

        private void RemovePaused(SomaEntity entity)
        {
            RemoveFromDictionaries(entity, ref _entitiesPaused, ref _sourcesPaused);
        }

        private void AddStopping(SomaEntity entity)
        {
            AddToDictionaries(entity, ref _entitiesStopping, ref _sourcesStopping);
        }

        private void RemoveStopping(SomaEntity entity)
        {
            RemoveFromDictionaries(entity, ref _entitiesStopping, ref _sourcesStopping);
        }

        private bool HasPlaying(SomaEntity entity) => _entitiesPlaying.ContainsKey(entity);

        private bool HasPlaying(AudioSource source, out SomaEntity entity)
        {
            return _sourcesPlaying.TryGetValue(source, out entity);
        }

        private bool HasPlaying(AudioSource source) => _sourcesPlaying.ContainsKey(source);

        private bool HasPaused(SomaEntity entity) => _entitiesPaused.ContainsKey(entity);

        private bool HasPaused(AudioSource source, out SomaEntity entity)
        {
            return _sourcesPaused.TryGetValue(source, out entity);
        }

        private bool HasPaused(AudioSource source) => _sourcesPaused.ContainsKey(source);

        private bool HasStopping(SomaEntity entity) => _entitiesStopping.ContainsKey(entity);

        private bool HasStopping(AudioSource source, out SomaEntity entity)
        {
            return _sourcesStopping.TryGetValue(source, out entity);
        }

        private bool HasStopping(AudioSource source) => _sourcesStopping.ContainsKey(source);

        private void ClearPlayingEntities()
        {
            _entitiesPlaying.Clear();
            _sourcesPlaying.Clear();
        }

        private void ClearPausedEntities()
        {
            _entitiesPaused.Clear();
            _sourcesPaused.Clear();
        }

        private void ClearStoppingEntities()
        {
            _entitiesStopping.Clear();
            _sourcesStopping.Clear();
        }

        #endregion

        #endregion

        #region Mixer

        /// <summary>
        /// Registers a <see cref="SomaVolumeMixerGroup"/> in the internal dictionary.
        /// </summary>
        /// <param name="group">The group to be registered.</param>
        /// <remarks>Once registered, grants access through various methods like <see cref="SetMixerGroupVolume"/> or <see cref="FadeMixerGroupVolume"/>.</remarks>
        public static void RegisterMixerVolumeGroup(SomaVolumeMixerGroup group)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return;

            s_instance.RegisterMixerVolumeGroup_Internal(group);
        }

        /// <summary>
        /// Unregisters a <see cref="SomaVolumeMixerGroup"/> from the internal dictionary.
        /// </summary>
        /// <param name="group">The group to be unregistered.</param>
        public static void UnregisterMixerVolumeGroup(SomaVolumeMixerGroup group)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return;

            s_instance.UnregisterMixerVolumeGroup_Internal(group);
        }

        /// <summary>
        /// Sets the volume for an Audio Mixer Group.
        /// </summary>
        /// <param name="exposedParameter">The exposed parameter with which to access the group, e.g. 'VolumeMusic'.</param>
        /// <param name="value">The volumes' new value.</param>
        /// <returns>True, if group with exposedParameter is registered.</returns>
        /// <remarks>Changing a volume stops any ongoing volume fades applied in the mixer.</remarks>
        public static bool SetMixerGroupVolume(string exposedParameter, float value)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return false;

            return s_instance.SetMixerGroupVolume_Internal(exposedParameter, value);
        }

        /// <summary>
        /// Increases the volume of an Audio Mixer Group incrementally.
        /// </summary>
        /// <param name="exposedParameter">The exposed parameter with which to access the group, e.g. 'VolumeMusic'.</param>
        /// <returns>True, if group with exposedParameter is registered.</returns>
        /// <remarks>Has no effect if no Volume Segments are defined in the <see cref="SomaVolumeMixerGroup"/>.</remarks>
        public static bool IncreaseMixerGroupVolume(string exposedParameter)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return false;

            return s_instance.IncreaseMixerGroupVolume_Internal(exposedParameter);
        }

        /// <summary>
        /// Decreases the volume of an Audio Mixer Group incrementally.
        /// </summary>
        /// <param name="exposedParameter">The exposed parameter with which to access the group, e.g. 'VolumeMusic'.</param>
        /// <returns>True, if group with exposedParameter is registered.</returns>
        /// <remarks>Has no effect if no Volume Segments are defined in the <see cref="SomaVolumeMixerGroup"/>.</remarks>
        public static bool DecreaseMixerGroupVolume(string exposedParameter)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return false;

            return s_instance.DecreaseMixerGroupVolume_Internal(exposedParameter);
        }

        /// <summary>
        /// Mutes/Un-mutes the volume of an Audio Mixer Group by setting the volume to 0 or reapplying the previously stored value.
        /// </summary>
        /// <param name="exposedParameter">The exposed parameter with which to access the group, e.g. 'VolumeMusic'.</param>
        /// <param name="value">True = muted, False = unmuted.</param>
        /// <returns>True, if group with exposedParameter is registered.</returns>
        public static bool MuteMixerGroupVolume(string exposedParameter, bool value)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return false;

            return s_instance.MuteMixerGroupVolume_Internal(exposedParameter, value);
        }

        /// <summary>
        /// Fades the volume of an Audio Mixer Group.
        /// </summary>
        /// <param name="exposedParameter">The exposed parameter with which to access the group, e.g. 'VolumeMusic'.</param>
        /// <param name="targetVolume">The target volume reached at the end of the fade.</param>
        /// <param name="duration">The duration in seconds the fade will prolong.</param>
        /// <param name="ease">The easing applied when fading.</param>
        /// <param name="onComplete">Optional callback once mixer completes fading.</param>
        /// <returns>True, if group with exposedParameter is registered.</returns>
        public static bool FadeMixerGroupVolume(string exposedParameter,
                                                float targetVolume,
                                                float duration,
                                                Ease ease = Ease.Linear,
                                                Action onComplete = null)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return false;

            return s_instance.FadeMixerGroupVolume_Internal(
                exposedParameter,
                targetVolume,
                duration,
                EasingFunctions.GetEasingFunction(ease),
                onComplete);
        }

        /// <summary>
        /// Asynchronously fades the volume of an Audio Mixer Group.
        /// </summary>
        /// <param name="exposedParameter">The exposed parameter with which to access the group, e.g. 'VolumeMusic'.</param>
        /// <param name="targetVolume">The target volume reached at the end of the fade.</param>
        /// <param name="duration">The duration in seconds the fade will prolong.</param>
        /// <param name="ease">The easing applied when fading.</param>
        /// <param name="cancellationToken">Optional token for cancelling the fading.</param>
        public static DynamicTask FadeMixerGroupVolumeAsync(string exposedParameter,
                                                            float targetVolume,
                                                            float duration,
                                                            Ease ease = Ease.Linear,
                                                            CancellationToken cancellationToken = default)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return default;

            return s_instance.FadeMixerGroupVolumeAsync_Internal(
                exposedParameter,
                targetVolume,
                duration,
                EasingFunctions.GetEasingFunction(ease),
                cancellationToken);
        }

        /// <summary>
        /// Linearly cross-fades the volume of two Audio Mixer Groups.
        /// </summary>
        /// <param name="fadeOutExposedParameter">The exposed parameter with which to access the group fading out, e.g. 'VolumeSFX'.</param>
        /// <param name="fadeInExposedParameter">The exposed parameter with which to access the group fading in, e.g. 'VolumeMusic'.</param>
        /// <param name="duration">The duration in seconds the cross-fade will prolong.</param>
        /// <param name="onComplete">Optional callback once mixer completes cross-fading.</param>
        /// <returns>True, if both fadeOutExposedParameter and fadeInExposedParameter are registered.</returns>
        public static bool CrossFadeMixerGroupVolumes(string fadeOutExposedParameter,
                                                      string fadeInExposedParameter,
                                                      float duration,
                                                      Action onComplete = null)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return false;

            return s_instance.CrossFadeMixerGroupVolumes_Internal(
                fadeOutExposedParameter,
                fadeInExposedParameter,
                duration,
                onComplete);
        }

        /// <summary>
        /// Asynchronously and linearly cross-fades the volume of two Audio Mixer Groups.
        /// </summary>
        /// <param name="fadeOutExposedParameter">The exposed parameter with which to access the group fading out, e.g. 'VolumeSFX'.</param>
        /// <param name="fadeInExposedParameter">The exposed parameter with which to access the group fading in, e.g. 'VolumeMusic'.</param>
        /// <param name="duration">The duration in seconds the cross-fade will prolong.</param>
        /// <param name="cancellationToken">Optional token for cancelling the fading.</param>
        public static DynamicTask CrossFadeMixerGroupVolumesAsync(string fadeOutExposedParameter,
                                                                  string fadeInExposedParameter,
                                                                  float duration,
                                                                  CancellationToken cancellationToken = default)
        {
            if (!TryGetInstance(out s_instance) || !s_setupSoma) return default;

            return s_instance.CrossFadeMixerGroupVolumesAsync_Internal(
                fadeOutExposedParameter,
                fadeInExposedParameter,
                duration,
                cancellationToken);
        }

        private void RegisterMixerVolumeGroup_Internal(SomaVolumeMixerGroup group)
        {
            if (group == null) return;

            string exposedParameter = group.ExposedParameter;

            if (string.IsNullOrWhiteSpace(exposedParameter))
            {
                Debug.LogError(
                    $"You are trying to register a {nameof(SomaVolumeMixerGroup)} with an empty exposed parameter. " +
                    "\nThis is not allowed.");

                return;
            }

            if (!group.AudioMixer.HasParameter(exposedParameter))
            {
                Debug.LogError(
                    $"You are trying to register a {nameof(SomaVolumeMixerGroup)} with the exposed parameter " +
                    $"'{exposedParameter}' for the Audio Mixer '{group.AudioMixer.name}'. " +
                    "\nPlease expose the necessary parameter via the Editor or check your spelling.");

                return;
            }

            if (_mixerVolumeGroups.TryAdd(exposedParameter, group)) return;

            Debug.LogWarning(
                $"You are trying to register a {nameof(SomaVolumeMixerGroup)} with the exposed parameter " +
                $"'{exposedParameter}' for the Audio Mixer '{group.AudioMixer.name}'. " +
                "\nThe parameter was either already registered or you were trying to register it with multiple Audio Mixers." +
                "\nIt is not allowed to use the same exposed parameter for different Audio Mixers.");
        }

        private void UnregisterMixerVolumeGroup_Internal(SomaVolumeMixerGroup group)
        {
            if (group == null) return;

            _mixerVolumeGroups.Remove(group.ExposedParameter);
        }

        private bool SetMixerGroupVolume_Internal(string exposedParameter, float value)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out SomaVolumeMixerGroup mixerVolumeGroup)) return false;

            CancelCurrentMixerFading(exposedParameter);

            mixerVolumeGroup.Set(value);

            return true;
        }

        private bool IncreaseMixerGroupVolume_Internal(string exposedParameter)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out SomaVolumeMixerGroup mixerVolumeGroup)) return false;

            CancelCurrentMixerFading(exposedParameter);

            mixerVolumeGroup.Increase();

            return true;
        }

        private bool DecreaseMixerGroupVolume_Internal(string exposedParameter)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out SomaVolumeMixerGroup mixerVolumeGroup)) return false;

            CancelCurrentMixerFading(exposedParameter);

            mixerVolumeGroup.Decrease();

            return true;
        }

        private bool MuteMixerGroupVolume_Internal(string exposedParameter, bool value)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out SomaVolumeMixerGroup mixerVolumeGroup)) return false;

            CancelCurrentMixerFading(exposedParameter);

            mixerVolumeGroup.Mute(value);

            return true;
        }

        private bool FadeMixerGroupVolume_Internal(string exposedParameter,
                                                   float targetVolume,
                                                   float duration,
                                                   Func<float, float> easeFunction,
                                                   Action onComplete)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out SomaVolumeMixerGroup mixerVolumeGroup)) return false;

            CancelCurrentMixerFading(exposedParameter);

#if UNITASK_INCLUDED
            CancellationTokenSource cts = new();
            _mixerFadeCancellationTokenSources.TryAdd(exposedParameter, cts);

            DoFadeTask(cts.Token).Forget();

            return true;

            async UniTaskVoid DoFadeTask(CancellationToken cancellationToken)
            {
                await FadeMixerTask(mixerVolumeGroup, duration, targetVolume, easeFunction, cancellationToken);

                onComplete?.Invoke();

                _mixerFadeCancellationTokenSources.Remove(exposedParameter);
            }
#else
            _mixerFadeRoutines.TryAdd(exposedParameter, StartCoroutine(DoFadeRoutine()));

            return true;

            IEnumerator DoFadeRoutine()
            {
                yield return FadeMixerRoutine(mixerVolumeGroup, duration, targetVolume, easeFunction);

                onComplete?.Invoke();

                _mixerFadeRoutines.Remove(exposedParameter);
            }
#endif
        }

        private async DynamicTask FadeMixerGroupVolumeAsync_Internal(string exposedParameter,
                                                                     float targetVolume,
                                                                     float duration,
                                                                     Func<float, float> easeFunction,
                                                                     CancellationToken cancellationToken)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out SomaVolumeMixerGroup mixerVolumeGroup)) return;

            CancelCurrentMixerFading(exposedParameter);

            CancellationTokenSource cts = new();
            TokenHelper.Link(ref cancellationToken, ref cts);

            _mixerFadeCancellationTokenSources.TryAdd(exposedParameter, cts);

            await FadeMixerTask(mixerVolumeGroup, duration, targetVolume, easeFunction, cancellationToken);

            _mixerFadeCancellationTokenSources.Remove(exposedParameter);
        }

        private bool CrossFadeMixerGroupVolumes_Internal(string fadeOutExposedParameter,
                                                         string fadeInExposedParameter,
                                                         float duration,
                                                         Action onComplete)
        {
            bool bothRegistered = MixerVolumeGroupRegistered(fadeOutExposedParameter, out SomaVolumeMixerGroup _) &&
                                  MixerVolumeGroupRegistered(fadeInExposedParameter, out SomaVolumeMixerGroup _);

            if (!bothRegistered) return false;

            Func<float, float> easeLinear = EasingFunctions.GetEasingFunction(Ease.Linear);

            FadeMixerGroupVolume_Internal(fadeOutExposedParameter, 0, duration, easeLinear, null);
            FadeMixerGroupVolume_Internal(fadeInExposedParameter, 1, duration, easeLinear, onComplete);

            return true;
        }

        private DynamicTask CrossFadeMixerGroupVolumesAsync_Internal(string fadeOutExposedParameter,
                                                                     string fadeInExposedParameter,
                                                                     float duration,
                                                                     CancellationToken cancellationToken = default)
        {
            Func<float, float> easeLinear = EasingFunctions.GetEasingFunction(Ease.Linear);

            _ = FadeMixerGroupVolumeAsync_Internal(fadeOutExposedParameter, 0, duration, easeLinear, cancellationToken);

            return FadeMixerGroupVolumeAsync_Internal(
                fadeInExposedParameter,
                1,
                duration,
                easeLinear,
                cancellationToken);
        }

#if !UNITASK_INCLUDED
        private static IEnumerator FadeMixerRoutine(SomaVolumeMixerGroup mixerVolumeGroup,
                                                    float duration,
                                                    float targetVolume,
                                                    Func<float, float> easeFunction)
        {
            targetVolume = Mathf.Clamp01(targetVolume);

            if (duration <= 0)
            {
                mixerVolumeGroup.Set(targetVolume);
                yield break;
            }

            float deltaTime = 0;
            float startVolume = mixerVolumeGroup.VolumeCurrent;

            while (deltaTime < duration)
            {
                deltaTime += Time.deltaTime;
                mixerVolumeGroup.Set(Mathf.Lerp(startVolume, targetVolume, easeFunction(deltaTime / duration)));

                yield return null;
            }

            mixerVolumeGroup.Set(targetVolume);
        }
#endif

        private static async DynamicTask FadeMixerTask(SomaVolumeMixerGroup somaVolumeMixerGroup,
                                                       float duration,
                                                       float targetVolume,
                                                       Func<float, float> easeFunction,
                                                       CancellationToken cancellationToken = default)
        {
            targetVolume = Mathf.Clamp01(targetVolume);

            if (duration <= 0)
            {
                somaVolumeMixerGroup.Set(targetVolume);
                return;
            }

            float deltaTime = 0;
            float startVolume = somaVolumeMixerGroup.VolumeCurrent;

            while (deltaTime < duration)
            {
                if (cancellationToken.IsCancellationRequested) return;
                
                deltaTime += Time.deltaTime;
                somaVolumeMixerGroup.Set(Mathf.Lerp(startVolume, targetVolume, easeFunction(deltaTime / duration)));

                await DynamicTask.Yield();
            }

            somaVolumeMixerGroup.Set(targetVolume);
        }

        private bool MixerVolumeGroupRegistered(string exposedParameter, out SomaVolumeMixerGroup somaVolumeMixerGroup)
        {
            if (_mixerVolumeGroups.TryGetValue(exposedParameter, out somaVolumeMixerGroup)) return true;

            Debug.LogError($"There is no {nameof(SomaVolumeMixerGroup)} for {exposedParameter} registered.");
            return false;
        }

        private void CancelCurrentMixerFading(string exposedParameter)
        {
#if !UNITASK_INCLUDED
            if (_mixerFadeRoutines.Remove(exposedParameter, out Coroutine fadeRoutine))
            {
                StopCoroutine(fadeRoutine);
            }
#endif

            if (_mixerFadeCancellationTokenSources.Remove(exposedParameter, out CancellationTokenSource cts))
            {
                TokenHelper.Cancel(ref cts);
            }
        }

        #endregion
    }
}