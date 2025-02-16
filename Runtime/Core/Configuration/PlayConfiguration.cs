using System;
using UnityEngine;
using UnityEngine.Events;

namespace Devolfer.Soma
{
    [Serializable]
    public class PlayConfiguration
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
}