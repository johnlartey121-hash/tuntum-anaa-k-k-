import UIKit
import Capacitor

/// Registers the app's own LocalNet plugin with Capacitor.
class MainViewController: CAPBridgeViewController {
    override open func capacitorDidLoad() {
        bridge?.registerPluginInstance(LocalNetPlugin())
    }
}
