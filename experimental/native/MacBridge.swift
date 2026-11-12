// JSON-line helper. No chat text is returned; only control geometry/state.
import AppKit
import ApplicationServices
import Foundation

func attr(_ element: AXUIElement, _ name: String) -> CFTypeRef? {
    var value: CFTypeRef?
    guard AXUIElementCopyAttributeValue(element, name as CFString, &value) == .success else { return nil }
    return value
}
func children(_ element: AXUIElement) -> [AXUIElement] {
    return attr(element, kAXChildrenAttribute) as? [AXUIElement] ?? []
}
func label(_ element: AXUIElement) -> String {
    for key in [kAXTitleAttribute, kAXDescriptionAttribute, kAXHelpAttribute] {
        if let name = attr(element, key) as? String, !name.isEmpty { return name }
    }
    return ""
}
func rect(_ element: AXUIElement) -> CGRect? {
    guard let p = attr(element, kAXPositionAttribute), let s = attr(element, kAXSizeAttribute),
          CFGetTypeID(p) == AXValueGetTypeID(), CFGetTypeID(s) == AXValueGetTypeID() else { return nil }
    var point = CGPoint.zero; var size = CGSize.zero
    guard AXValueGetValue(p as! AXValue, .cgPoint, &point),
          AXValueGetValue(s as! AXValue, .cgSize, &size), size.width > 0, size.height > 0 else { return nil }
    return CGRect(origin: point, size: size)
}
func applications() -> [NSRunningApplication] {
    return NSWorkspace.shared.runningApplications.filter {
        ["ChatGPT", "Codex"].contains($0.localizedName ?? "") ||
        ["com.openai.chat", "com.openai.codex"].contains($0.bundleIdentifier ?? "")
    }
}
func snapshot() -> [String: Any] {
    guard AXIsProcessTrusted() else {
        return ["error": "Allow Accessibility for YoChat's MacBridge and terminal in System Settings."]
    }
    let apps = applications()
    guard !apps.isEmpty else { return ["error": "Start ChatGPT and sign in first."] }
    var active = false; var recognized = false; var incomplete = false
    var pill: [String: Double]? = nil
    for app in apps {
        let root = AXUIElementCreateApplication(app.processIdentifier)
        AXUIElementSetMessagingTimeout(root, 0.15)
        guard let windows = attr(root, kAXWindowsAttribute) as? [AXUIElement] else { incomplete = true; continue }
        for window in windows {
            var queue = [window]; var index = 0; var mute = false; var stop: CGRect?
            var obstacles = [CGRect]()
            while index < queue.count && index < 2500 {
                let node = queue[index]; index += 1
                let text = label(node)
                if ["Start voice chat", "Start new voice chat", "Mute microphone", "Mute voice chat", "Unmute voice chat"].contains(text) { recognized = true }
                if ["Mute voice chat", "Unmute voice chat"].contains(text) { mute = true; active = true }
                if text == "Stop voice chat" { recognized = true; active = true; stop = rect(node) }
                if let r = rect(node), r.width >= 80 && r.width <= 650 && r.height >= 28 && r.height <= 240 { obstacles.append(r) }
                queue.append(contentsOf: children(node))
            }
            if index < queue.count { incomplete = true }
            let minimized = (attr(window, kAXMinimizedAttribute) as? Bool) ?? false
            if mute, let r = stop, !minimized, !app.isHidden {
                var top = r.minY
                for obstacle in obstacles where obstacle.maxY <= r.minY && obstacle.maxY > r.minY - 240 && obstacle.minX < r.midX + 160 && obstacle.maxX > r.midX - 160 {
                    top = min(top, obstacle.minY)
                }
                pill = ["x": r.midX, "top": top]
            }
        }
    }
    if !active && (!recognized || incomplete) {
        return ["error": "Voice controls are unavailable. Check English UI and Accessibility access."]
    }
    var result: [String: Any] = ["active": active]
    if let value = pill { result["pill"] = value }
    return result
}

func sendHotkey(_ keys: [String]) -> [String: Any] {
    let state = snapshot()
    guard state["error"] == nil, state["active"] as? Bool == false else {
        return ["error": "Voice is active or its state is unavailable; shortcut was not sent."]
    }
    let codes: [String: CGKeyCode] = ["a":0,"s":1,"d":2,"f":3,"h":4,"g":5,"z":6,"x":7,"c":8,"v":9,"b":11,"q":12,"w":13,"e":14,"r":15,"y":16,"t":17,"1":18,"2":19,"3":20,"4":21,"6":22,"5":23,"9":25,"7":26,"8":28,"0":29,"o":31,"u":32,"i":34,"p":35,"enter":36,"l":37,"j":38,"k":40,"n":45,"m":46,"space":49]
    guard keys.count >= 2, let key = keys.last, let code = codes[key], Set(keys).count == keys.count else { return ["error": "Unsupported hotkey."] }
    let modifiers: [String: CGEventFlags] = ["cmd": .maskCommand, "ctrl": .maskControl, "alt": .maskAlternate, "shift": .maskShift]
    var flags = CGEventFlags()
    for token in keys.dropLast() {
        guard let flag = modifiers[token] else { return ["error": "Unsupported modifier."] }
        flags.insert(flag)
    }
    guard let down = CGEvent(keyboardEventSource: nil, virtualKey: code, keyDown: true),
          let up = CGEvent(keyboardEventSource: nil, virtualKey: code, keyDown: false) else { return ["error": "Cannot create key event."] }
    down.flags = flags; up.flags = flags
    down.post(tap: .cghidEventTap); up.post(tap: .cghidEventTap)
    return ["ok": true]
}

while let line = readLine() {
    let result: [String: Any] = autoreleasepool {
        guard let data = line.data(using: .utf8), let request = (try? JSONSerialization.jsonObject(with: data)) as? [String: Any] else { return ["error": "Invalid request."] }
        if request["command"] as? String == "hotkey" { return sendHotkey(request["keys"] as? [String] ?? []) }
        return snapshot()
    }
    if let data = try? JSONSerialization.data(withJSONObject: result, options: [.sortedKeys]), let text = String(data: data, encoding: .utf8) {
        print(text); fflush(stdout)
    }
}
