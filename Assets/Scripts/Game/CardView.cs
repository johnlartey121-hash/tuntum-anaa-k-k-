using System.Collections;
using Tuntum.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Tuntum
{
    /// <summary>A playing card: blue polka-dot back with a big "?", white face with corners and a centre pip.</summary>
    public sealed class CardView : MonoBehaviour
    {
        public const float Aspect = 1.42f;
        public RectTransform Rt { get; private set; }
        RectTransform back, front, body;
        Text[] corners = new Text[2];
        Text pip, courtSuit;
        public bool FaceUp { get; private set; }

        public static CardView Create(Transform parent, float height, string name = "Card")
        {
            var rt = UI.Rect(parent, name);
            rt.sizeDelta = new Vector2(height / Aspect, height);
            var cv = rt.gameObject.AddComponent<CardView>();
            cv.Build(height);
            return cv;
        }

        void Build(float h)
        {
            Rt = (RectTransform)transform;
            float r = h * .075f, bw = Mathf.Max(3, h * .016f);
            body = UI.Rect(Rt, "Body").Stretch();
            // drop shadow that shows while the card is lifted
            var sh = UI.Img(body, "Lift shadow", Theme.Ink.A(.9f), null, r).rectTransform.Stretch();
            sh.anchoredPosition = new Vector2(8, -10);
            sh.gameObject.SetActive(false);
            sh.name = "LiftShadow";

            back = UI.Chunk(body, "Back", Theme.Blue, r, bw, 0).Stretch();
            var dots = UI.Img(UI.FillOf(back).transform, "Dots", Theme.Paper.A(.35f), Sprites.Dot);
            dots.type = Image.Type.Tiled; dots.pixelsPerUnitMultiplier = 1;
            dots.rectTransform.Stretch(bw * 2, bw * 2, bw * 2, bw * 2);
            var disc = UI.Chunk(UI.FillOf(back).transform, "Disc", Theme.Paper, h * .2f, bw, 0);
            disc.Place(new Vector2(.5f, .5f), Vector2.zero, Vector2.one * h * .4f);
            UI.Label(disc, "?", Theme.Display, Mathf.RoundToInt(h * .26f), Theme.Ink).rectTransform.Stretch(0, 0, 0, h * .02f);

            front = UI.Chunk(body, "Front", Theme.Paper, r, bw, 0).Stretch();
            for (int i = 0; i < 2; i++)
            {
                var c = UI.Label(front, "", Theme.Suits, Mathf.RoundToInt(h * .1f), Theme.Ink, TextAnchor.MiddleCenter);
                c.fontStyle = FontStyle.Bold;
                c.lineSpacing = .85f;
                c.rectTransform.Place(i == 0 ? new Vector2(0, 1) : new Vector2(1, 0), i == 0 ? new Vector2(h * .12f, -h * .17f) : new Vector2(-h * .12f, h * .17f), new Vector2(h * .2f, h * .3f));
                if (i == 1) c.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
                corners[i] = c;
            }
            pip = UI.Label(front, "", Theme.Suits, Mathf.RoundToInt(h * .36f), Theme.Ink);
            pip.rectTransform.Stretch();
            courtSuit = UI.Label(front, "", Theme.Suits, Mathf.RoundToInt(h * .13f), Theme.Ink);
            courtSuit.rectTransform.Stretch(0, h * .5f, 0, 0);
            SetFaceUp(false);
        }

        public void Show(Card c)
        {
            var col = c.IsRed ? Theme.RedDeep : Theme.Ink;
            foreach (var t in corners) { t.text = c.RankText + "\n" + c.SuitSymbol; t.color = col; }
            if (c.IsCourt)
            {
                pip.font = Theme.Display; pip.text = c.RankText; pip.fontSize = Mathf.RoundToInt(Rt.rect.height * .4f);
                courtSuit.text = c.SuitSymbol;
            }
            else
            {
                pip.font = Theme.Suits; pip.text = c.SuitSymbol; pip.fontSize = Mathf.RoundToInt(Rt.rect.height * .36f);
                courtSuit.text = "";
            }
            pip.color = courtSuit.color = col;
        }

        public void SetFaceUp(bool up)
        {
            FaceUp = up;
            back.gameObject.SetActive(!up);
            front.gameObject.SetActive(up);
        }

        public void SetLifted(bool on) => body.Find("LiftShadow").gameObject.SetActive(on);

        /// <summary>Lift, turn over (the face swaps at the half-way squash), overshoot a little, land.</summary>
        public IEnumerator Flip(Card c)
        {
            Show(c);
            Sfx.Play(Sfx.Flip);
            SetLifted(true);
            yield return Tween.Run(.28f, t =>
            {
                body.localScale = new Vector3(Mathf.Lerp(1, 0, t) * (1 + .12f * t), 1 + .12f * t, 1);
                body.anchoredPosition = new Vector2(0, 34 * t);
            }, Ease.InCubic);
            SetFaceUp(true);
            yield return Tween.Run(.34f, t =>
            {
                body.localScale = new Vector3(Mathf.LerpUnclamped(0, 1, Ease.OutBack(t)) * (1.12f - .12f * t), 1.12f - .12f * t, 1);
                body.anchoredPosition = new Vector2(0, 34 * (1 - t));
            }, Ease.Linear);
            body.localScale = Vector3.one;
            body.anchoredPosition = Vector2.zero;
        }
    }
}
