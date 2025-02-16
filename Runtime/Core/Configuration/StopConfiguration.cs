using System;
using UnityEngine;
using UnityEngine.Events;

namespace Devolfer.Soma
{
    [Serializable]
    public class StopConfiguration
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
}