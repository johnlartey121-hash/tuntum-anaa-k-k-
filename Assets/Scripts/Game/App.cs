using System.Collections;
using System.Collections.Generic;
using Tuntum.Core;
using Tuntum.Net;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tuntum
{
    /// <summary>
    /// The whole game. Builds every screen in code (Home, Online, Game, End) and runs the table:
    /// deal → guess → flip → fly to the score box (right) or shake and toss to the misses (wrong).
    /// </summary>
    public sealed partial class App : MonoBehaviour
    {
        enum Mode { Computer, PassAndPlay, Online }

        struct Pending { public int Index; public CardColor Called; public Card? Card; }

        // ---- boot ---------------------------------------------------------------------------
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindObjectOfType<App>() != null) return;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            var go = new GameObject("Tuntum");
            DontDestroyOnLoad(go);
            go.AddComponent<App>();
        }

        Canvas canvas;
        RectTransform safe, home, online, game, end, sheet;
        Fx fx;
        OnlineClient net;

        void Awake()
        {
            Theme.Load();
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.transform.SetParent(transform);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Theme.Sun;
            cam.cullingMask = 0;
            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(transform);
            }
            var cgo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cgo.transform.SetParent(transform);
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(430, 932);
            scaler.matchWidthOrHeight = 1;
            Sfx.Create(gameObject);
            net = gameObject.AddComponent<OnlineClient>();
            net.OnMessage += OnNet;
            net.OnDisconnected += OnNetLost;
        }

        IEnumerator Start()
        {
            var root = (RectTransform)canvas.transform;
            BuildBackground(root);
            // phones wider than tall (tablets in landscape, desktop browsers) get a centred phone-shaped column
            var column = UI.Rect(root, "Column");
            column.anchorMin = new Vector2(.5f, 0); column.anchorMax = new Vector2(.5f, 1);
            column.sizeDelta = new Vector2(460, 0);
            safe = UI.Rect(column, "Safe area").Stretch();
            yield return null; // let the canvas size itself
            ApplySafeArea();
            Canvas.ForceUpdateCanvases();
            fx = Fx.Create(root);
            BuildHome();
            BuildOnline();
            BuildGame();
            BuildEnd();
            BuildSheet();
            fx.transform.SetAsLastSibling();
            Show(home);
            StartCoroutine(Idle());
        }

        Rect lastSafe;
        void ApplySafeArea()
        {
            var sa = Screen.safeArea;
            lastSafe = sa;
            var rootRect = ((RectTransform)canvas.transform).rect;
            float k = rootRect.height / Mathf.Max(1, Screen.height);
            float top = (Screen.height - sa.yMax) * k, bottom = sa.yMin * k;
            safe.Stretch(16, top + 10, 16, bottom + 10);
            var column = (RectTransform)safe.parent;
            column.sizeDelta = new Vector2(Mathf.Min(460, rootRect.width), 0);
        }

        void Update()
        {
            if (safe != null && Screen.safeArea != lastSafe) ApplySafeArea();
            if (game != null && game.gameObject.activeSelf && match != null && Input.GetKeyDown(KeyCode.LeftArrow)) Pick(CardColor.Black);
            if (game != null && game.gameObject.activeSelf && match != null && Input.GetKeyDown(KeyCode.RightArrow)) Pick(CardColor.Red);
        }

        void BuildBackground(RectTransform root)
        {
            var bg = UI.Rect(root, "Background").Stretch();
            UI.Img(bg, "Sun", Theme.Sun).rectTransform.Stretch();
            var c1 = UI.Img(bg, "Disc 1", Theme.SunDeep, Sprites.Circle);
            c1.rectTransform.anchorMin = c1.rectTransform.anchorMax = new Vector2(1, .62f);
            c1.rectTransform.sizeDelta = Vector2.one * 760; c1.rectTransform.anchoredPosition = new Vector2(80, 0);
            var c2 = UI.Img(bg, "Disc 2", Theme.SunPale, Sprites.Circle);
            c2.rectTransform.anchorMin = c2.rectTransform.anchorMax = new Vector2(0, .2f);
            c2.rectTransform.sizeDelta = Vector2.one * 480; c2.rectTransform.anchoredPosition = new Vector2(-60, 0);
        }

        void Show(RectTransform screen)
        {
            foreach (var s in new[] { home, online, game, end }) if (s) s.gameObject.SetActive(s == screen);
            if (screen != null) StartCoroutine(Tween.Run(.3f, t => screen.localScale = Vector3.one * Mathf.LerpUnclamped(.92f, 1, Ease.OutBack(t))));
        }

        // ---- small builders shared by screens ----------------------------------------------
        RectTransform RoundButton(Transform parent, string glyph, Font font, UnityEngine.Events.UnityAction onClick)
        {
            var b = UI.Button(parent, "", Theme.Paper, Theme.Ink, new Vector2(46, 46), onClick, 18, 23);
            var t = UI.Label(b.Face, glyph, font, 20, Theme.Ink);
            t.fontStyle = FontStyle.Bold;
            t.rectTransform.Stretch();
            b.Root.Find("Shadow").GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -3);
            return b.Root;
        }

        static Text Chip(Transform parent, string text, out RectTransform chip, Color? fill = null)
        {
            chip = UI.Chunk(parent, "Chip", fill ?? Theme.Paper, 17, 3, 0);
            var t = UI.Label(chip, text, Theme.Body, 15, Theme.Ink);
            t.fontStyle = FontStyle.Bold;
            t.rectTransform.Stretch(12, 0, 12, 0);
            return t;
        }

        static InputField Field(Transform parent, string value, int maxLength, int fontSize, Font font)
        {
            var box = UI.Chunk(parent, "Field", Theme.Paper, 14, 3, 0);
            var text = UI.Label(box, value, font, fontSize, Theme.Ink, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            text.rectTransform.Stretch(12, 2, 12, 2);
            var ph = UI.Label(box, "", font, fontSize, Theme.Ink.A(.35f), TextAnchor.MiddleLeft);
            ph.rectTransform.Stretch(12, 2, 12, 2);
            var f = box.gameObject.AddComponent<InputField>();
            f.textComponent = text; f.placeholder = ph; f.characterLimit = maxLength; f.text = value;
            f.targetGraphic = UI.FillOf(box);
            UI.FillOf(box).raycastTarget = true;
            f.shouldHideMobileInput = false;
            return f;
        }

        // ---- idle animation on the home logo ------------------------------------------------
        readonly List<RectTransform> logoCards = new List<RectTransform>();
        IEnumerator Idle()
        {
            float t = 0;
            while (true)
            {
                t += Time.unscaledDeltaTime;
                if (home.gameObject.activeSelf && logoCards.Count == 3)
                {
                    float w = Mathf.Sin(t * 2.1f);
                    logoCards[0].anchoredPosition = new Vector2(-70 - 8 * (w * .5f + .5f), 0);
                    logoCards[0].localRotation = Quaternion.Euler(0, 0, 16 + 6 * (w * .5f + .5f));
                    logoCards[1].anchoredPosition = new Vector2(70 + 8 * (w * .5f + .5f), 0);
                    logoCards[1].localRotation = Quaternion.Euler(0, 0, -16 - 6 * (w * .5f + .5f));
                    logoCards[2].anchoredPosition = new Vector2(0, 12 * (w * .5f + .5f));
                    logoCards[2].localRotation = Quaternion.Euler(0, 0, -2 + 5 * w * .5f);
                }
                yield return null;
            }
        }
    }
}
