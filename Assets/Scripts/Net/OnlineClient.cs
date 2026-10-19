using System;
using NativeWebSocket;
using UnityEngine;

namespace Tuntum.Net
{
    /// <summary>One message on the wire (JSON). The server shuffles and decides every guess, so nobody can cheat by peeking.</summary>
    [Serializable]
    public class Msg
    {
        public string t;        // create, join, guess, rematch, ping | created, start, result, left, error, pong
        public string code;     // room code, 4 letters
        public string name;
        public string[] names;  // [creator, joiner]
        public int you = -1;    // which seat this phone is
        public int i = -1;      // card index
        public string col;      // "r" or "k"
        public string card;     // e.g. "QH"
        public bool ok;
        public int turn;
        public string msg;      // human-readable error
    }

    /// <summary>WebSocket link to the Tuntum server. Works on web, Android and iOS.</summary>
    public sealed class OnlineClient : MonoBehaviour
    {
        /// <summary>Set this to your deployed server (see Server/README.md). Can be overridden in PlayerPrefs "server".</summary>
        public const string DefaultServer = "wss://tuntum-anaa-kokoo.fly.dev";

        public event Action<Msg> OnMessage;
        public event Action<string> OnDisconnected;
        public bool Connected => ws != null && ws.State == WebSocketState.Open;

        WebSocket ws;
        float pingTimer;
        bool closing;

        public static string ServerUrl => PlayerPrefs.GetString("server", DefaultServer);

        public void Connect(Action onOpen, Action<string> onFail)
        {
            Close();
            closing = false;
            ws = new WebSocket(ServerUrl);
            bool opened = false;
            ws.OnOpen += () => { opened = true; onOpen?.Invoke(); };
            ws.OnError += e => { if (!opened) onFail?.Invoke("Can't reach the game server. Check your internet and try again."); };
            ws.OnClose += c => { if (opened && !closing) OnDisconnected?.Invoke("Connection lost."); };
            ws.OnMessage += bytes =>
            {
                Msg m = null;
                try { m = JsonUtility.FromJson<Msg>(System.Text.Encoding.UTF8.GetString(bytes)); } catch (Exception) { }
                if (m != null && !string.IsNullOrEmpty(m.t) && m.t != "pong") OnMessage?.Invoke(m);
            };
            _ = ws.Connect();
        }

        public void Send(Msg m)
        {
            if (!Connected) return;
            _ = ws.SendText(JsonUtility.ToJson(m));
        }

        public void Close()
        {
            if (ws == null) return;
            closing = true;
            var old = ws;
            ws = null;
            if (old.State == WebSocketState.Open || old.State == WebSocketState.Connecting) _ = old.Close();
        }

        void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            ws?.DispatchMessageQueue();
#endif
            if (Connected && (pingTimer += Time.unscaledDeltaTime) > 20f)
            {
                pingTimer = 0;
                Send(new Msg { t = "ping" });
            }
        }

        void OnApplicationQuit() => Close();
    }
}
