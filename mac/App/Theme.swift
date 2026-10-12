import AppKit
import GlystrataCore
import SwiftUI

/// One colour token, usable from both SwiftUI and AppKit.
struct ThemeColor: Equatable {
    let hex: String

    var color: Color { Color(nsColor: nsColor) }

    var nsColor: NSColor { NSColor(hex: hex) ?? .gray }
}

/// The colour tokens of the Windows build's Light.xaml and Dark.xaml, under the same names.
struct ThemeColors: Equatable {
    let kind: ThemeKind
    let windowBackground: ThemeColor
    let surface: ThemeColor
    let sidebar: ThemeColor
    let border: ThemeColor
    let primaryText: ThemeColor
    let secondaryText: ThemeColor
    let accent: ThemeColor
    let editorBackground: ThemeColor
    let editorForeground: ThemeColor
    let tabBackground: ThemeColor
    let tabActive: ThemeColor
    let tabActiveHover: ThemeColor
    let tabHover: ThemeColor
    let tabActiveBorder: ThemeColor
    let tabForeground: ThemeColor
    let buttonHover: ThemeColor
    let buttonPressed: ThemeColor

    static let light = ThemeColors(
        kind: .light,
        windowBackground: ThemeColor(hex: "#F7F8FA"),
        surface: ThemeColor(hex: "#FFFFFF"),
        sidebar: ThemeColor(hex: "#F0F2F5"),
        border: ThemeColor(hex: "#D9DDE3"),
        primaryText: ThemeColor(hex: "#202124"),
        secondaryText: ThemeColor(hex: "#68707C"),
        accent: ThemeColor(hex: "#3B6FF5"),
        editorBackground: ThemeColor(hex: "#FFFFFF"),
        editorForeground: ThemeColor(hex: "#24292F"),
        tabBackground: ThemeColor(hex: "#F7F8FA"),
        tabActive: ThemeColor(hex: "#E9EEF9"),
        tabActiveHover: ThemeColor(hex: "#E1E8F8"),
        tabHover: ThemeColor(hex: "#F0F3F8"),
        tabActiveBorder: ThemeColor(hex: "#B8C9F4"),
        tabForeground: ThemeColor(hex: "#68707C"),
        buttonHover: ThemeColor(hex: "#E8EEFF"),
        buttonPressed: ThemeColor(hex: "#D3DFFC"))

    static let dark = ThemeColors(
        kind: .dark,
        windowBackground: ThemeColor(hex: "#17191D"),
        surface: ThemeColor(hex: "#202329"),
        sidebar: ThemeColor(hex: "#1D2025"),
        border: ThemeColor(hex: "#343942"),
        primaryText: ThemeColor(hex: "#F1F3F5"),
        secondaryText: ThemeColor(hex: "#A6ADB8"),
        accent: ThemeColor(hex: "#7C9CFF"),
        editorBackground: ThemeColor(hex: "#202329"),
        editorForeground: ThemeColor(hex: "#E6EAF0"),
        tabBackground: ThemeColor(hex: "#1D2025"),
        tabActive: ThemeColor(hex: "#28303B"),
        tabActiveHover: ThemeColor(hex: "#303A49"),
        tabHover: ThemeColor(hex: "#242B34"),
        tabActiveBorder: ThemeColor(hex: "#4D5E79"),
        tabForeground: ThemeColor(hex: "#A6ADB8"),
        buttonHover: ThemeColor(hex: "#2B3550"),
        buttonPressed: ThemeColor(hex: "#37426B"))

    static func resolve(_ preference: ThemePreference, system: ColorScheme) -> ThemeColors {
        switch preference {
        case .light:
            return .light
        case .dark:
            return .dark
        case .system:
            return system == .dark ? .dark : .light
        }
    }
}

extension EnvironmentValues {
    @Entry var theme: ThemeColors = .light
}

extension NSColor {
    /// Accepts "#RRGGBB" and the "#AARRGGBB" form the Windows build's settings may contain.
    convenience init?(hex: String) {
        let digits = hex.trimmingCharacters(in: .whitespaces).drop { $0 == "#" }
        guard digits.count == 6 || digits.count == 8, let value = UInt64(digits, radix: 16) else {
            return nil
        }

        let alpha = digits.count == 8 ? CGFloat((value >> 24) & 0xFF) / 255 : 1
        self.init(
            srgbRed: CGFloat((value >> 16) & 0xFF) / 255,
            green: CGFloat((value >> 8) & 0xFF) / 255,
            blue: CGFloat(value & 0xFF) / 255,
            alpha: alpha)
    }
}
