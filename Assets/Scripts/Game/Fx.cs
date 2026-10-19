using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Tuntum
{
    /// <summary>Celebrations on a layer above everything: confetti, slam-in banners, suit bursts, ribbons, the red flash.</summary>
    public sealed class Fx : MonoBehaviour
    {
        RectTransform layer;
        Image flash;
        RectTransform banner;
        Text bannerBig, bannerSub;
        RectTransform bannerSubChip;

        struct Bit { public RectTransform rt; public Vector2 v; public float spin, life, max; public Image img; public Text txt; }
        readonly List<Bit> bits = new List<Bit>();
        readonly Stack<RectTransform> pool = new Stack<RectTransform>();

        public static Fx Create(RectTransform parent)
        {
            var rt = UI.Rect(parent, "Fx").Stretch();
            var fx = rt.gameObject.AddComponent<Fx>();
            fx.layer = rt;
            fx.flash = UI.Img(rt, "Flash", Theme.Red.A(0));
            fx.flash.rectTransform.Stretch(-200, -200, -200, -200);

            fx.banner = UI.Rect(rt, "Banner");
            fx.banner.Place(new Vector2(.5f, .62f), Vector2.zero, new Vector2(460, 160));
            fx.bannerBig = UI.Label(fx.banner, "", Theme.Display, 66, Theme.Paper).Stroked(3, 6);
            fx.bannerBig.rectTransform.Stretch(0, 0, 0, 50);
            fx.bannerBig.horizontalOverflow = HorizontalWrapMode.Overflow;
            fx.bannerSubChip = UI.Chunk(fx.banner, "Sub", Theme.Paper, 18, 3, 3);
            fx.bannerSubChip.Place(new Vector2(.5f, 0), new Vector2(0, 22), new Vector2(200, 38));
            fx.bannerSub = UI.Label(fx.bannerSubChip, "", Theme.Body, 17, Theme.Ink);
            fx.bannerSub.fontStyle = FontStyle.Bold;
            fx.bannerSub.rectTransform.Stretch();
            fx.banner.gameObject.AddComponent<CanvasGroup>();
            fx.banner.gameObject.SetActive(false);
            return fx;
        }

        public IEnumerator Flash()
        {
            yield return Tween.Run(.42f, t => flash.color = Theme.Red.A(.35f * Ease.Hump(t)), Ease.Linear);
        }

        /// <summary>Big title slams in tilted, wobbles, holds, then floats off.</summary>
        public IEnumerator Say(string big, Color color, string sub, float hold)
        {
            banner.gameObject.SetActive(true);
            bannerBig.text = big; bannerBig.color = color;
            bannerSub.text = sub;
            bannerSubChip.sizeDelta = new Vector2(Mathf.Max(120, bannerSub.preferredWidth + 34), 38);
            var cg = banner.GetComponent<CanvasGroup>();
            yield return Tween.Run(.28f, t =>
            {
                float s = Mathf.LerpUnclamped(3f, 1f, Ease.OutBack(t));
                banner.localScale = Vector3.one * s;
                banner.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(14, 5, t));
                cg.alpha = Mathf.Clamp01(t * 2);
            }, Ease.Linear);
            yield return Tween.Wait(hold);
            yield return Tween.Run(.25f, t =>
            {
                banner.localScale = Vector3.one * (1 + .4f * t);
                banner.anchoredPosition = new Vector2(0, 30 * t);
                cg.alpha = 1 - t;
            }, Ease.InCubic);
            banner.anchoredPosition = Vector2.zero;
            banner.gameObject.SetActive(false);
        }

        /// <summary>Confetti from a point (in this layer's space): ink-edged paper bits and little suit symbols.</summary>
        public void Confetti(Vector2 at, int count)
        {
            if (Tween.Speed > 1.5f) return; // reduced motion
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2, sp = 240 + Random.value * 540;
                var color = Theme.Confetti[i % Theme.Confetti.Length];
                bool suit = Random.value < .25f;
                var rt = Spawn(suit);
                var bit = new Bit { rt = rt, v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp + Vector2.up * 360, spin = Random.Range(-400f, 400f), life = 0, max = 1.5f + Random.value * .8f };
                if (suit)
                {
                    bit.txt = rt.GetComponent<Text>();
                    bit.txt.text = color == Theme.Red ? "♥" : "♠";
                    bit.txt.color = color == Theme.Red ? Theme.Red : Theme.Ink;
                }
                else
                {
                    bit.img = rt.GetChild(1).GetComponent<Image>();
                    bit.img.color = color;
                    rt.sizeDelta = new Vector2(6 + Random.value * 8, 4 + Random.value * 6);
                }
                rt.anchoredPosition = at;
                rt.localRotation = Quaternion.Euler(0, 0, Random.value * 360);
                bits.Add(bit);
            }
        }

        RectTransform Spawn(bool suit)
        {
            RectTransform rt;
            if (suit)
            {
                var t = UI.Label(layer, "", Theme.Suits, 18, Theme.Ink);
                rt = t.rectTransform; rt.sizeDelta = new Vector2(24, 24);
            }
            else
            {
                rt = UI.Rect(layer, "Bit");
                UI.Img(rt, "Ink", Theme.Ink).rectTransform.Stretch(-1.5f, -1.5f, -1.5f, -1.5f);
                UI.Img(rt, "Fill", Color.white).rectTransform.Stretch();
            }
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            return rt;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = bits.Count - 1; i >= 0; i--)
            {
                var b = bits[i];
                b.life += dt;
                b.v.y -= 1000 * dt;
                b.v.x *= Mathf.Pow(.4f, dt);
                b.rt.anchoredPosition += b.v * dt;
                b.rt.localRotation = Quaternion.Euler(0, 0, b.rt.localEulerAngles.z + b.spin * dt);
                float alpha = Mathf.Clamp01((b.max - b.life) / .5f);
                if (b.img) b.img.color = b.img.color.A(alpha);
                if (b.txt) b.txt.color = b.txt.color.A(alpha);
                bits[i] = b;
                if (b.life >= b.max) { Destroy(b.rt.gameObject); bits.RemoveAt(i); }
            }
        }

        /// <summary>The small 3-in-a-row party: suit symbols and stars pop out of a point and drift down.</summary>
        public void Burst(Vector2 at)
        {
            string[] sym = { "♥", "♠", "♦", "♣", "★" };
            Color[] col = { Theme.Red, Theme.Ink, Theme.Red, Theme.Ink, Theme.SunDeep };
            for (int k = 0; k < 14; k++)
            {
                int n = k % 5;
                var t = UI.Label(layer, sym[n], Theme.Suits, 24, col[n]);
                if (n == 4) t.Stroked(1.2f);
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(.5f, .5f);
                t.rectTransform.sizeDelta = new Vector2(30, 30);
                float a = k / 14f * Mathf.PI * 2 + Random.value * .4f, d = 50 + Random.value * 50;
                StartCoroutine(BurstOne(t.rectTransform, at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d, .9f + Random.value * .3f));
            }
            Confetti(at, 36);
        }

        IEnumerator BurstOne(RectTransform rt, Vector2 at, Vector2 dir, float dur)
        {
            var txt = rt.GetComponent<Text>();
            yield return Tween.Run(dur, t =>
            {
                float out1 = Ease.OutCubic(Mathf.Clamp01(t / .6f));
                rt.anchoredPosition = at + dir * (out1 + .2f * t) + Vector2.down * 30 * t * t;
                rt.localScale = Vector3.one * (t < .6f ? Mathf.Lerp(.3f, 1.2f, out1) : Mathf.Lerp(1.2f, .8f, (t - .6f) / .4f));
                rt.localRotation = Quaternion.Euler(0, 0, dir.x * 3 * t);
                txt.color = txt.color.A(t < .6f ? 1 : 1 - (t - .6f) / .4f);
            }, Ease.Linear);
            Destroy(rt.gameObject);
        }

        /// <summary>A green "+1" (or any text) that floats up from a point and fades.</summary>
        public IEnumerator Float(Vector2 at, string text)
        {
            var t = UI.Label(layer, text, Theme.Display, 28, Theme.Mint).Stroked(2);
            var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f); rt.sizeDelta = new Vector2(80, 40);
            yield return Tween.Run(.9f, k =>
            {
                rt.anchoredPosition = at + new Vector2(0, Mathf.Lerp(-10, 44, Ease.OutCubic(k)));
                rt.localScale = Vector3.one * (k < .35f ? Mathf.Lerp(.4f, 1.2f, k / .35f) : Mathf.Lerp(1.2f, 1f, (k - .35f) / .65f));
                t.color = Theme.Mint.A(k < .35f ? k / .35f : 1 - (k - .35f) / .65f);
            }, Ease.Linear);
            Destroy(rt.gameObject);
        }

        /// <summary>Green ribbon that pops under a player's box: "3 IN A ROW!".</summary>
        public IEnumerator Ribbon(RectTransform under, string text)
        {
            var rib = UI.Chunk(under, "Ribbon", Theme.Mint, 15, 3, 3);
            var label = UI.Label(rib, text, Theme.Display, 16, Theme.Ink);
            label.rectTransform.Stretch();
            rib.Place(new Vector2(.5f, 0), new Vector2(0, -16), new Vector2(label.preferredWidth + 28, 30));
            yield return Tween.Run(2f, k =>
            {
                float s = k < .2f ? Ease.OutBack(k / .2f) * 1.1f : k < .3f ? Mathf.Lerp(1.1f, 1f, (k - .2f) / .1f) : k < .85f ? 1 : Mathf.Lerp(1, 0, (k - .85f) / .15f);
                rib.localScale = Vector3.one * Mathf.Max(0, s);
                rib.localRotation = Quaternion.Euler(0, 0, k < .2f ? Mathf.Lerp(-10, 3, k / .2f) : -2);
            }, Ease.Linear);
            Destroy(rib.gameObject);
        }

        public Vector2 Local(RectTransform target) => UI.CenterIn(target, layer);
    }
}
