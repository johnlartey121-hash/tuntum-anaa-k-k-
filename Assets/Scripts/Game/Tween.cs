using System;
using System.Collections;
using UnityEngine;

namespace Tuntum
{
    public static class Ease
    {
        public static float Linear(float t) => t;
        public static float OutCubic(float t) => 1 - Mathf.Pow(1 - t, 3);
        public static float InCubic(float t) => t * t * t;
        public static float InOutCubic(float t) => t < .5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
        public static float OutBack(float t) { const float c1 = 1.70158f, c3 = c1 + 1; return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2); }
        public static float InBack(float t) { const float c1 = 1.70158f, c3 = c1 + 1; return c3 * t * t * t - c1 * t * t; }
        public static float OutElastic(float t) => t <= 0 ? 0 : t >= 1 ? 1 : Mathf.Pow(2, -10 * t) * Mathf.Sin((t * 10 - .75f) * (2 * Mathf.PI / 3)) + 1;
        /// <summary>0 → 1 → 0, for hops and pulses.</summary>
        public static float Hump(float t) => Mathf.Sin(t * Mathf.PI);
    }

    /// <summary>Tiny coroutine tweens. Every animation in the game is one of these.</summary>
    public static class Tween
    {
        /// <summary>Respect the phone's reduce-motion wish (set from Settings).</summary>
        public static float Speed = 1f;

        public static IEnumerator Run(float seconds, Action<float> step, Func<float, float> ease = null)
        {
            ease ??= Ease.OutCubic;
            float d = Mathf.Max(0.01f, seconds / Speed), t = 0;
            while (t < d)
            {
                step(ease(t / d));
                yield return null;
                t += Time.unscaledDeltaTime;
            }
            step(ease(1));
        }

        public static IEnumerator Wait(float seconds)
        {
            float t = 0, d = seconds / Speed;
            while (t < d) { yield return null; t += Time.unscaledDeltaTime; }
        }

        public static IEnumerator Shake(RectTransform rt, float seconds, float amount)
        {
            var home = rt.anchoredPosition;
            yield return Run(seconds, t =>
            {
                float fall = 1 - t;
                rt.anchoredPosition = home + new Vector2(Mathf.Sin(t * 50) * amount * fall, 0);
                rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 50) * amount * .3f * fall);
            }, Ease.Linear);
            rt.anchoredPosition = home;
            rt.localRotation = Quaternion.identity;
        }

        public static IEnumerator Pop(Transform tr, float seconds, float to = 1.15f)
        {
            yield return Run(seconds, t => tr.localScale = Vector3.one * (1 + (to - 1) * Ease.Hump(t)), Ease.Linear);
            tr.localScale = Vector3.one;
        }
    }
}
