using System;
using UnityEngine;
using UnityEngine.Events;

namespace Devolfer.Soma
{
    [Serializable]
    public class FadeConfiguration
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