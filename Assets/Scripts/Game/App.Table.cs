using System.Collections;
using System.Collections.Generic;
using Tuntum.Core;
using Tuntum.Net;
using UnityEngine;
using UnityEngine.UI;

namespace Tuntum
{
    public sealed partial class App
    {
        Match match;
        Mode mode;
        int me;                       // online: which seat is this phone
        ComputerPlayer cpu;
        readonly System.Random rng = new System.Random();
        readonly Queue<Pending> queue = new Queue<Pending>();
        bool busy, running;
        int generation;               // bumps when a game is abandoned, so old coroutines stop
        float guessSentAt = -1;

        // table pieces
        float cardH;
        RectTransform table, slot, deckPile, missPile, rays, trail;
        CardView current;
        CardView missTop;
        readonly RectTransform[] panels = new RectTransform[2];
        readonly Text[] panelName = new Text[2], panelTag = new Text[2], panelScore = new Text[2], panelInitial = new Text[2];
        Text cardsLeft, streakText, ask;
        RectTransform streakChip;
        Image streakFill;
        UI.ChunkButton pickBlack, pickRed;

        // ---- build -----------------------------------------------------------------------------
        void BuildGame()
        {
            game = UI.Rect(safe, "Game").Stretch();
            float H = safe.rect.height;

            var quit = RoundButton(game, "✕", Theme.Suits, GoHome);
            quit.Place(new Vector2(0, 1), new Vector2(24, -24), new Vector2(46, 46));
            cardsLeft = Chip(game, "52 cards left", out var leftChip);
            leftChip.Place(new Vector2(.5f, 1), new Vector2(0, -24), new Vector2(150, 36));
            MuteButton(game).Place(new Vector2(1, 1), new Vector2(-24, -24), new Vector2(46, 46));

            for (int k = 0; k < 2; k++) BuildPanel(k);

            streakText = Chip(game, "0 in a row", out streakChip);
            streakChip.Place(new Vector2(.5f, 1), new Vector2(0, -150), new Vector2(150, 36));
            streakFill = UI.FillOf(streakChip);

            // guess buttons and the colour trail sit at the bottom
            trail = UI.Rect(game, "Trail");
            trail.Place(new Vector2(.5f, 0), new Vector2(0, 14), new Vector2(300, 28));
            var hl = trail.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 5; hl.childAlignment = TextAnchor.MiddleCenter; hl.childControlWidth = false; hl.childControlHeight = false; hl.childForceExpandWidth = false;

            float pickH = Mathf.Clamp(H * .14f, 92, 124);
            pickBlack = PickButton(game, "♠♣", "TUNTUM", "BLACK", Theme.Ink, 0, pickH, () => Pick(CardColor.Black));
            pickRed = PickButton(game, "♥♦", "KƆKƆƆ", "RED", Theme.Red, 1, pickH, () => Pick(CardColor.Red));

            ask = UI.Label(game, "What's next?", Theme.Display, 26, Theme.Ink);
            ask.rectTransform.Place(new Vector2(.5f, 0), new Vector2(0, 50 + pickH + 26), new Vector2(400, 36));

            // the table fills the middle
            float top = 190, bottom = 50 + pickH + 52;
            table = UI.Rect(game, "Table");
            table.anchorMin = Vector2.zero; table.anchorMax = Vector2.one;
            table.offsetMin = new Vector2(0, bottom); table.offsetMax = new Vector2(0, -top);
            float tableH = H - top - bottom;
            cardH = Mathf.Clamp(tableH * .86f, 140, 250);

            var rimg = UI.Img(table, "Rays", Theme.Paper.A(0), Sprites.Rays(16));
            rays = rimg.rectTransform;
            rays.Place(new Vector2(.5f, .5f), Vector2.zero, Vector2.one * 900);

            slot = UI.Rect(table, "Slot");
            slot.Place(new Vector2(.5f, .5f), Vector2.zero, new Vector2(cardH / CardView.Aspect, cardH));
            var ghost = UI.Img(slot, "Ghost", Theme.Ink.A(.12f), null, cardH * .075f);
            ghost.rectTransform.Stretch();

            float miniH = cardH * .5f, side = (cardH / CardView.Aspect) / 2 + (430 - 32 - cardH / CardView.Aspect) / 4;
            deckPile = UI.Rect(table, "Deck");
            deckPile.Place(new Vector2(.5f, .5f), new Vector2(-side, 0), new Vector2(miniH / CardView.Aspect, miniH));
            PileLabel(deckPile, "DECK");
            missPile = UI.Rect(table, "Misses");
            missPile.Place(new Vector2(.5f, .5f), new Vector2(side, 0), new Vector2(miniH / CardView.Aspect, miniH));
            var dash = UI.Img(missPile, "Empty", Theme.Ink.A(.15f), null, 10);
            dash.rectTransform.Stretch();
            PileLabel(missPile, "MISSES");
            game.gameObject.SetActive(false);
        }

        void PileLabel(RectTransform pile, string text)
        {
            var l = UI.Label(pile, text, Theme.Body, 12, Theme.Ink);
            l.fontStyle = FontStyle.Bold;
            l.rectTransform.Place(new Vector2(.5f, 0), new Vector2(0, -18), new Vector2(120, 20));
        }

        void BuildPanel(int k)
        {
            var p = UI.Chunk(game, "Player " + k, Theme.Cream, 18, 3, 4);
            p.anchorMin = new Vector2(k * .5f, 1); p.anchorMax = new Vector2(k * .5f + .5f, 1); p.pivot = new Vector2(.5f, 1);
            p.offsetMin = new Vector2(k == 0 ? 0 : 6, -134); p.offsetMax = new Vector2(k == 0 ? -10 : -4, -70);
            var av = UI.Chunk(p, "Avatar", Theme.PlayerColor(k), 20, 3, 0);
            av.Place(new Vector2(0, .5f), new Vector2(31, 0), new Vector2(40, 40));
            panelInitial[k] = UI.Label(av, "?", Theme.Body, 17, Theme.Paper);
            panelInitial[k].fontStyle = FontStyle.Bold;
            panelInitial[k].rectTransform.Stretch();
            panelName[k] = UI.Label(p, "", Theme.Body, 15, Theme.Ink, TextAnchor.LowerLeft);
            panelName[k].fontStyle = FontStyle.Bold;
            panelName[k].horizontalOverflow = HorizontalWrapMode.Overflow;
            panelName[k].rectTransform.Stretch(60, 8, 44, 30);
            panelTag[k] = UI.Label(p, "", Theme.Body, 10, Theme.RedDeep, TextAnchor.UpperLeft);
            panelTag[k].fontStyle = FontStyle.Bold;
            panelTag[k].rectTransform.Stretch(60, 34, 44, 6);
            panelScore[k] = UI.Label(p, "0", Theme.Display, 32, Theme.Ink, TextAnchor.MiddleRight);
            panelScore[k].rectTransform.Stretch(0, 0, 14, 0);
            panels[k] = p;
        }

        UI.ChunkButton PickButton(RectTransform parent, string suits, string word, string small, Color fill, int col, float h, UnityEngine.Events.UnityAction onClick)
        {
            var b = UI.Button(parent, "", fill, Theme.Paper, new Vector2(0, h), onClick, 19, 24);
            b.Root.anchorMin = new Vector2(col * .5f, 0); b.Root.anchorMax = new Vector2(col * .5f + .5f, 0); b.Root.pivot = new Vector2(.5f, 0);
            b.Root.offsetMin = new Vector2(col == 0 ? 0 : 7, 50); b.Root.offsetMax = new Vector2(col == 0 ? -7 : 0, 50 + h);
            var s = UI.Label(b.Face, suits, Theme.Suits, 28, Theme.Paper); s.rectTransform.Place(new Vector2(.5f, .5f), new Vector2(0, h * .2f), new Vector2(160, 34));
            var w = UI.Label(b.Face, word, Theme.Display, 25, Theme.Paper); w.rectTransform.Place(new Vector2(.5f, .5f), new Vector2(0, -h * .05f), new Vector2(180, 32));
            var sm = UI.Label(b.Face, small, Theme.Body, 11, Theme.Paper.A(.75f)); sm.fontStyle = FontStyle.Bold;
            sm.rectTransform.Place(new Vector2(.5f, .5f), new Vector2(0, -h * .27f), new Vector2(160, 16));
            return b;
        }

        // ---- start / stop ------------------------------------------------------------------------
        void StartLocal(Mode m)
        {
            PlayerPrefs.SetString("name", PlayerName);
            PlayerPrefs.SetString("name2", Player2Name);
            BeginMatch(m, Match.Local(PlayerName, m == Mode.Computer ? "Computer" : Player2Name, rng), 0);
        }

        void BeginMatch(Mode m, Match newMatch, int seat)
        {
            generation++;
            StopAllCoroutinesExceptIdle();
            mode = m; match = newMatch; me = seat;
            cpu = new ComputerPlayer(rng);
            queue.Clear(); busy = false; running = false; guessSentAt = -1;
            sheet.gameObject.SetActive(false);
            foreach (Transform c in slot) if (c.name != "Ghost") Destroy(c.gameObject);
            if (missTop) Destroy(missTop.gameObject);
            foreach (Transform c in trail) Destroy(c.gameObject);
            for (int k = 0; k < 2; k++)
            {
                panelName[k].text = match.Players[k].Name;
                panelInitial[k].text = Initial(match.Players[k].Name);
                panelScore[k].text = "0";
            }
            SetStreak(0);
            Show(game);
            RenderDeck();
            RenderTurn();
            StartCoroutine(FirstDeal(generation));
        }

        void StopAllCoroutinesExceptIdle()
        {
            StopAllCoroutines();
            StartCoroutine(Idle());
        }

        IEnumerator FirstDeal(int gen)
        {
            busy = true;
            yield return Tween.Wait(.25f);
            yield return Deal();
            if (gen != generation) yield break;
            busy = false;
            RenderTurn();
            NextAction();
        }

        void GoHome()
        {
            generation++;
            StopAllCoroutinesExceptIdle();
            match = null;
            net.Close();
            sheet.gameObject.SetActive(false);
            Show(home);
        }

        void PlayAgain()
        {
            if (mode != Mode.Online) { StartLocal(mode); return; }
            net.Send(new Msg { t = "rematch" });
            againLabel.text = "Rematch asked…";
        }

        // ---- turn state -----------------------------------------------------------------------------
        bool ComputerTurn => match != null && mode == Mode.Computer && match.Turn == 1;
        bool RemoteTurn => match != null && mode == Mode.Online && match.Turn != me;

        void RenderTurn()
        {
            if (match == null) return;
            for (int k = 0; k < 2; k++)
            {
                bool on = k == match.Turn;
                UI.FillOf(panels[k]).color = on ? Theme.Paper : Theme.Cream;
                panels[k].localRotation = Quaternion.Euler(0, 0, on ? 1 : 0);
                panelTag[k].text = on ? (ComputerTurn ? "THINKING…" : RemoteTurn ? "GUESSING…" : "YOUR GO!") : "WAITING";
                panelTag[k].color = on ? Theme.RedDeep : Theme.Brown;
            }
            bool locked = busy || ComputerTurn || RemoteTurn || match.IsOver;
            pickBlack.Interactable = !locked;
            pickRed.Interactable = !locked;
            ask.text = ComputerTurn ? "Computer is thinking…" : RemoteTurn ? $"{match.Players[match.Turn].Name} is guessing…" : "What's next?";
        }

        void RenderDeck()
        {
            foreach (Transform c in deckPile) if (c.name.StartsWith("Pile card")) Destroy(c.gameObject);
            int layers = Mathf.Min(5, Mathf.CeilToInt((match.CardsLeft - 1) / 10f));
            for (int k = layers - 1; k >= 0; k--)
            {
                var m = CardView.Create(deckPile, cardH * .5f, "Pile card " + k);
                m.Rt.anchorMin = m.Rt.anchorMax = new Vector2(.5f, .5f);
                m.Rt.anchoredPosition = new Vector2(-k * 2, k * 3);
                m.Rt.localRotation = Quaternion.Euler(0, 0, 6 - k);
                m.transform.SetSiblingIndex(0);
            }
            cardsLeft.text = match.CardsLeft == 1 ? "1 card left" : $"{match.CardsLeft} cards left";
        }

        void SetStreak(int n)
        {
            streakText.text = n == 1 ? "1 in a row" : $"{n} in a row";
            bool hot = n >= 3;
            streakFill.color = hot ? Theme.Red : Theme.Paper;
            streakText.color = hot ? Theme.Paper : Theme.Ink;
            var img = rays.GetComponent<Image>();
            img.color = Theme.Paper.A(n >= 5 ? Mathf.Min(.55f, .2f + (n - 5) * .06f) : 0);
        }

        void LateUpdate()
        {
            if (rays != null && game.gameObject.activeSelf) rays.localRotation = Quaternion.Euler(0, 0, -Time.unscaledTime * 20);
            if (streakChip != null && match != null && match.Streak >= 3)
            {
                float f = Mathf.PingPong(Time.unscaledTime * 4, 1);
                streakChip.localScale = Vector3.one * (1 + .07f * f);
                streakChip.localRotation = Quaternion.Euler(0, 0, 2 * f);
            }
            else if (streakChip != null) { streakChip.localScale = Vector3.one; streakChip.localRotation = Quaternion.identity; }
            if (mode == Mode.Online && busy && !running && guessSentAt > 0 && Time.unscaledTime - guessSentAt > 4f)
            {
                guessSentAt = -1; busy = false; RenderTurn(); // the server never answered; let the player tap again
            }
        }

        // ---- guessing --------------------------------------------------------------------------------
        void Pick(CardColor c)
        {
            if (match == null || busy || match.IsOver || ComputerTurn || RemoteTurn) return;
            (c == CardColor.Black ? pickBlack : pickRed).Root.GetComponent<PressSink>().Tap();
            if (mode == Mode.Online)
            {
                busy = true; RenderTurn();
                guessSentAt = Time.unscaledTime;
                net.Send(new Msg { t = "guess", i = match.Index, col = c == CardColor.Red ? "r" : "k" });
                return;
            }
            Enqueue(new Pending { Index = match.Index, Called = c });
        }

        void Enqueue(Pending p)
        {
            queue.Enqueue(p);
            if (!running) StartCoroutine(RunQueue(generation));
        }

        IEnumerator RunQueue(int gen)
        {
            running = true;
            while (gen == generation && queue.Count > 0)
            {
                var p = queue.Dequeue();
                if (p.Index != match.Index) continue;
                yield return Play(p, gen);
            }
            if (gen != generation) yield break;
            running = false;
            busy = false;
            RenderTurn();
            NextAction();
        }

        IEnumerator Play(Pending p, int gen)
        {
            busy = true; guessSentAt = -1;
            RenderTurn();
            var r = p.Card.HasValue ? match.Apply(p.Index, p.Called, p.Card.Value) : match.Guess(p.Called);
            var card = current;
            var panel = panels[r.Player];
            yield return card.Flip(r.Card);
            AddTrail(r.Card.Color);

            if (r.Correct)
            {
                SetStreak(r.Streak);
                Sfx.Play(Sfx.Correct(r.Streak));
                Haptics.Tick();
                yield return Tween.Wait(.26f);
                if (gen != generation) yield break;
                Vector2 from = card.Rt.anchoredPosition, to = UI.CenterIn(panel.Find("Avatar") as RectTransform, slot);
                yield return Tween.Run(.52f, t =>
                {
                    float up = t < .25f ? Ease.OutCubic(t / .25f) : 1;
                    float go = t < .25f ? 0 : Ease.InOutCubic((t - .25f) / .75f);
                    card.Rt.anchoredPosition = Vector2.Lerp(from + Vector2.up * 20 * up, to, go);
                    card.Rt.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(8 * up, -25, go));
                    card.Rt.localScale = Vector3.one * Mathf.Lerp(1 + .05f * up, .15f, go);
                }, Ease.Linear);
                Destroy(card.gameObject);
                panelScore[r.Player].text = match.Players[r.Player].Score.ToString();
                StartCoroutine(Tween.Pop(panel, .32f, 1.1f));
                StartCoroutine(fx.Float(fx.Local(panelScore[r.Player].rectTransform) + new Vector2(60, 10), "+1"));
                if (r.Milestone > 0) yield return Celebrate(r.Milestone, r.Streak, panel);
            }
            else
            {
                Sfx.Play(Sfx.Wrong);
                Haptics.Buzz();
                StartCoroutine(fx.Flash());
                yield return Tween.Shake(card.Rt, .42f, 16);
                if (gen != generation) yield break;
                string sub = r.StreakBefore >= 2 ? $"Streak ends at {r.StreakBefore}" : "Turn over";
                yield return fx.Say(r.Card.IsRed ? "It was Kɔkɔɔ!" : "It was Tuntum!", r.Card.IsRed ? Theme.Red : Theme.Paper, sub, 1f);
                SetStreak(0);
                float rot = Random.Range(-15f, 15f);
                Vector2 from = card.Rt.anchoredPosition, to = UI.CenterIn(missPile, slot);
                yield return Tween.Run(.48f, t =>
                {
                    card.Rt.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
                    card.Rt.localRotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(0, rot + 360, t));
                    card.Rt.localScale = Vector3.one * Mathf.LerpUnclamped(1, .5f, t);
                }, Ease.OutBack);
                Destroy(card.gameObject);
                if (missTop) Destroy(missTop.gameObject);
                missTop = CardView.Create(missPile, cardH * .5f, "Miss top");
                missTop.Rt.anchorMin = missTop.Rt.anchorMax = new Vector2(.5f, .5f);
                missTop.Show(r.Card); missTop.SetFaceUp(true);
                missTop.Rt.localRotation = Quaternion.Euler(0, 0, rot);
            }

            if (gen != generation) yield break;
            if (r.GameOver)
            {
                yield return Tween.Wait(.3f);
                ShowEnd();
                yield break;
            }
            if (r.TurnPassed) yield return PassTurn(gen);
            if (gen != generation) yield break;
            yield return Deal();
        }

        IEnumerator Deal()
        {
            current = CardView.Create(slot, cardH, "Current card");
            current.Rt.anchorMin = current.Rt.anchorMax = new Vector2(.5f, .5f);
            Vector2 from = UI.CenterIn(deckPile, slot);
            Sfx.Play(Sfx.Deal);
            var rt = current.Rt;
            yield return Tween.Run(.52f, t =>
            {
                float k = Ease.OutCubic(t);
                rt.anchoredPosition = Vector2.Lerp(from, Vector2.zero, k) + Vector2.up * 14 * Ease.Hump(Mathf.Clamp01(t * 1.3f));
                rt.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(18, 0, k) - 4 * Ease.Hump(t));
                rt.localScale = Vector3.one * (Mathf.Lerp(.5f, 1, k) + .06f * Ease.Hump(t));
            }, Ease.Linear);
            rt.anchoredPosition = Vector2.zero; rt.localRotation = Quaternion.identity; rt.localScale = Vector3.one;
            if (match != null) RenderDeck();
        }

        void AddTrail(CardColor c)
        {
            if (trail.childCount >= 12) DestroyImmediate(trail.GetChild(0).gameObject);
            var chip = UI.Chunk(trail, "Drawn", c == CardColor.Red ? Theme.Red : Theme.Ink, 5, 3, 0);
            chip.sizeDelta = new Vector2(18, 26);
            StartCoroutine(Tween.Run(.35f, t => chip.localScale = Vector3.one * t, Ease.OutBack));
        }

        IEnumerator Celebrate(int level, int streak, RectTransform panel)
        {
            Haptics.Tick();
            // every milestone: the crowd cheers and the player's own box parties
            Sfx.Play(Sfx.Cheer(level), level == 1 ? .85f : 1f);
            var av = (RectTransform)panel.Find("Avatar");
            StartCoroutine(Tween.Run(.7f, t =>
            {
                av.anchoredPosition = new Vector2(31, 16 * Ease.Hump(t));
                av.localRotation = Quaternion.Euler(0, 0, -360 * t);
                av.localScale = Vector3.one * (1 + .25f * Ease.Hump(t));
            }, Ease.OutCubic));
            fx.Burst(fx.Local(av));
            StartCoroutine(fx.Ribbon(panel, $"{streak} IN A ROW!"));
            if (level == 1)
            {
                Sfx.Play(Sfx.Yay);
                yield return fx.Say("NICE!", Theme.Mint, "3 in a row", .7f);
                yield break;
            }
            Sfx.Play(Sfx.Fanfare(level - 1));
            fx.Confetti(fx.Local(slot), 60 + level * 40);
            StartCoroutine(Tween.Shake(game, .42f, 10));
            string[] lines = { "", "NICE!", "ON FIRE!", "UNSTOPPABLE!", "LEGENDARY!", "GODLIKE!" };
            yield return fx.Say(lines[Mathf.Clamp(level, 1, 5)], level >= 3 ? Theme.Mint : Theme.Paper, $"{streak} in a row", .9f);
        }

        IEnumerator PassTurn(int gen)
        {
            Sfx.Play(Sfx.Whoosh);
            RenderTurn();
            var p = match.Players[match.Turn];
            bool mine = mode == Mode.PassAndPlay || match.Turn == (mode == Mode.Computer ? 0 : me);
            string title = mode == Mode.PassAndPlay ? $"{p.Name}'s turn" : mine ? "Your turn" : $"{p.Name}'s turn";
            if (mode == Mode.PassAndPlay)
            {
                bool tapped = false;
                sheetAction = () => tapped = true;
                yield return OpenSheet(Initial(p.Name), Theme.PlayerColor(match.Turn), title, $"Pass the phone to {p.Name}", $"I'm {p.Name}, ready");
                while (!tapped && gen == generation) yield return null;
            }
            else
            {
                yield return OpenSheet(Initial(p.Name), Theme.PlayerColor(match.Turn), title, $"{match.CardsLeft} cards left · {match.Players[0].Score}–{match.Players[1].Score}", null);
                yield return Tween.Wait(1.1f);
            }
            if (gen != generation) yield break;
            yield return CloseSheet();
        }

        void NextAction()
        {
            if (!ComputerTurn || busy || match.IsOver) return;
            StartCoroutine(ComputerGuess(generation));
        }

        IEnumerator ComputerGuess(int gen)
        {
            busy = true;
            RenderTurn();
            yield return Tween.Wait(.9f);
            if (gen != generation || match == null) yield break;
            var c = cpu.Choose(match);
            (c == CardColor.Black ? pickBlack : pickRed).Root.GetComponent<PressSink>().Tap();
            queue.Enqueue(new Pending { Index = match.Index, Called = c });
            if (!running) StartCoroutine(RunQueue(gen));
        }

        // ---- online ----------------------------------------------------------------------------------
        void OpenOnline()
        {
            PlayerPrefs.SetString("name", PlayerName);
            Show(online);
            hostPanel.gameObject.SetActive(true); joinPanel.gameObject.SetActive(true); waitPanel.gameObject.SetActive(false);
            codeField.text = "";
            OnlineNote("Play a friend on another phone in real time.");
        }

        void WithConnection(System.Action then)
        {
            if (net.Connected) { then(); return; }
            OnlineNote("Connecting…");
            net.Connect(() => then(), err => OnlineNote(err, true));
        }

        void CreateRoom()
        {
            WithConnection(() => net.Send(new Msg { t = "create", name = PlayerName }));
        }

        void JoinRoom()
        {
            var code = (codeField.text ?? "").Trim().ToUpperInvariant();
            if (code.Length != 4) { OnlineNote("Type the 4-letter code from your friend.", true); return; }
            WithConnection(() => { OnlineNote($"Joining {code}…"); net.Send(new Msg { t = "join", code = code, name = PlayerName }); });
        }

        void OnNet(Msg m)
        {
            switch (m.t)
            {
                case "created":
                    roomCode.text = m.code;
                    hostPanel.gameObject.SetActive(false); joinPanel.gameObject.SetActive(false); waitPanel.gameObject.SetActive(true);
                    OnlineNote("Room open. Share the code.");
                    break;
                case "start":
                    if (m.names == null || m.names.Length != 2 || (m.you != 0 && m.you != 1)) return;
                    Sfx.Play(Sfx.Join);
                    BeginMatch(Mode.Online, Match.Remote(Clean(m.names[0], "Player 1"), Clean(m.names[1], "Player 2")), m.you);
                    break;
                case "result":
                    if (match == null || mode != Mode.Online) return;
                    if (!Card.TryParse(m.card, out var card) || (m.col != "r" && m.col != "k")) return;
                    int expected = match.Index + queue.Count + (running ? 1 : 0);
                    if (m.i < match.Index) return; // already shown
                    if (m.i > expected) { OnNetLost("Lost track of the game."); return; }
                    Enqueue(new Pending { Index = m.i, Called = m.col == "r" ? CardColor.Red : CardColor.Black, Card = card });
                    break;
                case "rematch":
                    if (end.gameObject.activeSelf) againLabel.text = $"{Clean(m.name, "Friend")} wants a rematch!";
                    break;
                case "left":
                    if (match == null) return;
                    ShowGoneSheet($"{Clean(m.name, "Your friend")} left", "The game has ended.");
                    break;
                case "error":
                    if (online.gameObject.activeSelf) OnlineNote(m.msg ?? "Something went wrong.", true);
                    break;
            }
        }

        void OnNetLost(string why)
        {
            if (match != null && mode == Mode.Online) ShowGoneSheet("Connection lost", why);
            else if (online.gameObject.activeSelf) OnlineNote(why, true);
        }

        void ShowGoneSheet(string title, string sub)
        {
            generation++;
            StopAllCoroutinesExceptIdle();
            net.Close();
            var other = match != null ? match.Players[1 - me].Name : "?";
            sheetAction = GoHome;
            StartCoroutine(OpenSheet(Initial(other), Theme.PlayerColor(1 - me), title, sub, "Back to home"));
        }
    }

    /// <summary>Short phone vibrations where the platform allows it.</summary>
    public static class Haptics
    {
        public static bool On = true;
        public static void Tick() { }
        public static void Buzz()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (On) Handheld.Vibrate();
#endif
        }
    }
}
