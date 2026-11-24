import AppKit
import GlystrataCore
import SwiftUI

/// A tab's scrolling column, with the 6-point inset the Windows window gives its panels.
struct SettingsPage<Content: View>: View {
    @ViewBuilder let content: () -> Content

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                content()
            }
            .padding(6)
            .frame(maxWidth: .infinity, alignment: .leading)
        }
    }
}

/// A label in a fixed 250-point column with its control beside it.
struct SettingsRow<Control: View>: View {
    let label: String
    @ViewBuilder let control: () -> Control

    var body: some View {
        HStack(spacing: 0) {
            Text(label)
                .frame(width: 250, alignment: .leading)
            control()
        }
        .padding(.vertical, 6)
    }
}

struct SettingsHelp: View {
    let text: String

    @Environment(\.theme) private var theme

    var body: some View {
        Text(text)
            .font(.system(size: 11))
            .foregroundStyle(theme.secondaryText.color)
            .fixedSize(horizontal: false, vertical: true)
            .padding(EdgeInsets(top: -2, leading: 250, bottom: 10, trailing: 0))
            .frame(maxWidth: .infinity, alignment: .leading)
    }
}

/// A paragraph above a list of rows.
struct SettingsNote: View {
    let text: String

    @Environment(\.theme) private var theme

    var body: some View {
        Text(text)
            .font(.system(size: 11))
            .foregroundStyle(theme.secondaryText.color)
            .fixedSize(horizontal: false, vertical: true)
            .padding(.bottom, 8)
            .frame(maxWidth: .infinity, alignment: .leading)
    }
}

/// The colour well (which opens the system colour panel) and the `#RRGGBB` box, kept in step.
/// Together they cover the Windows picker's hue/saturation area, hex box and preview swatch.
struct ColorEntry: View {
    @Binding var hex: String

    var body: some View {
        HStack(spacing: 8) {
            ColorPicker("", selection: well, supportsOpacity: false)
                .labelsHidden()
                .frame(width: 44, height: 24)
            TextField("#RRGGBB", text: $hex)
                .textFieldStyle(.roundedBorder)
                .multilineTextAlignment(.center)
                .frame(width: 110)
                .autocorrectionDisabled()
        }
    }

    /// A half-typed value leaves the well on its last valid colour, like the Windows swatch.
    private var well: Binding<Color> {
        Binding(
            get: { Color(nsColor: NSColor(hex: HexColor.normalize(hex) ?? "#808080") ?? .gray) },
            set: { hex = Self.hexString($0) })
    }

    private static func hexString(_ color: Color) -> String {
        guard let rgb = NSColor(color).usingColorSpace(.sRGB) else {
            return "#808080"
        }

        func byte(_ value: CGFloat) -> Int { Int((min(max(value, 0), 1) * 255).rounded()) }
        return String(format: "#%02X%02X%02X", byte(rgb.redComponent), byte(rgb.greenComponent), byte(rgb.blueComponent))
    }
}

/// One colour setting: well and hex box, then the name.
struct ColorRow: View {
    let label: String
    @Binding var hex: String

    var body: some View {
        HStack(spacing: 0) {
            Text(label)
                .frame(width: 170, alignment: .leading)
            ColorEntry(hex: $hex)
        }
        .padding(.vertical, 2)
    }
}
