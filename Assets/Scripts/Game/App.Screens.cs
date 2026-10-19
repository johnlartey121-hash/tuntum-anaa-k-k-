using System.Collections;
using Tuntum.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Tuntum
{
    public sealed partial class App
    {
        InputField nameField, name2Field, codeField;
        Text onlineNote, roomCode;
        RectTransform hostPanel, waitPanel, joinPanel;
        Text endCrown, endTitle, endStats, againLabel;
        RectTransform finals;
        Text sheetAvatar, sheetTitle, sheetSub;
        Image sheetAvatarFill;
        UI.ChunkButton sheetButton;
        System.Action sheetAction;

        string PlayerName => Clean(nameField.text, "Player 1");
        string Player2Name => Clean(name2Field.text, "Player 2");
        static string Clean(string s, string fallback)
        {
            s = (s ?? "").Trim();
            if (s.Length > 10) s = s.Substring(0, 10);
            return s.Length == 0 ? fallback : s;
        }

        // ---- Home ------------------------------------------------------------------------------
        void BuildHome()
        {
            home = UI.Rect(safe, "Home").Stretch();
            float H = safe.rect.height;
            bool roomy = H >= 860, tiny = H < 760; // shorter phones drop the rules card and shrink the logo
            var logo = UI.Rect(home, "Logo");
            logo.Place(new Vector2(.5f, 1), new Vector2(0, tiny ? -80 : -105), new Vector2(280, 170));
            if (tiny) logo.localScale = Vector3.one * .75f;
            var faces = new[] { new Card(Suit.Spades, 12), new Card(Suit.Hearts, 0), default };
            for (int i = 0; i < 3; i++)
            {
                var cv = CardView.Create(logo, 150, "Logo card " + i);
                cv.Rt.anchorMin = cv.Rt.anchorMax = new Vector2(.5f, .5f);
                if (i < 2) { cv.Show(faces[i]); cv.SetFaceUp(true); }
                logoCards.Add(cv.Rt);
            }

            var title = UI.Rect(home, "Title");
            title.Place(new Vector2(.5f, 1), new Vector2(0, tiny ? -250 : -300), new Vector2(400, 170));
            UI.Label(title, "Tuntum", Theme.Display, 58, Theme.Ink).rectTransform.Place(new Vector2(.5f, 1), new Vector2(0, -30), new Vector2(400, 64));
            var anaa = UI.Label(title, "A  N  A  A", Theme.Body, 15, Theme.Ink);
            anaa.fontStyle = FontStyle.Bold;
            anaa.rectTransform.Place(new Vector2(.5f, 1), new Vector2(0, -74), new Vector2(400, 20));
            UI.Label(title, "Kɔkɔɔ", Theme.Display, 58, Theme.Red).Stroked(2, 4).rectTransform.Place(new Vector2(.5f, 1), new Vector2(0, -114), new Vector2(400, 64));
            var tag = UI.Label(title, "Black or red? Call the next card.", Theme.Body, 15, Theme.Ink.A(.75f));
            tag.fontStyle = FontStyle.Bold;
            tag.rectTransform.Place(new Vector2(.5f, 1), new Vector2(0, -160), new Vector2(400, 24));

            var stack = UI.VStack(home, "Menu", 12);
            stack.anchorMin = new Vector2(0, 0); stack.anchorMax = new Vector2(1, 0); stack.pivot = new Vector2(.5f, 0);
            stack.anchoredPosition = new Vector2(0, 8); stack.sizeDelta = new Vector2(0, roomy ? 430 : 314);
            stack.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.LowerCenter;

            var names = UI.Rect(stack, "Names"); names.Layout(70);
            var savedName = PlayerPrefs.GetString("name", "Ama");
            nameField = NameBox(names, 0, "PLAYER 1", savedName);
            name2Field = NameBox(names, 1, "PLAYER 2", PlayerPrefs.GetString("name2", "Kofi"));

            UI.Button(stack, "Play vs Computer", Theme.Red, Theme.Paper, new Vector2(0, 62), () => StartLocal(Mode.Computer)).Root.Layout(62);
            UI.Button(stack, "Play Online", Theme.Blue, Theme.Paper, new Vector2(0, 62), OpenOnline).Root.Layout(62);
            UI.Button(stack, "Pass & Play", Theme.Paper, Theme.Ink, new Vector2(0, 62), () => StartLocal(Mode.PassAndPlay)).Root.Layout(62);

            var rules = UI.Chunk(stack, "Rules", Theme.Paper, 20, 3, 4);
            rules.Layout(104);
            var rt = UI.Label(rules, "How to play. Guess if the face-down card is Tuntum (black ♠♣) or Kɔkɔɔ (red ♥♦). Right: +1 and go again. Wrong: your turn ends. Most points when all 52 cards are gone wins.",
                Theme.Suits, 13, Theme.Ink, TextAnchor.MiddleLeft);
            rt.rectTransform.Stretch(16, 8, 16, 10);
            rt.lineSpacing = 1.1f;
            rules.gameObject.SetActive(roomy);

            MuteButton(home).Place(new Vector2(1, 1), new Vector2(-24, -24), new Vector2(46, 46));
        }

        InputField NameBox(RectTransform row, int col, string label, string value)
        {
            var cell = UI.Rect(row, label);
            cell.anchorMin = new Vector2(col * .5f, 0); cell.anchorMax = new Vector2(col * .5f + .5f, 1);
            cell.offsetMin = new Vector2(col == 0 ? 0 : 5, 0); cell.offsetMax = new Vector2(col == 0 ? -5 : 0, 0);
            var l = UI.Label(cell, label, Theme.Body, 12, Theme.Ink, TextAnchor.UpperLeft);
            l.fontStyle = FontStyle.Bold;
            l.rectTransform.Stretch(2, 0, 0, 52);
            var f = Field(cell, value, 10, 16, Theme.Body);
            ((RectTransform)f.transform).Stretch(0, 20, 0, 0);
            f.textComponent.fontStyle = FontStyle.Bold;
            return f;
        }

        readonly System.Collections.Generic.List<Text> muteLabels = new System.Collections.Generic.List<Text>();

        RectTransform MuteButton(Transform parent)
        {
            var b = RoundButton(parent, Sfx.Muted ? "\u2715" : "\u266A", Theme.Suits, ToggleMute);
            muteLabels.Add(b.GetComponentsInChildren<Text>()[1]);
            return b;
        }

        void ToggleMute()
        {
            Sfx.Muted = !Sfx.Muted;
            if (!Sfx.Muted) Sfx.Play(Sfx.Click);
            foreach (var t in muteLabels) t.text = Sfx.Muted ? "\u2715" : "\u266A";
        }

        // ---- Online ---------------------------------------------------------------------------
        void BuildOnline()
        {
            online = UI.Rect(safe, "Online").Stretch();
            RoundButton(online, "✕", Theme.Suits, GoHome).Place(new Vector2(0, 1), new Vector2(24, -24), new Vector2(46, 46));
            UI.Label(online, "Play Online", Theme.Display, 44, Theme.Ink).rectTransform.Place(new Vector2(.5f, 1), new Vector2(0, -100), new Vector2(400, 56));

            hostPanel = Panel(online, "START A ROOM", -150, 200);
            var p = UI.Label(hostPanel, "You get a 4-letter code. Your friend opens the game, taps Play Online and types it.", Theme.Body, 14, Theme.Ink.A(.8f), TextAnchor.UpperLeft);
            p.rectTransform.Stretch(18, 46, 18, 80);
            PinBottom(UI.Button(hostPanel, "Create room", Theme.Red, Theme.Paper, new Vector2(0, 58), CreateRoom).Root, 18, 58);

            waitPanel = Panel(online, "YOUR ROOM CODE", -150, 250);
            var codeBox = UI.Chunk(waitPanel, "Code", Theme.Cream, 20, 3, 0);
            codeBox.Place(new Vector2(.5f, 1), new Vector2(0, -96), new Vector2(330, 90));
            roomCode = UI.Label(codeBox, "····", Theme.Display, 60, Theme.Ink);
            roomCode.rectTransform.Stretch();
            var copy = UI.Button(waitPanel, "Copy code", Theme.Paper, Theme.Ink, new Vector2(0, 52), CopyCode);
            PinBottom(copy.Root, 52, 52);
            var waiting = UI.Label(waitPanel, "Waiting for your friend…", Theme.Body, 15, Theme.Ink);
            waiting.fontStyle = FontStyle.Bold;
            waiting.rectTransform.Place(new Vector2(.5f, 0), new Vector2(0, 22), new Vector2(330, 24));
            waitPanel.gameObject.SetActive(false);

            joinPanel = Panel(online, "JOIN A FRIEND", -370, 140);
            codeField = Field(joinPanel, "", 4, 28, Theme.Display);
            codeField.characterValidation = InputField.CharacterValidation.Alphanumeric;
            codeField.onValidateInput += (text, index, ch) => char.ToUpperInvariant(ch);
            codeField.textComponent.alignment = TextAnchor.MiddleCenter;
            ((Text)codeField.placeholder).text = "CODE";
            ((Text)codeField.placeholder).alignment = TextAnchor.MiddleCenter;
            var cf = (RectTransform)codeField.transform;
            cf.anchorMin = new Vector2(0, 0); cf.anchorMax = new Vector2(1, 0); cf.pivot = new Vector2(0, 0);
            cf.offsetMin = new Vector2(18, 22); cf.offsetMax = new Vector2(-130, 80);
            var join = UI.Button(joinPanel, "Join", Theme.Blue, Theme.Paper, new Vector2(100, 58), JoinRoom);
            join.Root.anchorMin = join.Root.anchorMax = new Vector2(1, 0); join.Root.pivot = new Vector2(1, 0);
            join.Root.anchoredPosition = new Vector2(-18, 22);

            onlineNote = UI.Label(online, "", Theme.Body, 14, Theme.Ink);
            onlineNote.fontStyle = FontStyle.Bold;
            onlineNote.rectTransform.Place(new Vector2(.5f, 1), new Vector2(0, -545), new Vector2(390, 60));
        }

        static void PinBottom(RectTransform rt, float bottomGap, float height)
        {
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(.5f, 0);
            rt.offsetMin = new Vector2(18, bottomGap); rt.offsetMax = new Vector2(-18, bottomGap + height);
        }

        RectTransform Panel(RectTransform parent, string heading, float top, float height)
        {
            var p = UI.Chunk(parent, heading, Theme.Paper, 24, 4, 5);
            p.anchorMin = new Vector2(0, 1); p.anchorMax = new Vector2(1, 1); p.pivot = new Vector2(.5f, 1);
            p.offsetMin = new Vector2(2, top - height); p.offsetMax = new Vector2(-7, top);
            var h = UI.Label(p, heading, Theme.Body, 13, Theme.Ink, TextAnchor.UpperLeft);
            h.fontStyle = FontStyle.Bold;
            h.rectTransform.Stretch(20, 20, 20, 0);
            return p;
        }

        void OnlineNote(string text, bool error = false)
        {
            onlineNote.text = text;
            onlineNote.color = error ? Theme.RedDeep : Theme.Ink;
        }

        void CopyCode()
        {
            GUIUtility.systemCopyBuffer = roomCode.text;
            OnlineNote("Code copied. Send it to your friend.");
        }

        // ---- End --------------------------------------------------------------------------------
        void BuildEnd()
        {
            end = UI.Rect(safe, "End").Stretch();
            endCrown = UI.Label(end, "♛", Theme.Suits, 64, Theme.Ink);
            endCrown.rectTransform.Place(new Vector2(.5f, 1), new Vector2(0, -80), new Vector2(200, 80));
            endTitle = UI.Label(end, "", Theme.Display, 48, Theme.Ink);
            endTitle.rectTransform.Place(new Vector2(.5f, 1), new Vector2(0, -160), new Vector2(400, 60));
            finals = UI.Rect(end, "Finals");
            finals.Place(new Vector2(.5f, 1), new Vector2(0, -290), new Vector2(390, 170));
            var stats = UI.Chunk(end, "Stats", Theme.Paper, 20, 3, 0);
            stats.Place(new Vector2(.5f, 1), new Vector2(0, -470), new Vector2(390, 130));
            endStats = UI.Label(stats, "", Theme.Suits, 14, Theme.Ink, TextAnchor.MiddleLeft);
            endStats.lineSpacing = 1.6f;
            endStats.rectTransform.Stretch(18, 8, 18, 8);
            var again = UI.Button(end, "Play again", Theme.Red, Theme.Paper, new Vector2(390, 62), PlayAgain);
            again.Root.Place(new Vector2(.5f, 0), new Vector2(0, 110), new Vector2(390, 62));
            againLabel = again.Text;
            UI.Button(end, "Home", Theme.Paper, Theme.Ink, new Vector2(390, 62), GoHome).Root.Place(new Vector2(.5f, 0), new Vector2(0, 34), new Vector2(390, 62));
        }

        void ShowEnd()
        {
            var p = match.Players;
            int w = match.Winner, meIdx = mode == Mode.Computer ? 0 : mode == Mode.Online ? me : -1;
            endCrown.text = w < 0 ? "⚖" : "♛";
            endTitle.text = w < 0 ? "It's a draw!" : w == meIdx ? "You win!" : $"{p[w].Name} wins!";
            foreach (Transform c in finals) Destroy(c.gameObject);
            for (int k = 0; k < 2; k++)
            {
                bool win = k == w;
                var box = UI.Chunk(finals, "Final " + k, win ? Theme.Paper : Theme.Cream, 22, 4, 5);
                box.Place(new Vector2(k == 0 ? .25f : .75f, .5f), new Vector2(k == 0 ? -4 : 4, 0), new Vector2(180, 160));
                if (win) box.localRotation = Quaternion.Euler(0, 0, 2);
                var av = UI.Chunk(box, "Avatar", Theme.PlayerColor(k), 20, 3, 0);
                av.Place(new Vector2(.5f, 1), new Vector2(0, -34), new Vector2(40, 40));
                UI.Label(av, Initial(p[k].Name), Theme.Body, 17, Theme.Paper).fontStyle = FontStyle.Bold;
                av.GetChild(av.childCount - 1).GetComponent<RectTransform>().Stretch();
                var n = UI.Label(box, p[k].Name, Theme.Body, 15, Theme.Ink); n.fontStyle = FontStyle.Bold;
                n.rectTransform.Place(new Vector2(.5f, 1), new Vector2(0, -72), new Vector2(160, 22));
                UI.Label(box, p[k].Score.ToString(), Theme.Display, 52, Theme.Ink).rectTransform.Place(new Vector2(.5f, 0), new Vector2(0, 38), new Vector2(160, 60));
            }
            string best = p[0].BestStreak == p[1].BestStreak ? $"{p[0].BestStreak} · both" : p[0].BestStreak > p[1].BestStreak ? $"{p[0].BestStreak} · {p[0].Name}" : $"{p[1].BestStreak} · {p[1].Name}";
            endStats.text = $"Longest streak   {best}\nAccuracy   {p[0].Name} {p[0].Accuracy}% · {p[1].Name} {p[1].Accuracy}%\nTurns   {match.Turns}";
            againLabel.text = mode == Mode.Online ? "Ask for a rematch" : "Play again";
            Show(end);
            if (meIdx >= 0 && w >= 0 && w != meIdx) Sfx.Play(Sfx.Wrong);
            else { Sfx.Play(Sfx.Win); Sfx.Play(Sfx.Cheer(3), .9f); fx.Confetti(fx.Local(endTitle.rectTransform), 220); }
        }

        static string Initial(string name) => string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();

        // ---- Turn sheet (pass the phone / whose turn / opponent left) --------------------------
        void BuildSheet()
        {
            sheet = UI.Rect(canvas.transform, "Sheet").Stretch();
            var dim = UI.Img(sheet, "Dim", Theme.Ink.A(.45f));
            dim.raycastTarget = true;
            dim.rectTransform.Stretch();
            var box = UI.Chunk(sheet, "Box", Theme.Paper, 28, 4, 8);
            box.Place(new Vector2(.5f, .5f), Vector2.zero, new Vector2(360, 300));
            var av = UI.Chunk(box, "Avatar", Theme.Blue, 38, 4, 0);
            av.Place(new Vector2(.5f, 1), new Vector2(0, -62), new Vector2(76, 76));
            sheetAvatarFill = UI.FillOf(av);
            sheetAvatar = UI.Label(av, "K", Theme.Body, 32, Theme.Paper);
            sheetAvatar.fontStyle = FontStyle.Bold;
            sheetAvatar.rectTransform.Stretch();
            sheetTitle = UI.Label(box, "", Theme.Display, 34, Theme.Ink);
            sheetTitle.rectTransform.Place(new Vector2(.5f, 1), new Vector2(0, -136), new Vector2(330, 44));
            sheetSub = UI.Label(box, "", Theme.Body, 15, Theme.Ink.A(.75f));
            sheetSub.fontStyle = FontStyle.Bold;
            sheetSub.rectTransform.Place(new Vector2(.5f, 1), new Vector2(0, -176), new Vector2(330, 24));
            sheetButton = UI.Button(box, "Ready", Theme.Red, Theme.Paper, new Vector2(310, 58), () => sheetAction?.Invoke());
            sheetButton.Root.Place(new Vector2(.5f, 0), new Vector2(0, 46), new Vector2(310, 58));
            sheet.gameObject.SetActive(false);
        }

        IEnumerator OpenSheet(string initial, Color color, string title, string sub, string button)
        {
            sheetAvatar.text = initial; sheetAvatarFill.color = color;
            sheetTitle.text = title; sheetSub.text = sub;
            sheetButton.Root.gameObject.SetActive(button != null);
            if (button != null) sheetButton.Text.text = button;
            sheet.SetAsLastSibling();
            fx.transform.SetAsLastSibling();
            sheet.gameObject.SetActive(true);
            var box = (RectTransform)sheet.Find("Box");
            yield return Tween.Run(.38f, t =>
            {
                box.anchoredPosition = new Vector2(0, Mathf.Lerp(-60, 0, t));
                box.localScale = Vector3.one * Mathf.LerpUnclamped(.8f, 1, t);
                box.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(4, 0, t));
            }, Ease.OutBack);
        }

        IEnumerator CloseSheet()
        {
            var box = (RectTransform)sheet.Find("Box");
            yield return Tween.Run(.2f, t => { box.anchoredPosition = new Vector2(0, 40 * t); box.localScale = Vector3.one * (1 - .1f * t); }, Ease.InCubic);
            sheet.gameObject.SetActive(false);
            box.anchoredPosition = Vector2.zero; box.localScale = Vector3.one;
        }
    }
}
