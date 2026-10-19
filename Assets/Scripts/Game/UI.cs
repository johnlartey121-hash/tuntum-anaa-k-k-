using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Tuntum
{
    /// <summary>The Pop Party look: sunny yellow, thick ink outlines, chunky drop shadows.</summary>
    public static class Theme
    {
        public static readonly Color Sun = Hex(0xffd84d), SunDeep = Hex(0xffc21a), SunPale = Hex(0xffe78a), Cream = Hex(0xfff8d6);
        public static readonly Color Ink = Hex(0x111111), Paper = Color.white, Red = Hex(0xff5a5f), RedDeep = Hex(0xd93a40);
        public static readonly Color Blue = Hex(0x3d7bff), Mint = Hex(0x3ddc97), Brown = Hex(0x6b5a1a);
        public static readonly Color[] Confetti = { Red, Blue, Ink, Paper, Mint, SunDeep };

        public static Font Display, Body, Suits;

        public static void Load()
        {
            Display = Resources.Load<Font>("Fonts/LilitaOne");
            Body = Resources.Load<Font>("Fonts/Figtree");
            Suits = Resources.Load<Font>("Fonts/Suits");
            var fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!Display) Display = fallback;
            if (!Body) Body = fallback;
            if (!Suits) Suits = fallback;
        }

        public static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
        public static Color A(this Color c, float a) { c.a = a; return c; }
        public static Color PlayerColor(int i) => i == 0 ? Red : Blue;
    }

    /// <summary>Procedural sprites so the project needs no image files.</summary>
    public static class Sprites
    {
        const int Size = 128, R = 64;
        static Sprite round, ring4, dot, glow, star, circle;

        /// <summary>A white rounded square, 9-sliced. Set Image.pixelsPerUnitMultiplier = 64 / radius.</summary>
        public static Sprite Round => round ??= Make(Size, (x, y) => RoundAlpha(x, y, Size, R), new Vector4(R, R, R, R));
        public static Sprite Circle => circle ??= Make(Size, (x, y) => RoundAlpha(x, y, Size, R), Vector4.zero);
        public static Sprite Dot => dot ??= Make(16, (x, y) => Mathf.Clamp01(3.5f - Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(8, 8))), Vector4.zero);
        public static Sprite Glow => glow ??= Make(128, (x, y) => { float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(64, 64)) / 64f; return Mathf.Clamp01(1 - d) * Mathf.Clamp01(1 - d); }, Vector4.zero);
        public static Sprite Star => star ??= Make(64, StarAlpha, Vector4.zero);

        /// <summary>Thin spokes for the "on fire" rays behind the card.</summary>
        public static Sprite Rays(int spokes) => Make(256, (x, y) =>
        {
            var v = new Vector2(x + .5f - 128, y + .5f - 128);
            float a = Mathf.Atan2(v.y, v.x) / (2 * Mathf.PI) * spokes;
            float spoke = Mathf.Repeat(a, 1f) < .42f ? 1 : 0;
            float fade = Mathf.Clamp01(1 - v.magnitude / 128f);
            return spoke * fade;
        }, Vector4.zero);

        static float RoundAlpha(int x, int y, int size, float r)
        {
            float px = x + .5f, py = y + .5f;
            float cx = Mathf.Clamp(px, r, size - r), cy = Mathf.Clamp(py, r, size - r);
            float d = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
            return Mathf.Clamp01(r - d + .5f);
        }

        static float StarAlpha(int x, int y)
        {
            var v = new Vector2(x + .5f - 32, y + .5f - 32);
            float ang = Mathf.Atan2(v.y, v.x) + Mathf.PI / 2;
            float k = Mathf.Cos(ang * 5) * .5f + .5f;
            float rad = Mathf.Lerp(13, 30, Mathf.Pow(k, 3));
            return Mathf.Clamp01(rad - v.magnitude + .5f);
        }

        static Sprite Make(int size, Func<int, int, float> alpha, Vector4 border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(x, y)) * 255));
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, border);
        }
    }

    /// <summary>Builders for every piece of UI. Units match CSS pixels on a 430-wide phone.</summary>
    public static class UI
    {
        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float l = 0, float t = 0, float r = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        /// <summary>Place by anchor point (0..1 of parent) with a pixel offset and size.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static Image Img(Transform parent, string name, Color c, Sprite s = null, float radius = 0)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            if (s != null) img.sprite = s;
            if (radius > 0) { img.sprite = Sprites.Round; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 64f / radius; }
            return img;
        }

        /// <summary>The house block: ink drop shadow, ink outline, coloured fill. Returns the fill's parent (content goes inside).</summary>
        public static RectTransform Chunk(Transform parent, string name, Color fill, float radius, float border = 3, float shadow = 4, bool shadowDown = false)
        {
            var root = Rect(parent, name);
            if (shadow > 0)
            {
                var sh = Img(root, "Shadow", Theme.Ink, null, radius).rectTransform.Stretch();
                sh.anchoredPosition = shadowDown ? new Vector2(0, -shadow) : new Vector2(shadow, -shadow);
            }
            Img(root, "Outline", Theme.Ink, null, radius).rectTransform.Stretch();
            var f = Img(root, "Fill", fill, null, Mathf.Max(1, radius - border));
            f.rectTransform.Stretch(border, border, border, border);
            return root;
        }

        public static Image FillOf(RectTransform chunk) => chunk.Find("Fill").GetComponent<Image>();

        public static Text Label(Transform parent, string text, Font font, int size, Color c, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var rt = Rect(parent, "Text");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.color = c; t.alignment = anchor; t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = false;
            return t;
        }

        /// <summary>Thick ink outline + offset shadow, like the CSS -webkit-text-stroke look.</summary>
        public static Text Stroked(this Text t, float stroke = 2.5f, float drop = 0)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = Theme.Ink; o.effectDistance = new Vector2(stroke, -stroke);
            var o2 = t.gameObject.AddComponent<Outline>();
            o2.effectColor = Theme.Ink; o2.effectDistance = new Vector2(-stroke, stroke);
            if (drop > 0) { var s = t.gameObject.AddComponent<Shadow>(); s.effectColor = Theme.Ink; s.effectDistance = new Vector2(drop, -drop); }
            return t;
        }

        public sealed class ChunkButton
        {
            public RectTransform Root;
            public Button Button;
            public Text Text;
            public RectTransform Face;
            public bool Interactable { get => Button.interactable; set { Button.interactable = value; Face.GetComponent<CanvasGroup>().alpha = value ? 1 : .55f; } }
        }

        /// <summary>Chunky push button: the face sinks onto its shadow when pressed.</summary>
        public static ChunkButton Button(Transform parent, string text, Color fill, Color textColor, Vector2 size, UnityAction onClick, int fontSize = 19, float radius = 22)
        {
            var root = Rect(parent, "Button " + text);
            root.sizeDelta = size;
            var sh = Img(root, "Shadow", Theme.Ink, null, radius).rectTransform.Stretch();
            sh.anchoredPosition = new Vector2(0, -6);
            var face = Rect(root, "Face").Stretch();
            face.gameObject.AddComponent<CanvasGroup>();
            var hit = Img(face, "Outline", Theme.Ink, null, radius);
            hit.raycastTarget = true;
            hit.rectTransform.Stretch();
            Img(face, "Fill", fill, null, radius - 4).rectTransform.Stretch(4, 4, 4, 4);
            var label = Label(face, text, Theme.Body, fontSize, textColor);
            label.fontStyle = FontStyle.Bold;
            label.rectTransform.Stretch(10, 4, 10, 4);
            var b = root.gameObject.AddComponent<Button>();
            b.targetGraphic = hit;
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { Sfx.Play(Sfx.Click); onClick?.Invoke(); });
            var press = root.gameObject.AddComponent<PressSink>();
            press.Face = face;
            return new ChunkButton { Root = root, Button = b, Text = label, Face = face };
        }

        public static RectTransform VStack(Transform parent, string name, float spacing, TextAnchor align = TextAnchor.UpperCenter)
        {
            var rt = Rect(parent, name);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.childAlignment = align;
            v.childControlWidth = true; v.childControlHeight = false; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            return rt;
        }

        public static LayoutElement Layout(this RectTransform rt, float height, float width = -1)
        {
            var le = rt.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height; le.minHeight = height;
            if (width > 0) le.preferredWidth = width;
            return le;
        }

        public static Vector2 CenterIn(RectTransform target, RectTransform space)
        {
            // where target's centre sits in space's local coordinates
            var world = target.TransformPoint(target.rect.center);
            return space.InverseTransformPoint(world);
        }
    }

    /// <summary>Press feedback for ChunkButton: the face moves down 5 px while held.</summary>
    public sealed class PressSink : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        public RectTransform Face;
        Button b;
        void Awake() => b = GetComponent<Button>();
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) { if (b.interactable) Face.anchoredPosition = new Vector2(0, -5); }
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) => Face.anchoredPosition = Vector2.zero;
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) => Face.anchoredPosition = Vector2.zero;
        public void Tap() { StartCoroutine(TapRoutine()); }
        System.Collections.IEnumerator TapRoutine() { Face.anchoredPosition = new Vector2(0, -5); yield return Tween.Wait(.14f); Face.anchoredPosition = Vector2.zero; }
    }
}
