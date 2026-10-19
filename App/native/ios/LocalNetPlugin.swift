import Foundation
import Capacitor
import Network

/// Lets this iPhone host a nearby game: a small WebSocket server on the Personal Hotspot / Wi-Fi network.
/// Guests connect to ws://<this phone's address>:8737. No internet is involved.
@objc(LocalNetPlugin)
public class LocalNetPlugin: CAPPlugin, CAPBridgedPlugin {
    public let identifier = "LocalNetPlugin"
    public let jsName = "LocalNet"
    public let pluginMethods: [CAPPluginMethod] = [
        CAPPluginMethod(name: "startServer", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "stopServer", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "send", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "getIp", returnType: CAPPluginReturnPromise)
    ]

    private var listener: NWListener?
    private var connections: [Int: NWConnection] = [:]
    private var nextId = 1
    private let queue = DispatchQueue(label: "tuntum.localnet")

    @objc func startServer(_ call: CAPPluginCall) {
        let port = UInt16(call.getInt("port") ?? 8737)
        queue.async {
            self.stopAll()
            let params = NWParameters.tcp
            let ws = NWProtocolWebSocket.Options()
            ws.autoReplyPing = true
            params.defaultProtocolStack.applicationProtocols.insert(ws, at: 0)
            params.allowLocalEndpointReuse = true
            do {
                guard let nwPort = NWEndpoint.Port(rawValue: port) else { call.reject("Bad port"); return }
                let l = try NWListener(using: params, on: nwPort)
                l.newConnectionHandler = { [weak self] c in self?.accept(c) }
                l.start(queue: self.queue)
                self.listener = l
                call.resolve(["ip": LocalNetPlugin.localIp() ?? ""])
            } catch {
                call.reject("Could not start hosting: \(error.localizedDescription)")
            }
        }
    }

    @objc func stopServer(_ call: CAPPluginCall) {
        queue.async { self.stopAll(); call.resolve() }
    }

    @objc func send(_ call: CAPPluginCall) {
        let id = call.getInt("id") ?? -1
        let text = call.getString("data") ?? ""
        queue.async {
            if let c = self.connections[id] { self.sendText(text, on: c) }
            call.resolve()
        }
    }

    @objc func getIp(_ call: CAPPluginCall) {
        call.resolve(["ip": LocalNetPlugin.localIp() ?? ""])
    }

    private func accept(_ c: NWConnection) {
        let id = nextId
        nextId += 1
        connections[id] = c
        c.stateUpdateHandler = { [weak self] state in
            switch state {
            case .failed(_), .cancelled: self?.drop(id)
            default: break
            }
        }
        c.start(queue: queue)
        notifyListeners("connection", data: ["id": id])
        receive(id, c)
    }

    private func receive(_ id: Int, _ c: NWConnection) {
        c.receiveMessage { [weak self] data, context, _, error in
            guard let self = self else { return }
            if let meta = context?.protocolMetadata(definition: NWProtocolWebSocket.definition) as? NWProtocolWebSocket.Metadata,
               meta.opcode == .close {
                self.drop(id)
                return
            }
            if let data = data, !data.isEmpty, let text = String(data: data, encoding: .utf8) {
                self.notifyListeners("message", data: ["id": id, "data": text])
            }
            if error != nil { self.drop(id); return }
            self.receive(id, c)
        }
    }

    private func sendText(_ text: String, on c: NWConnection) {
        let meta = NWProtocolWebSocket.Metadata(opcode: .text)
        let context = NWConnection.ContentContext(identifier: "text", metadata: [meta])
        c.send(content: text.data(using: .utf8), contentContext: context, isComplete: true, completion: .contentProcessed { _ in })
    }

    private func drop(_ id: Int) {
        guard let c = connections.removeValue(forKey: id) else { return }
        c.cancel()
        notifyListeners("disconnect", data: ["id": id])
    }

    private func stopAll() {
        listener?.cancel()
        listener = nil
        for (_, c) in connections { c.cancel() }
        connections.removeAll()
    }

    /// The address guests should use: Personal Hotspot (bridge100, usually 172.20.10.1) first, then Wi-Fi (en0).
    static func localIp() -> String? {
        var found: [String: String] = [:]
        var ifaddr: UnsafeMutablePointer<ifaddrs>?
        guard getifaddrs(&ifaddr) == 0, let first = ifaddr else { return nil }
        defer { freeifaddrs(ifaddr) }
        for ptr in sequence(first: first, next: { $0.pointee.ifa_next }) {
            let iface = ptr.pointee
            guard let addr = iface.ifa_addr, addr.pointee.sa_family == UInt8(AF_INET) else { continue }
            let name = String(cString: iface.ifa_name)
            var host = [CChar](repeating: 0, count: Int(NI_MAXHOST))
            if getnameinfo(addr, socklen_t(addr.pointee.sa_len), &host, socklen_t(host.count), nil, 0, NI_NUMERICHOST) == 0 {
                found[name] = String(cString: host)
            }
        }
        if let ip = found.first(where: { $0.key.hasPrefix("bridge") })?.value { return ip }
        return found["en0"] ?? found.first(where: { $0.value.hasPrefix("192.168.") || $0.value.hasPrefix("172.20.") })?.value
    }
}
