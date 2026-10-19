package com.johnlartey.tuntum;

import com.getcapacitor.JSObject;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;

import org.java_websocket.WebSocket;
import org.java_websocket.handshake.ClientHandshake;
import org.java_websocket.server.WebSocketServer;

import java.net.Inet4Address;
import java.net.InetAddress;
import java.net.InetSocketAddress;
import java.net.NetworkInterface;
import java.util.Collections;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.atomic.AtomicInteger;

/**
 * Lets this phone host a nearby game: a small WebSocket server on the hotspot / Wi-Fi network.
 * Guests connect to ws://<this phone's address>:8737. No internet is involved.
 */
@CapacitorPlugin(name = "LocalNet")
public class LocalNetPlugin extends Plugin {
    private Server server;
    private final Map<Integer, WebSocket> sockets = new ConcurrentHashMap<>();
    private final AtomicInteger nextId = new AtomicInteger(1);

    @PluginMethod
    public void startServer(PluginCall call) {
        int port = call.getInt("port", 8737);
        stopAll();
        try {
            server = new Server(new InetSocketAddress(port));
            server.setReuseAddr(true);
            server.start();
            JSObject r = new JSObject();
            r.put("ip", localIp());
            call.resolve(r);
        } catch (Exception e) {
            call.reject("Could not start hosting: " + e.getMessage());
        }
    }

    @PluginMethod
    public void stopServer(PluginCall call) {
        stopAll();
        call.resolve();
    }

    @PluginMethod
    public void send(PluginCall call) {
        WebSocket ws = sockets.get(call.getInt("id", -1));
        if (ws != null && ws.isOpen()) ws.send(call.getString("data", ""));
        call.resolve();
    }

    @PluginMethod
    public void getIp(PluginCall call) {
        JSObject r = new JSObject();
        r.put("ip", localIp());
        call.resolve(r);
    }

    @Override
    protected void handleOnDestroy() {
        stopAll();
    }

    private void stopAll() {
        if (server != null) {
            try { server.stop(500); } catch (Exception ignored) { }
            server = null;
        }
        sockets.clear();
    }

    /** The address guests should use: the hotspot interface first, then Wi-Fi. */
    static String localIp() {
        String fallback = "";
        try {
            for (NetworkInterface ni : Collections.list(NetworkInterface.getNetworkInterfaces())) {
                if (!ni.isUp() || ni.isLoopback()) continue;
                String name = ni.getName();
                for (InetAddress a : Collections.list(ni.getInetAddresses())) {
                    if (!(a instanceof Inet4Address)) continue;
                    String ip = a.getHostAddress();
                    boolean hotspot = name.startsWith("ap") || name.startsWith("swlan") || name.startsWith("softap") || ip.startsWith("192.168.43.");
                    if (hotspot) return ip;
                    if (name.startsWith("wlan") && fallback.isEmpty()) fallback = ip;
                    else if (fallback.isEmpty() && (ip.startsWith("192.168.") || ip.startsWith("172.20."))) fallback = ip;
                }
            }
        } catch (Exception ignored) { }
        return fallback;
    }

    private class Server extends WebSocketServer {
        Server(InetSocketAddress addr) { super(addr); }

        @Override
        public void onOpen(WebSocket conn, ClientHandshake handshake) {
            int id = nextId.getAndIncrement();
            conn.setAttachment(id);
            sockets.put(id, conn);
            JSObject e = new JSObject();
            e.put("id", id);
            notifyListeners("connection", e);
        }

        @Override
        public void onClose(WebSocket conn, int code, String reason, boolean remote) {
            Integer id = conn.getAttachment();
            if (id == null) return;
            sockets.remove(id);
            JSObject e = new JSObject();
            e.put("id", id);
            notifyListeners("disconnect", e);
        }

        @Override
        public void onMessage(WebSocket conn, String message) {
            Integer id = conn.getAttachment();
            if (id == null) return;
            JSObject e = new JSObject();
            e.put("id", id);
            e.put("data", message);
            notifyListeners("message", e);
        }

        @Override
        public void onError(WebSocket conn, Exception ex) { }

        @Override
        public void onStart() { }
    }
}
