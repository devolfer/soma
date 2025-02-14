using UnityEngine;
using UnityEngine.Audio;

namespace Devolfer.Soma
{
    internal static class AudioMixerExtensions
    {
        internal static bool TrySetVolume(this AudioMixer mixer, string exposedParameter, ref float value)
        {
            value = Mathf.Clamp01(value);
            float decibel = value != 0 ? Mathf.Log10(value) * 20 : -80;

            return mixer.SetFloat(exposedParameter, decibel);
        }

        internal static bool TryGetVolume(this AudioMixer mixer, string exposedParameter, out float value)
        {
            value = 0;

            if (!mixer.GetFloat(exposedParameter, out float decibel)) return false;

            value = decibel > -80 ? Mathf.Pow(10, decibel / 20) : 0;

            return true;
        }

        internal static bool HasParameter(this AudioMixer mixer, string exposedParameter)
        {
            return mixer.GetFloat(exposedParameter, out float _);
        }
    }
}