using System;
using System.Collections.Generic;
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

        private bool _setup;

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

        #region Setup

        protected override void Setup()
        {
            base.Setup();

            if (s_instance != this) return;

            SetupIfNeeded();
        }

        private void SetupIfNeeded()
        {
            if (_setup) return;

            _setup = true;

            SetupEntities();
            SetupMixers();
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
                    GameObject obj = new($"SomaEntity-{_entityPool.CountAll}");
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

        /// <inheritdoc cref="Play_Properties"/>
        public static SomaEntity Play(SomaProperties properties,
                                      Transform followTarget = null,
                                      Vector3 position = default,
                                      bool fadeIn = false,
                                      float fadeInDuration = .5f,
                                      Ease fadeInEase = Ease.Linear,
                                      Action onComplete = null)
        {
            return Instance.Play_Properties(
                properties,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                onComplete);
        }

        /// <inheritdoc cref="Play_Source"/>
        public static SomaEntity Play(AudioSource audioSource,
                                      Transform followTarget = null,
                                      Vector3 position = default,
                                      bool fadeIn = false,
                                      float fadeInDuration = .5f,
                                      Ease fadeInEase = Ease.Linear,
                                      Action onComplete = null)
        {
            return Instance.Play_Source(
                audioSource,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                onComplete);
        }

        /// <inheritdoc cref="Play_Clip"/>
        public static SomaEntity Play(AudioClip audioClip,
                                      Transform followTarget = null,
                                      Vector3 position = default,
                                      bool fadeIn = false,
                                      float fadeInDuration = .5f,
                                      Ease fadeInEase = Ease.Linear,
                                      Action onComplete = null)
        {
            return Instance.Play_Clip(
                audioClip,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                onComplete);
        }

        /// <inheritdoc cref="PlayAsync_Properties_Outed"/>
        public static DynamicTask PlayAsync(out SomaEntity entity,
                                            SomaProperties properties,
                                            Transform followTarget = null,
                                            Vector3 position = default,
                                            bool fadeIn = false,
                                            float fadeInDuration = .5f,
                                            Ease fadeInEase = Ease.Linear,
                                            CancellationToken cancellationToken = default)
        {
            return Instance.PlayAsync_Properties_Outed(
                out entity,
                properties,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                cancellationToken);
        }

        /// <inheritdoc cref="PlayAsync_Source_Outed"/>
        public static DynamicTask PlayAsync(out SomaEntity entity,
                                            AudioSource audioSource,
                                            Transform followTarget = null,
                                            Vector3 position = default,
                                            bool fadeIn = false,
                                            float fadeInDuration = .5f,
                                            Ease fadeInEase = Ease.Linear,
                                            CancellationToken cancellationToken = default)
        {
            return Instance.PlayAsync_Source_Outed(
                out entity,
                audioSource,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                cancellationToken);
        }

        /// <inheritdoc cref="PlayAsync_Clip_Outed"/>
        public static DynamicTask PlayAsync(out SomaEntity entity,
                                            AudioClip audioClip,
                                            Transform followTarget = null,
                                            Vector3 position = default,
                                            bool fadeIn = false,
                                            float fadeInDuration = .5f,
                                            Ease fadeInEase = Ease.Linear,
                                            CancellationToken cancellationToken = default)
        {
            return Instance.PlayAsync_Clip_Outed(
                out entity,
                audioClip,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                cancellationToken);
        }

        /// <inheritdoc cref="PlayAsync_Properties"/>
        public static DynamicTask PlayAsync(SomaProperties properties,
                                            Transform followTarget = null,
                                            Vector3 position = default,
                                            bool fadeIn = false,
                                            float fadeInDuration = .5f,
                                            Ease fadeInEase = Ease.Linear,
                                            CancellationToken cancellationToken = default)
        {
            return Instance.PlayAsync_Properties(
                properties,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                cancellationToken);
        }

        /// <inheritdoc cref="PlayAsync_Source"/>
        public static DynamicTask PlayAsync(AudioSource audioSource,
                                            Transform followTarget = null,
                                            Vector3 position = default,
                                            bool fadeIn = false,
                                            float fadeInDuration = .5f,
                                            Ease fadeInEase = Ease.Linear,
                                            CancellationToken cancellationToken = default)
        {
            return Instance.PlayAsync_Source(
                audioSource,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                cancellationToken);
        }

        /// <inheritdoc cref="PlayAsync_Clip"/>
        public static DynamicTask PlayAsync(AudioClip audioClip,
                                            Transform followTarget = null,
                                            Vector3 position = default,
                                            bool fadeIn = false,
                                            float fadeInDuration = .5f,
                                            Ease fadeInEase = Ease.Linear,
                                            CancellationToken cancellationToken = default)
        {
            return Instance.PlayAsync_Clip(
                audioClip,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                cancellationToken);
        }

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
        private SomaEntity Play_Properties(SomaProperties properties,
                                           Transform followTarget = null,
                                           Vector3 position = default,
                                           bool fadeIn = false,
                                           float fadeInDuration = .5f,
                                           Ease fadeInEase = Ease.Linear,
                                           Action onComplete = null)
        {
            SetupIfNeeded();

            SomaEntity entity = _entityPool.Get();

            entity.Play(properties, followTarget, position, fadeIn, fadeInDuration, fadeInEase, onComplete);
            AddPlaying(entity);

            return entity;
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
        private SomaEntity Play_Source(AudioSource audioSource,
                                       Transform followTarget = null,
                                       Vector3 position = default,
                                       bool fadeIn = false,
                                       float fadeInDuration = .5f,
                                       Ease fadeInEase = Ease.Linear,
                                       Action onComplete = null)
        {
            if (audioSource == null || audioSource.clip == null) return null;

            SetupIfNeeded();

            if (HasPlaying(audioSource, out SomaEntity playingEntity)) return playingEntity;

            if (HasPaused(audioSource, out SomaEntity pausedEntity))
            {
                Resume(pausedEntity);
                return pausedEntity;
            }

            SomaEntity entity = _entityPool.Get();

            entity.Play(audioSource, followTarget, position, fadeIn, fadeInDuration, fadeInEase, onComplete);
            AddPlaying(entity);

            return entity;
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
        private SomaEntity Play_Clip(AudioClip audioClip,
                                     Transform followTarget = null,
                                     Vector3 position = default,
                                     bool fadeIn = false,
                                     float fadeInDuration = .5f,
                                     Ease fadeInEase = Ease.Linear,
                                     Action onComplete = null)
        {
            if (audioClip == null) return null;

            return Play_Properties(
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
        private DynamicTask PlayAsync_Properties_Outed(out SomaEntity entity,
                                                       SomaProperties properties,
                                                       Transform followTarget = null,
                                                       Vector3 position = default,
                                                       bool fadeIn = false,
                                                       float fadeInDuration = .5f,
                                                       Ease fadeInEase = Ease.Linear,
                                                       CancellationToken cancellationToken = default)
        {
            SetupIfNeeded();

            entity = _entityPool.Get();

            DynamicTask task = entity.PlayAsync(
                properties,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                cancellationToken);

            AddPlaying(entity);

            return task;
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
        private DynamicTask PlayAsync_Source_Outed(out SomaEntity entity,
                                                   AudioSource audioSource,
                                                   Transform followTarget = null,
                                                   Vector3 position = default,
                                                   bool fadeIn = false,
                                                   float fadeInDuration = .5f,
                                                   Ease fadeInEase = Ease.Linear,
                                                   CancellationToken cancellationToken = default)
        {
            if (audioSource == null || audioSource.clip == null)
            {
                entity = null;
                return default;
            }

            SetupIfNeeded();

            entity = _entityPool.Get();

            DynamicTask task = entity.PlayAsync(
                audioSource,
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                cancellationToken);

            AddPlaying(entity);

            return task;
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
        private DynamicTask PlayAsync_Clip_Outed(out SomaEntity entity,
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

            return PlayAsync_Properties_Outed(
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
        private DynamicTask PlayAsync_Properties(SomaProperties properties,
                                                 Transform followTarget = null,
                                                 Vector3 position = default,
                                                 bool fadeIn = false,
                                                 float fadeInDuration = .5f,
                                                 Ease fadeInEase = Ease.Linear,
                                                 CancellationToken cancellationToken = default)
        {
            return PlayAsync_Properties_Outed(
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
        private DynamicTask PlayAsync_Source(AudioSource audioSource,
                                             Transform followTarget = null,
                                             Vector3 position = default,
                                             bool fadeIn = false,
                                             float fadeInDuration = .5f,
                                             Ease fadeInEase = Ease.Linear,
                                             CancellationToken cancellationToken = default)
        {
            return PlayAsync_Source_Outed(
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
        private DynamicTask PlayAsync_Clip(AudioClip audioClip,
                                           Transform followTarget = null,
                                           Vector3 position = default,
                                           bool fadeIn = false,
                                           float fadeInDuration = .5f,
                                           Ease fadeInEase = Ease.Linear,
                                           CancellationToken cancellationToken = default)
        {
            if (audioClip == null) return default;

            return PlayAsync_Properties(
                new SomaProperties(audioClip),
                followTarget,
                position,
                fadeIn,
                fadeInDuration,
                fadeInEase,
                cancellationToken);
        }

        #endregion

        #region Pause

        /// <inheritdoc cref="Pause_Entity"/>
        public static bool Pause(SomaEntity entity)
        {
            return Instance.Pause_Entity(entity);
        }

        /// <inheritdoc cref="Pause_Source"/>
        public static bool Pause(AudioSource audioSource)
        {
            return Instance.Pause_Source(audioSource);
        }

        /// <inheritdoc cref="PauseAll_Entities"/>
        public static bool PauseAll()
        {
            return Instance.PauseAll_Entities();
        }

        /// <summary>
        /// Pauses a playing sound that is managed by Soma.
        /// </summary>
        /// <param name="entity">The entity that is currently playing.</param>
        /// <returns>True, if pausing was successful.</returns>
        /// <remarks>Has no effect if the entity is currently stopping.</remarks>
        private bool Pause_Entity(SomaEntity entity)
        {
            if (entity == null) return false;

            SetupIfNeeded();

            if (!HasPlaying(entity)) return false;
            if (HasStopping(entity)) return false;

            RemovePlaying(entity);

            entity.Pause();

            AddPaused(entity);

            return true;
        }

        /// <summary>
        /// Pauses a playing sound that is managed by Soma.
        /// </summary>
        /// <param name="audioSource">The source of the sound.</param>
        /// <returns>True, if pausing was successful.</returns>
        /// <remarks>Has no effect if the source is currently stopping.</remarks>
        private bool Pause_Source(AudioSource audioSource)
        {
            if (audioSource == null) return false;

            SetupIfNeeded();

            if (HasStopping(audioSource)) return false;
            if (!HasPlaying(audioSource, out SomaEntity entity)) return false;

            RemovePlaying(entity);

            entity.Pause();

            AddPaused(entity);

            return true;
        }

        /// <summary>
        /// Pauses all currently playing sounds that are managed by Soma.
        /// </summary>
        /// <remarks>Has no effect on sounds currently stopping.</remarks>
        private bool PauseAll_Entities()
        {
            SetupIfNeeded();

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

        /// <inheritdoc cref="Resume_Entity"/>
        public static bool Resume(SomaEntity entity)
        {
            return Instance.Resume_Entity(entity);
        }

        /// <inheritdoc cref="Resume_Source"/>
        public static bool Resume(AudioSource audioSource)
        {
            return Instance.Resume_Source(audioSource);
        }

        /// <inheritdoc cref="ResumeAll_Entities"/>
        public static bool ResumeAll()
        {
            return Instance.ResumeAll_Entities();
        }

        /// <summary>
        /// Resumes a paused sound that is managed by Soma.
        /// </summary>
        /// <param name="entity">The entity that is currently paused.</param>
        /// <returns>True, if resuming was successful.</returns>
        /// <remarks>Has no effect if the entity is currently stopping.</remarks>
        private bool Resume_Entity(SomaEntity entity)
        {
            if (entity == null) return false;

            SetupIfNeeded();

            if (!HasPaused(entity)) return false;
            if (HasStopping(entity)) return false;

            RemovePaused(entity);

            entity.Resume();

            AddPlaying(entity);

            return true;
        }

        /// <summary>
        /// Resumes a paused sound that is managed by Soma.
        /// </summary>
        /// <param name="audioSource">The source of the sound.</param>
        /// <returns>True, if resuming was successful.</returns>
        /// <remarks>Has no effect if the source is currently stopping.</remarks>
        private bool Resume_Source(AudioSource audioSource)
        {
            if (audioSource == null) return false;

            SetupIfNeeded();

            if (HasStopping(audioSource)) return false;
            if (!HasPaused(audioSource, out SomaEntity entity)) return false;

            RemovePaused(entity);

            entity.Resume();

            AddPlaying(entity);

            return true;
        }

        /// <summary>
        /// Resumes all currently paused sounds that are managed by Soma.
        /// </summary>
        private bool ResumeAll_Entities()
        {
            SetupIfNeeded();

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

        /// <inheritdoc cref="Stop_Entity"/>
        public static void Stop(SomaEntity entity,
                                bool fadeOut = true,
                                float fadeOutDuration = .5f,
                                Ease fadeOutEase = Ease.Linear,
                                Action onComplete = null)
        {
            Instance.Stop_Entity(entity, fadeOut, fadeOutDuration, fadeOutEase, onComplete);
        }

        /// <inheritdoc cref="Stop_Source"/>
        public static void Stop(AudioSource audioSource,
                                bool fadeOut = true,
                                float fadeOutDuration = .5f,
                                Ease fadeOutEase = Ease.Linear,
                                Action onComplete = null)
        {
            Instance.Stop_Source(audioSource, fadeOut, fadeOutDuration, fadeOutEase, onComplete);
        }

        /// <inheritdoc cref="StopAsync_Entity"/>
        public static DynamicTask StopAsync(SomaEntity entity,
                                            bool fadeOut = true,
                                            float fadeOutDuration = .5f,
                                            Ease fadeOutEase = Ease.Linear,
                                            CancellationToken cancellationToken = default)
        {
            return Instance.StopAsync_Entity(entity, fadeOut, fadeOutDuration, fadeOutEase, cancellationToken);
        }

        /// <inheritdoc cref="StopAsync_Source"/>
        public static DynamicTask StopAsync(AudioSource audioSource,
                                            bool fadeOut = true,
                                            float fadeOutDuration = .5f,
                                            Ease fadeOutEase = Ease.Linear,
                                            CancellationToken cancellationToken = default)
        {
            return Instance.StopAsync_Source(audioSource, fadeOut, fadeOutDuration, fadeOutEase, cancellationToken);
        }

        /// <inheritdoc cref="StopAll_Entities"/>
        public static void StopAll(bool fadeOut = true,
                                   float fadeOutDuration = 1,
                                   Ease fadeOutEase = Ease.Linear)
        {
            Instance.StopAll_Entities(fadeOut, fadeOutDuration, fadeOutEase);
        }

        /// <inheritdoc cref="StopAllAsync_Entities"/>
        public static DynamicTask StopAllAsync(bool fadeOut = true,
                                               float fadeOutDuration = 1,
                                               Ease fadeOutEase = Ease.Linear,
                                               CancellationToken cancellationToken = default)
        {
            return Instance.StopAllAsync_Entities(fadeOut, fadeOutDuration, fadeOutEase, cancellationToken);
        }

        /// <summary>
        /// Stops playback of a playing/paused sound that is managed by Soma.
        /// </summary>
        /// <param name="entity">The entity that is either currently playing or paused.</param>
        /// <param name="fadeOut">True by default. Set this to false, if the volume should not fade out when stopping.</param>
        /// <param name="fadeOutDuration">The duration in seconds the fading out will prolong.</param>
        /// <param name="fadeOutEase">The easing applied when fading out.</param>
        /// <param name="onComplete">Optional callback once sound completes stopping.</param>
        /// <remarks>Paused sounds will be stopped without fade out regardless.</remarks>
        internal void Stop_Entity(SomaEntity entity,
                                  bool fadeOut = true,
                                  float fadeOutDuration = .5f,
                                  Ease fadeOutEase = Ease.Linear,
                                  Action onComplete = null)
        {
            if (entity == null) return;

            SetupIfNeeded();

            bool playingEntity = HasPlaying(entity);
            bool pausedEntity = HasPaused(entity);

            if (!playingEntity && !pausedEntity) return;

            if (playingEntity) RemovePlaying(entity);
            if (pausedEntity) RemovePaused(entity);
            
            AddStopping(entity);
            entity.Stop(fadeOut, fadeOutDuration, fadeOutEase, OnStopComplete);

            return;

            void OnStopComplete()
            {
                RemoveStopping(entity);

                _entityPool.Release(entity);

                onComplete?.Invoke();
            }
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
        private void Stop_Source(AudioSource audioSource,
                                 bool fadeOut = true,
                                 float fadeOutDuration = .5f,
                                 Ease fadeOutEase = Ease.Linear,
                                 Action onComplete = null)
        {
            if (audioSource == null) return;

            SetupIfNeeded();

            bool playingAudioSource = HasPlaying(audioSource, out SomaEntity entityPlaying);
            bool pausedAudioSource = HasPaused(audioSource, out SomaEntity entityPaused);

            if (!playingAudioSource && !pausedAudioSource) return;

            if (playingAudioSource) RemovePlaying(entityPlaying);
            if (pausedAudioSource) RemovePaused(entityPaused);
            
            SomaEntity entity = entityPlaying != null ? entityPlaying : entityPaused;
            AddStopping(entity);
            entity.Stop(fadeOut, fadeOutDuration, fadeOutEase, OnStopComplete);

            return;

            void OnStopComplete()
            {
                RemoveStopping(entity);

                _entityPool.Release(entity);

                onComplete?.Invoke();
            }
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
        internal async DynamicTask StopAsync_Entity(SomaEntity entity,
                                                    bool fadeOut = true,
                                                    float fadeOutDuration = .5f,
                                                    Ease fadeOutEase = Ease.Linear,
                                                    CancellationToken cancellationToken = default)
        {
            if (entity == null) return;

            SetupIfNeeded();

            bool playingEntity = HasPlaying(entity);
            bool pausedEntity = HasPaused(entity);

            if (!playingEntity && !pausedEntity) return;
            
            if (playingEntity) RemovePlaying(entity);
            if (pausedEntity) RemovePaused(entity);

            AddStopping(entity);

            await entity.StopAsync(fadeOut, fadeOutDuration, fadeOutEase, cancellationToken);
            
            RemoveStopping(entity);

            _entityPool.Release(entity);
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
        private async DynamicTask StopAsync_Source(AudioSource audioSource,
                                                   bool fadeOut = true,
                                                   float fadeOutDuration = .5f,
                                                   Ease fadeOutEase = Ease.Linear,
                                                   CancellationToken cancellationToken = default)
        {
            if (audioSource == null) return;

            SetupIfNeeded();

            bool playingAudioSource = HasPlaying(audioSource, out SomaEntity entityPlaying);
            bool pausedAudioSource = HasPaused(audioSource, out SomaEntity entityPaused);

            if (!playingAudioSource && !pausedAudioSource) return;
            
            if (playingAudioSource) RemovePlaying(entityPlaying);
            if (pausedAudioSource) RemovePaused(entityPaused);

            SomaEntity entity = entityPlaying != null ? entityPlaying : entityPaused;
            AddStopping(entity);

            await entity.StopAsync(fadeOut, fadeOutDuration, fadeOutEase, cancellationToken);
            
            RemoveStopping(entity);

            _entityPool.Release(entity);
        }

        /// <summary>
        /// Stops all currently playing/paused sounds that are managed by Soma.
        /// </summary>
        /// <param name="fadeOut">True by default. Set this to false, if the volumes should not fade out when stopping.</param>
        /// <param name="fadeOutDuration">The duration in seconds the fading out will prolong.</param>
        /// <param name="fadeOutEase">The easing applied when fading out.</param>
        private void StopAll_Entities(bool fadeOut = true,
                                      float fadeOutDuration = 1,
                                      Ease fadeOutEase = Ease.Linear)
        {
            SetupIfNeeded();

            foreach ((SomaEntity entity, AudioSource _) in _entitiesPlaying)
            {
                Stop_Entity(entity, fadeOut, fadeOutDuration, fadeOutEase);
            }

            foreach ((SomaEntity entity, AudioSource _) in _entitiesPaused)
            {
                entity.Stop(false);
                _entityPool.Release(entity);
            }

            ClearPausedEntities();
        }

        /// <summary>
        /// Asynchronously stops all currently playing/paused sounds that are managed by Soma.
        /// </summary>
        /// <param name="fadeOut">True by default. Set this to false, if the volumes should not fade out when stopping.</param>
        /// <param name="fadeOutDuration">The duration in seconds the fading out will prolong.</param>
        /// <param name="fadeOutEase">The easing applied when fading out.</param>
        /// <param name="cancellationToken">Optional token for cancelling the stopping.</param>
        /// <returns>The stopping task.</returns>
        private async DynamicTask StopAllAsync_Entities(bool fadeOut = true,
                                                        float fadeOutDuration = 1,
                                                        Ease fadeOutEase = Ease.Linear,
                                                        CancellationToken cancellationToken = default)
        {
            SetupIfNeeded();

            List<DynamicTask> stopTasks = new();

            foreach ((SomaEntity entity, AudioSource _) in _entitiesPlaying)
            {
                stopTasks.Add(StopAsync_Entity(entity, fadeOut, fadeOutDuration, fadeOutEase, cancellationToken));
            }

            foreach ((SomaEntity entity, AudioSource _) in _entitiesPaused)
            {
                stopTasks.Add(entity.StopAsync(false, cancellationToken: cancellationToken));
                _entityPool.Release(entity);
            }

            ClearPausedEntities();

            await DynamicTask.WhenAll(stopTasks);
        }

        #endregion

        #region Set

        /// <inheritdoc cref="Set_Entity"/>
        public static void Set(SomaEntity entity,
                               SomaProperties properties,
                               Transform followTarget,
                               Vector3 position)
        {
            Instance.Set_Entity(entity, properties, followTarget, position);
        }

        /// <inheritdoc cref="Set_Source"/>
        public static void Set(AudioSource audioSource,
                               SomaProperties properties,
                               Transform followTarget,
                               Vector3 position)
        {
            Instance.Set_Source(audioSource, properties, followTarget, position);
        }

        /// <summary>
        /// Sets the properties of a playing/paused sound.
        /// </summary>
        /// <param name="entity">The entity whose properties will be changed.</param>
        /// <param name="properties">The new properties.</param>
        /// <param name="followTarget">The target the sound will follow while playing (none if null).</param>
        /// <param name="position">Either the global position or, when following, the position offset of the sound.</param>
        /// <remarks>Will change ALL properties, the followTarget and the position.
        /// Be sure to retrieve the original properties (e.g. via copy constructor), if you only want to change certain properties.</remarks>
        private void Set_Entity(SomaEntity entity,
                                SomaProperties properties,
                                Transform followTarget,
                                Vector3 position)
        {
            if (entity == null) return;

            SetupIfNeeded();

            if (properties == null) return;

            entity.SetProperties(properties, followTarget, position);
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
        private void Set_Source(AudioSource audioSource,
                                SomaProperties properties,
                                Transform followTarget,
                                Vector3 position)
        {
            if (audioSource == null) return;

            SetupIfNeeded();

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

        /// <inheritdoc cref="Fade_Entity"/>
        public static void Fade(SomaEntity entity,
                                float targetVolume,
                                float duration,
                                Ease ease = Ease.Linear,
                                Action onComplete = null)
        {
            Instance.Fade_Entity(entity, targetVolume, duration, ease, onComplete);
        }

        /// <inheritdoc cref="Fade_Source"/>
        public static void Fade(AudioSource audioSource,
                                float targetVolume,
                                float duration,
                                Ease ease = Ease.Linear,
                                Action onComplete = null)
        {
            Instance.Fade_Source(audioSource, targetVolume, duration, ease, onComplete);
        }

        /// <inheritdoc cref="FadeAsync_Entity"/>
        public static DynamicTask FadeAsync(SomaEntity entity,
                                            float targetVolume,
                                            float duration,
                                            Ease ease = Ease.Linear,
                                            CancellationToken cancellationToken = default)
        {
            return Instance.FadeAsync_Entity(entity, targetVolume, duration, ease, cancellationToken);
        }

        /// <inheritdoc cref="FadeAsync_Source"/>
        public DynamicTask FadeAsync(AudioSource audioSource,
                                     float targetVolume,
                                     float duration,
                                     Ease ease = Ease.Linear,
                                     CancellationToken cancellationToken = default)
        {
            return Instance.FadeAsync_Source(audioSource, targetVolume, duration, ease, cancellationToken);
        }

        /// <summary>
        /// Fades the volume of a playing sound that is managed by Soma.
        /// </summary>
        /// <param name="entity">The entity that is currently playing or paused.</param>
        /// <param name="targetVolume">The target volume reached at the end of the fade.</param>
        /// <param name="duration">The duration in seconds the fade will prolong.</param>
        /// <param name="ease">The easing applied when fading.</param>
        /// <param name="onComplete">Optional callback once sound completes fading.</param>
        /// <remarks>Has no effect on sounds currently stopping.</remarks>
        private void Fade_Entity(SomaEntity entity,
                                 float targetVolume,
                                 float duration,
                                 Ease ease = Ease.Linear,
                                 Action onComplete = null)
        {
            if (entity == null) return;

            SetupIfNeeded();

            if (!HasPlaying(entity)) return;
            if (HasStopping(entity)) return;

            if (HasPaused(entity)) Resume(entity);

            entity.Fade(targetVolume, duration, ease, onComplete);
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
        private void Fade_Source(AudioSource audioSource,
                                 float targetVolume,
                                 float duration,
                                 Ease ease = Ease.Linear,
                                 Action onComplete = null)
        {
            if (audioSource == null) return;

            SetupIfNeeded();

            if (!HasPlaying(audioSource, out SomaEntity entityPlaying)) return;
            if (HasStopping(audioSource)) return;

            if (HasPaused(audioSource, out SomaEntity entityPaused)) Resume(entityPaused);

            entityPlaying.Fade(targetVolume, duration, ease, onComplete);
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
        private DynamicTask FadeAsync_Entity(SomaEntity entity,
                                             float targetVolume,
                                             float duration,
                                             Ease ease = Ease.Linear,
                                             CancellationToken cancellationToken = default)
        {
            if (entity == null) return default;

            SetupIfNeeded();

            if (HasStopping(entity)) return default;

            if (HasPaused(entity)) Resume(entity);

            return entity.FadeAsync(targetVolume, duration, ease, cancellationToken);
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
        private DynamicTask FadeAsync_Source(AudioSource audioSource,
                                             float targetVolume,
                                             float duration,
                                             Ease ease = Ease.Linear,
                                             CancellationToken cancellationToken = default)
        {
            if (audioSource == null) return default;

            SetupIfNeeded();

            if (!HasPlaying(audioSource, out SomaEntity entityPlaying)) return default;
            if (HasStopping(audioSource)) return default;

            if (HasPaused(audioSource, out SomaEntity entityPaused)) Resume(entityPaused);

            return entityPlaying.FadeAsync(targetVolume, duration, ease, cancellationToken);
        }

#if !UNITASK_INCLUDED
        internal static IEnumerator FadeRoutine(AudioSource audioSource,
                                                float duration,
                                                float targetVolume,
                                                Ease ease = Ease.Linear,
                                                WaitWhile waitWhilePredicate = null)
        {
            return FadeRoutine(
                audioSource,
                duration,
                targetVolume,
                EasingFunctions.GetEasingFunction(ease),
                waitWhilePredicate);
        }

        private static IEnumerator FadeRoutine(AudioSource audioSource,
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

        internal static DynamicTask FadeTask(AudioSource audioSource,
                                             float duration,
                                             float targetVolume,
                                             Ease ease = Ease.Linear,
                                             Func<bool> waitWhilePredicate = default,
                                             CancellationToken cancellationToken = default)
        {
            return FadeTask(
                audioSource,
                duration,
                targetVolume,
                EasingFunctions.GetEasingFunction(ease),
                waitWhilePredicate,
                cancellationToken);
        }

        private static async DynamicTask FadeTask(AudioSource audioSource,
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
                deltaTime += Time.deltaTime;
                audioSource.volume = Mathf.Lerp(startVolume, targetVolume, easeFunction(deltaTime / duration));

                if (waitWhilePredicate != default)
                {
#if UNITASK_INCLUDED
                    await UniTask.WaitWhile(waitWhilePredicate, cancellationToken: cancellationToken);
#else
                    await TaskHelper.WaitWhile(waitWhilePredicate, cancellationToken);
#endif
                }
                else
                {
#if UNITASK_INCLUDED
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: cancellationToken);
#else
                    await Task.Yield();
#endif
                }
            }

            audioSource.volume = targetVolume;
        }

        #endregion

        #region Cross-Fade

        /// <inheritdoc cref="CrossFade_Entity"/>
        public static SomaEntity CrossFade(float duration,
                                           SomaEntity fadeOutEntity,
                                           SomaProperties fadeInProperties,
                                           Transform followTarget = null,
                                           Vector3 fadeInPosition = default,
                                           Action onComplete = null)
        {
            return Instance.CrossFade_Entity(
                duration,
                fadeOutEntity,
                fadeInProperties,
                followTarget,
                fadeInPosition,
                onComplete);
        }

        /// <inheritdoc cref="CrossFade_Source"/>
        public static SomaEntity CrossFade(float duration,
                                           AudioSource fadeOutAudioSource,
                                           AudioSource fadeInAudioSource,
                                           Transform followTarget = null,
                                           Vector3 fadeInPosition = default,
                                           Action onComplete = null)
        {
            return Instance.CrossFade_Source(
                duration,
                fadeOutAudioSource,
                fadeInAudioSource,
                followTarget,
                fadeInPosition,
                onComplete);
        }

        /// <inheritdoc cref="CrossFadeAsync_Entity_Outed"/>
        public static DynamicTask CrossFadeAsync(out SomaEntity entity,
                                                 float duration,
                                                 SomaEntity fadeOutEntity,
                                                 SomaProperties fadeInProperties,
                                                 Transform followTarget = null,
                                                 Vector3 fadeInPosition = default,
                                                 CancellationToken cancellationToken = default)
        {
            return Instance.CrossFadeAsync_Entity_Outed(
                out entity,
                duration,
                fadeOutEntity,
                fadeInProperties,
                followTarget,
                fadeInPosition,
                cancellationToken);
        }

        /// <inheritdoc cref="CrossFadeAsync_Entity"/>
        public static DynamicTask CrossFadeAsync(float duration,
                                                 SomaEntity fadeOutEntity,
                                                 SomaProperties fadeInProperties,
                                                 Transform followTarget = null,
                                                 Vector3 fadeInPosition = default,
                                                 CancellationToken cancellationToken = default)
        {
            return Instance.CrossFadeAsync_Entity(
                duration,
                fadeOutEntity,
                fadeInProperties,
                followTarget,
                fadeInPosition,
                cancellationToken);
        }

        /// <inheritdoc cref="CrossFadeAsync_Source_Outed"/>
        public static DynamicTask CrossFadeAsync(out SomaEntity entity,
                                                 float duration,
                                                 AudioSource fadeOutAudioSource,
                                                 AudioSource fadeInAudioSource,
                                                 Transform followTarget = null,
                                                 Vector3 fadeInPosition = default,
                                                 CancellationToken cancellationToken = default)
        {
            return Instance.CrossFadeAsync_Source_Outed(
                out entity,
                duration,
                fadeOutAudioSource,
                fadeInAudioSource,
                followTarget,
                fadeInPosition,
                cancellationToken);
        }

        /// <inheritdoc cref="CrossFadeAsync_Source"/>
        public static DynamicTask CrossFadeAsync(float duration,
                                                 AudioSource fadeOutAudioSource,
                                                 AudioSource fadeInAudioSource,
                                                 Transform followTarget = null,
                                                 Vector3 fadeInPosition = default,
                                                 CancellationToken cancellationToken = default)
        {
            return Instance.CrossFadeAsync_Source(
                duration,
                fadeOutAudioSource,
                fadeInAudioSource,
                followTarget,
                fadeInPosition,
                cancellationToken);
        }

        /// <summary>
        /// Linearly cross-fades a playing sound that is managed by Soma and a new sound. The fading out sound will be stopped at the end.
        /// </summary>
        /// <param name="duration">The duration in seconds the cross-fade will prolong.</param>
        /// <param name="fadeOutEntity">The entity that will fade out and stop.</param>
        /// <param name="fadeInProperties">The properties that define the newly played sound.</param>
        /// <param name="followTarget">Optional target the new sound will follow while playing.</param>
        /// <param name="fadeInPosition">Either the global position or, when following, the position offset at which the new sound is played.</param>
        /// <param name="onComplete">Optional callback once sounds complete cross-fading.</param>
        /// <returns>The new <see cref="SomaEntity"/> fading in.</returns>
        /// <remarks>Simultaneously call Stop and Play methods for finer cross-fading control instead.</remarks>
        private SomaEntity CrossFade_Entity(float duration,
                                            SomaEntity fadeOutEntity,
                                            SomaProperties fadeInProperties,
                                            Transform followTarget = null,
                                            Vector3 fadeInPosition = default,
                                            Action onComplete = null)
        {
            if (fadeOutEntity == null) return null;

            Stop_Entity(fadeOutEntity, fadeOutDuration: duration);

            return Play_Properties(
                fadeInProperties,
                followTarget,
                fadeInPosition,
                true,
                duration,
                onComplete: onComplete);
        }

        /// <summary>
        /// Linearly cross-fades a playing sound that is managed by Soma and a new sound. The fading out sound will be stopped at the end.
        /// </summary>
        /// <param name="duration">The duration in seconds the cross-fade will prolong.</param>
        /// <param name="fadeOutAudioSource">The source that will fade out and stop.</param>
        /// <param name="fadeInAudioSource">The source that defines the newly played sound.</param>
        /// <param name="followTarget">Optional target the new sound will follow while playing.</param>
        /// <param name="fadeInPosition">Either the global position or, when following, the position offset at which the new sound is played.</param>
        /// <param name="onComplete">Optional callback once sounds complete cross-fading.</param>
        /// <returns>The new <see cref="SomaEntity"/> fading in.</returns>
        /// <remarks>Simultaneously call Stop and Play methods for finer cross-fading control instead.</remarks>
        private SomaEntity CrossFade_Source(float duration,
                                            AudioSource fadeOutAudioSource,
                                            AudioSource fadeInAudioSource,
                                            Transform followTarget = null,
                                            Vector3 fadeInPosition = default,
                                            Action onComplete = null)
        {
            if (fadeOutAudioSource == null) return null;

            Stop_Source(fadeOutAudioSource, fadeOutDuration: duration);

            return Play_Source(fadeInAudioSource, followTarget, fadeInPosition, true, duration, onComplete: onComplete);
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
        private DynamicTask CrossFadeAsync_Entity_Outed(out SomaEntity entity,
                                                        float duration,
                                                        SomaEntity fadeOutEntity,
                                                        SomaProperties fadeInProperties,
                                                        Transform followTarget = null,
                                                        Vector3 fadeInPosition = default,
                                                        CancellationToken cancellationToken = default)
        {
            if (fadeOutEntity == null)
            {
                entity = null;
                return default;
            }

            _ = StopAsync_Entity(fadeOutEntity, fadeOutDuration: duration, cancellationToken: cancellationToken);

            return PlayAsync_Properties_Outed(
                out entity,
                fadeInProperties,
                followTarget,
                fadeInPosition,
                true,
                duration,
                cancellationToken: cancellationToken);
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
        private DynamicTask CrossFadeAsync_Entity(float duration,
                                                  SomaEntity fadeOutEntity,
                                                  SomaProperties fadeInProperties,
                                                  Transform followTarget = null,
                                                  Vector3 fadeInPosition = default,
                                                  CancellationToken cancellationToken = default)
        {
            if (fadeOutEntity == null) return default;

            _ = StopAsync_Entity(fadeOutEntity, fadeOutDuration: duration, cancellationToken: cancellationToken);

            return PlayAsync_Properties_Outed(
                out _,
                fadeInProperties,
                followTarget,
                fadeInPosition,
                true,
                duration,
                cancellationToken: cancellationToken);
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
        private DynamicTask CrossFadeAsync_Source_Outed(out SomaEntity entity,
                                                        float duration,
                                                        AudioSource fadeOutAudioSource,
                                                        AudioSource fadeInAudioSource,
                                                        Transform followTarget = null,
                                                        Vector3 fadeInPosition = default,
                                                        CancellationToken cancellationToken = default)
        {
            if (fadeOutAudioSource == null)
            {
                entity = null;
                return default;
            }

            _ = StopAsync_Source(fadeOutAudioSource, fadeOutDuration: duration, cancellationToken: cancellationToken);

            return PlayAsync_Source_Outed(
                out entity,
                fadeInAudioSource,
                followTarget,
                fadeInPosition,
                true,
                duration,
                cancellationToken: cancellationToken);
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
        private DynamicTask CrossFadeAsync_Source(float duration,
                                                  AudioSource fadeOutAudioSource,
                                                  AudioSource fadeInAudioSource,
                                                  Transform followTarget = null,
                                                  Vector3 fadeInPosition = default,
                                                  CancellationToken cancellationToken = default)
        {
            if (fadeOutAudioSource == null) return default;

            _ = StopAsync_Source(fadeOutAudioSource, fadeOutDuration: duration, cancellationToken: cancellationToken);

            return PlayAsync_Source_Outed(
                out _,
                fadeInAudioSource,
                followTarget,
                fadeInPosition,
                true,
                duration,
                cancellationToken: cancellationToken);
        }

        #endregion

        #region Management

        private void LateUpdate()
        {
            if (!_setup) return;
            
            foreach ((SomaEntity entity, AudioSource _) in _entitiesPlaying) entity.ProcessTargetFollowing();
            foreach ((SomaEntity entity, AudioSource _) in _entitiesStopping) entity.ProcessTargetFollowing();
        }

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

        /// <inheritdoc cref="RegisterMixerVolumeGroup_Internal"/>
        public static void RegisterMixerVolumeGroup(SomaVolumeMixerGroup group)
        {
            Instance.RegisterMixerVolumeGroup_Internal(group);
        }

        /// <inheritdoc cref="UnregisterMixerVolumeGroup_Internal"/>
        public static void UnregisterMixerVolumeGroup(SomaVolumeMixerGroup group)
        {
            Instance.UnregisterMixerVolumeGroup_Internal(group);
        }

        /// <inheritdoc cref="SetMixerGroupVolume_Internal"/>
        public static bool SetMixerGroupVolume(string exposedParameter, float value)
        {
            return Instance.SetMixerGroupVolume_Internal(exposedParameter, value);
        }

        /// <inheritdoc cref="IncreaseMixerGroupVolume_Internal"/>
        public static bool IncreaseMixerGroupVolume(string exposedParameter)
        {
            return Instance.IncreaseMixerGroupVolume_Internal(exposedParameter);
        }

        /// <inheritdoc cref="DecreaseMixerGroupVolume_Internal"/>
        public static bool DecreaseMixerGroupVolume(string exposedParameter)
        {
            return Instance.DecreaseMixerGroupVolume_Internal(exposedParameter);
        }

        /// <inheritdoc cref="MuteMixerGroupVolume_Internal"/>
        public static bool MuteMixerGroupVolume(string exposedParameter, bool value)
        {
            return Instance.MuteMixerGroupVolume_Internal(exposedParameter, value);
        }

        /// <inheritdoc cref="FadeMixerGroupVolume_Internal"/>
        public static bool FadeMixerGroupVolume(string exposedParameter,
                                                float targetVolume,
                                                float duration,
                                                Ease ease = Ease.Linear,
                                                Action onComplete = null)
        {
            return Instance.FadeMixerGroupVolume_Internal(exposedParameter, targetVolume, duration, ease, onComplete);
        }

        /// <inheritdoc cref="FadeMixerGroupVolumeAsync_Internal"/>
        public static DynamicTask FadeMixerGroupVolumeAsync(string exposedParameter,
                                                            float targetVolume,
                                                            float duration,
                                                            Ease ease = Ease.Linear,
                                                            CancellationToken cancellationToken = default)
        {
            return Instance.FadeMixerGroupVolumeAsync_Internal(
                exposedParameter,
                targetVolume,
                duration,
                ease,
                cancellationToken);
        }

        /// <inheritdoc cref="CrossFadeMixerGroupVolumes_Internal"/>
        public static bool CrossFadeMixerGroupVolumes(string fadeOutExposedParameter,
                                                      string fadeInExposedParameter,
                                                      float duration,
                                                      Action onComplete = null)
        {
            return Instance.CrossFadeMixerGroupVolumes_Internal(
                fadeOutExposedParameter,
                fadeInExposedParameter,
                duration,
                onComplete);
        }

        /// <inheritdoc cref="CrossFadeMixerGroupVolumesAsync_Internal"/>
        public static DynamicTask CrossFadeMixerGroupVolumesAsync(string fadeOutExposedParameter,
                                                                  string fadeInExposedParameter,
                                                                  float duration,
                                                                  CancellationToken cancellationToken = default)
        {
            return Instance.CrossFadeMixerGroupVolumesAsync_Internal(
                fadeOutExposedParameter,
                fadeInExposedParameter,
                duration,
                cancellationToken);
        }

        /// <summary>
        /// Registers a <see cref="SomaVolumeMixerGroup"/> in the internal dictionary.
        /// </summary>
        /// <param name="group">The group to be registered.</param>
        /// <remarks>Once registered, grants access through various methods like <see cref="SetMixerGroupVolume"/> or <see cref="FadeMixerGroupVolume"/>.</remarks>
        private void RegisterMixerVolumeGroup_Internal(SomaVolumeMixerGroup group)
        {
            if (group == null) return;

            SetupIfNeeded();

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

        /// <summary>
        /// Unregisters a <see cref="SomaVolumeMixerGroup"/> from the internal dictionary.
        /// </summary>
        /// <param name="group">The group to be unregistered.</param>
        private void UnregisterMixerVolumeGroup_Internal(SomaVolumeMixerGroup group)
        {
            if (group == null) return;

            SetupIfNeeded();

            _mixerVolumeGroups.Remove(group.ExposedParameter);
        }

        /// <summary>
        /// Sets the volume for an Audio Mixer Group.
        /// </summary>
        /// <param name="exposedParameter">The exposed parameter with which to access the group, e.g. 'VolumeMusic'.</param>
        /// <param name="value">The volumes' new value.</param>
        /// <returns>True, if group with exposedParameter is registered.</returns>
        /// <remarks>Changing a volume stops any ongoing volume fades applied in the mixer.</remarks>
        private bool SetMixerGroupVolume_Internal(string exposedParameter, float value)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out SomaVolumeMixerGroup mixerVolumeGroup)) return false;

            StopMixerFading(exposedParameter);

            mixerVolumeGroup.Set(value);

            return true;
        }

        /// <summary>
        /// Increases the volume of an Audio Mixer Group incrementally.
        /// </summary>
        /// <param name="exposedParameter">The exposed parameter with which to access the group, e.g. 'VolumeMusic'.</param>
        /// <returns>True, if group with exposedParameter is registered.</returns>
        /// <remarks>Has no effect if no Volume Segments are defined in the <see cref="SomaVolumeMixerGroup"/>.</remarks>
        private bool IncreaseMixerGroupVolume_Internal(string exposedParameter)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out SomaVolumeMixerGroup mixerVolumeGroup)) return false;

            StopMixerFading(exposedParameter);

            mixerVolumeGroup.Increase();

            return true;
        }

        /// <summary>
        /// Decreases the volume of an Audio Mixer Group incrementally.
        /// </summary>
        /// <param name="exposedParameter">The exposed parameter with which to access the group, e.g. 'VolumeMusic'.</param>
        /// <returns>True, if group with exposedParameter is registered.</returns>
        /// <remarks>Has no effect if no Volume Segments are defined in the <see cref="SomaVolumeMixerGroup"/>.</remarks>
        private bool DecreaseMixerGroupVolume_Internal(string exposedParameter)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out SomaVolumeMixerGroup mixerVolumeGroup)) return false;

            StopMixerFading(exposedParameter);

            mixerVolumeGroup.Decrease();

            return true;
        }

        /// <summary>
        /// Mutes/Un-mutes the volume of an Audio Mixer Group by setting the volume to 0 or reapplying the previously stored value.
        /// </summary>
        /// <param name="exposedParameter">The exposed parameter with which to access the group, e.g. 'VolumeMusic'.</param>
        /// <param name="value">True = muted, False = unmuted.</param>
        /// <returns>True, if group with exposedParameter is registered.</returns>
        private bool MuteMixerGroupVolume_Internal(string exposedParameter, bool value)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out SomaVolumeMixerGroup mixerVolumeGroup)) return false;

            StopMixerFading(exposedParameter);

            mixerVolumeGroup.Mute(value);

            return true;
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
        private bool FadeMixerGroupVolume_Internal(string exposedParameter,
                                                   float targetVolume,
                                                   float duration,
                                                   Ease ease = Ease.Linear,
                                                   Action onComplete = null)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out SomaVolumeMixerGroup mixerVolumeGroup)) return false;

            StopMixerFading(exposedParameter);

#if UNITASK_INCLUDED
            CancellationTokenSource cts = new();
            _mixerFadeCancellationTokenSources.TryAdd(exposedParameter, cts);

            DoFadeTask(cts.Token).Forget();

            return true;

            async UniTaskVoid DoFadeTask(CancellationToken cancellationToken)
            {
                await FadeMixerTask(mixerVolumeGroup, duration, targetVolume, ease, cancellationToken);

                onComplete?.Invoke();

                _mixerFadeCancellationTokenSources.Remove(exposedParameter);
            }
#else
            _mixerFadeRoutines.TryAdd(exposedParameter, StartCoroutine(DoFadeRoutine()));

            return true;

            IEnumerator DoFadeRoutine()
            {
                yield return FadeMixerRoutine(mixerVolumeGroup, duration, targetVolume, ease);

                onComplete?.Invoke();

                _mixerFadeRoutines.Remove(exposedParameter);
            }
#endif
        }

        /// <summary>
        /// Asynchronously fades the volume of an Audio Mixer Group.
        /// </summary>
        /// <param name="exposedParameter">The exposed parameter with which to access the group, e.g. 'VolumeMusic'.</param>
        /// <param name="targetVolume">The target volume reached at the end of the fade.</param>
        /// <param name="duration">The duration in seconds the fade will prolong.</param>
        /// <param name="ease">The easing applied when fading.</param>
        /// <param name="cancellationToken">Optional token for cancelling the fading.</param>
        private async DynamicTask FadeMixerGroupVolumeAsync_Internal(string exposedParameter,
                                                                     float targetVolume,
                                                                     float duration,
                                                                     Ease ease = Ease.Linear,
                                                                     CancellationToken cancellationToken = default)
        {
            if (!MixerVolumeGroupRegistered(exposedParameter, out SomaVolumeMixerGroup mixerVolumeGroup)) return;

            StopMixerFading(exposedParameter);

            CancellationTokenSource cts = new();
            TaskHelper.Link(ref cancellationToken, ref cts);

            _mixerFadeCancellationTokenSources.TryAdd(exposedParameter, cts);

            await FadeMixerTask(mixerVolumeGroup, duration, targetVolume, ease, cancellationToken);

            _mixerFadeCancellationTokenSources.Remove(exposedParameter);
        }

        /// <summary>
        /// Linearly cross-fades the volume of two Audio Mixer Groups.
        /// </summary>
        /// <param name="fadeOutExposedParameter">The exposed parameter with which to access the group fading out, e.g. 'VolumeSFX'.</param>
        /// <param name="fadeInExposedParameter">The exposed parameter with which to access the group fading in, e.g. 'VolumeMusic'.</param>
        /// <param name="duration">The duration in seconds the cross-fade will prolong.</param>
        /// <param name="onComplete">Optional callback once mixer completes cross-fading.</param>
        /// <returns>True, if both fadeOutExposedParameter and fadeInExposedParameter are registered.</returns>
        private bool CrossFadeMixerGroupVolumes_Internal(string fadeOutExposedParameter,
                                                         string fadeInExposedParameter,
                                                         float duration,
                                                         Action onComplete = null)
        {
            bool bothRegistered = MixerVolumeGroupRegistered(fadeOutExposedParameter, out SomaVolumeMixerGroup _) &&
                                  MixerVolumeGroupRegistered(fadeInExposedParameter, out SomaVolumeMixerGroup _);

            if (!bothRegistered) return false;

            FadeMixerGroupVolume(fadeOutExposedParameter, 0, duration);
            FadeMixerGroupVolume(fadeInExposedParameter, 1, duration, onComplete: onComplete);

            return true;
        }

        /// <summary>
        /// Asynchronously and linearly cross-fades the volume of two Audio Mixer Groups.
        /// </summary>
        /// <param name="fadeOutExposedParameter">The exposed parameter with which to access the group fading out, e.g. 'VolumeSFX'.</param>
        /// <param name="fadeInExposedParameter">The exposed parameter with which to access the group fading in, e.g. 'VolumeMusic'.</param>
        /// <param name="duration">The duration in seconds the cross-fade will prolong.</param>
        /// <param name="cancellationToken">Optional token for cancelling the fading.</param>
        private DynamicTask CrossFadeMixerGroupVolumesAsync_Internal(string fadeOutExposedParameter,
                                                                     string fadeInExposedParameter,
                                                                     float duration,
                                                                     CancellationToken cancellationToken = default)
        {
            _ = FadeMixerGroupVolumeAsync(fadeOutExposedParameter, 0, duration, cancellationToken: cancellationToken);

            return FadeMixerGroupVolumeAsync(fadeInExposedParameter, 1, duration, cancellationToken: cancellationToken);
        }

#if !UNITASK_INCLUDED
        private static IEnumerator FadeMixerRoutine(SomaVolumeMixerGroup mixerVolumeGroup,
                                                    float duration,
                                                    float targetVolume,
                                                    Ease ease)
        {
            return FadeMixerRoutine(mixerVolumeGroup, duration, targetVolume, EasingFunctions.GetEasingFunction(ease));
        }

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

        private static DynamicTask FadeMixerTask(SomaVolumeMixerGroup somaVolumeMixerGroup,
                                                 float duration,
                                                 float targetVolume,
                                                 Ease ease,
                                                 CancellationToken cancellationToken = default)
        {
            return FadeMixerTask(
                somaVolumeMixerGroup,
                duration,
                targetVolume,
                EasingFunctions.GetEasingFunction(ease),
                cancellationToken);
        }

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
                deltaTime += Time.deltaTime;
                somaVolumeMixerGroup.Set(Mathf.Lerp(startVolume, targetVolume, easeFunction(deltaTime / duration)));

#if UNITASK_INCLUDED
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: cancellationToken);
#else
                await Task.Yield();
#endif
            }

            somaVolumeMixerGroup.Set(targetVolume);
        }

        private bool MixerVolumeGroupRegistered(string exposedParameter, out SomaVolumeMixerGroup somaVolumeMixerGroup)
        {
            SetupIfNeeded();

            if (_mixerVolumeGroups.TryGetValue(exposedParameter, out somaVolumeMixerGroup)) return true;

            Debug.LogError($"There is no {nameof(SomaVolumeMixerGroup)} for {exposedParameter} registered.");
            return false;
        }

        private void StopMixerFading(string exposedParameter)
        {
            SetupIfNeeded();

#if !UNITASK_INCLUDED
            if (_mixerFadeRoutines.Remove(exposedParameter, out Coroutine fadeRoutine))
            {
                StopCoroutine(fadeRoutine);
            }
#endif

            if (_mixerFadeCancellationTokenSources.Remove(exposedParameter, out CancellationTokenSource cts))
            {
                TaskHelper.Cancel(ref cts);
            }
        }

        #endregion
    }
}