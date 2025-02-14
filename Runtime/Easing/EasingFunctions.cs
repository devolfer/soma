using System;
using UnityEngine;

namespace Devolfer.Soma
{
    internal static class EasingFunctions
    {
        internal static Func<float, float> GetEasingFunction(Ease ease)
        {
            return ease switch
            {
                Ease.Linear     => Linear,
                Ease.InSine     => InSine,
                Ease.OutSine    => OutSine,
                Ease.InOutSine  => InOutSine,
                Ease.InQuad     => InQuad,
                Ease.OutQuad    => OutQuad,
                Ease.InOutQuad  => InOutQuad,
                Ease.InCubic    => InCubic,
                Ease.OutCubic   => OutCubic,
                Ease.InOutCubic => InOutCubic,
                Ease.InQuart    => InQuart,
                Ease.OutQuart   => OutQuart,
                Ease.InOutQuart => InOutQuart,
                Ease.InQuint    => InQuint,
                Ease.OutQuint   => OutQuint,
                Ease.InOutQuint => InOutQuint,
                Ease.InCirc     => InCirc,
                Ease.OutCirc    => OutCirc,
                Ease.InOutCirc  => InOutCirc,
                Ease.InExpo     => InExpo,
                Ease.OutExpo    => OutExpo,
                Ease.InOutExpo  => InOutExpo,
                _               => Linear
            };
        }

        private static float Linear(float t) => t;

        private static float InSine(float t) => 1 - Mathf.Cos(t * Mathf.PI * .5f);
        private static float OutSine(float t) => Mathf.Sin(t * Mathf.PI * .5f);
        private static float InOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1) * .5f;

        private static float InQuad(float t) => t * t;
        private static float OutQuad(float t) => t * (2 - t);
        private static float InOutQuad(float t) => t < .5f ? 2 * t * t : -1 + (4 - 2 * t) * t;

        private static float InCubic(float t) => t * t * t;
        private static float OutCubic(float t) => --t * t * t + 1;
        private static float InOutCubic(float t) => t < .5f ? 4 * t * t * t : (t - 1) * (2 * t - 2) * (2 * t - 2) + 1;

        private static float InQuart(float t) => t * t * t * t;
        private static float OutQuart(float t) => 1 - --t * t * t * t;
        private static float InOutQuart(float t) => t < .5f ? 8 * t * t * t * t : 1 - 8 * --t * t * t * t;

        private static float InQuint(float t) => t * t * t * t * t;
        private static float OutQuint(float t) => 1 + --t * t * t * t * t;
        private static float InOutQuint(float t) => t < .5f ? 16 * t * t * t * t * t : 1 + 16 * --t * t * t * t * t;

        private static float InCirc(float t) => 1 - Mathf.Sqrt(1 - Mathf.Pow(t, 2));
        private static float OutCirc(float t) => Mathf.Sqrt(1 - Mathf.Pow(t - 1, 2));
        private static float InOutCirc(float t)
        {
            return t < .5f ?
                (1 - Mathf.Sqrt(1 - Mathf.Pow(2 * t, 2))) * .5f :
                (Mathf.Sqrt(1 - Mathf.Pow(-2 * t + 2, 2)) + 1) * .5f;
        }

        private static float InExpo(float t) => t == 0 ? 0 : Mathf.Pow(2, 10 * (t - 1));
        private static float OutExpo(float t) => Mathf.Approximately(t, 1) ? 1 : 1 - Mathf.Pow(2, -10 * t);
        private static float InOutExpo(float t)
        {
            return t == 0                 ? 0 :
                Mathf.Approximately(t, 1) ? 1 :
                t < .5f                   ? Mathf.Pow(2, 20 * t - 10) * .5f : (2 - Mathf.Pow(2, -20 * t + 10)) * .5f;
        }
    }
}