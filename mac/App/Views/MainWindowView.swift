import GlystrataCore
import SwiftUI
import UniformTypeIdentifiers

struct MainWindowView: View {
    @Environment(AppModel.self) private var model
    @Environment(\.colorScheme) private var colorScheme
    @State private var sidebarWidth: CGFloat = 258

    var body: some View {
        let theme = ThemeColors.resolve(model.settings.theme, system: colorScheme)
        VStack(spacing: 0) {
            ToolbarView()
            HStack(spacing: 0) {
                SidebarView()
                    .frame(width: sidebarWidth)
                DragHandle(axis: .horizontal) { delta in
                    sidebarWidth = min(max(sidebarWidth + delta, 140), 520)
                }
                PaneLayoutView(node: model.layout)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                    .background(theme.windowBackground.color)
            }
            // The rule above the status bar runs the full width of the window, sidebar included.
            theme.border.color.frame(height: 1)
            StatusBarView()
        }
        .environment(\.theme, theme)
        .font(.system(size: 12))
        .foregroundStyle(theme.primaryText.color)
        .background(theme.windowBackground.color)
        .preferredColorScheme(preferredScheme)
        .onDrop(of: [.fileURL], isTargeted: nil) { providers in
            for provider in providers {
                _ = provider.loadObject(ofClass: URL.self) { url, _ in
                    guard let url, url.isFileURL else {
                        return
                    }
                    Task { @MainActor in model.openDropped(paths: [url.path]) }
                }
            }
            return !providers.isEmpty
        }
        .task(id: theme) {
            model.applyTheme(theme)
        }
    }

    private var preferredScheme: ColorScheme? {
        model.settings.theme.colorScheme
    }
}

/// A 5-point bar that resizes whatever sits before it when dragged.
struct DragHandle: View {
    let axis: Axis
    let onDrag: (CGFloat) -> Void

    @Environment(\.theme) private var theme
    @State private var lastTranslation: CGFloat = 0

    var body: some View {
        theme.border.color
            .frame(width: axis == .horizontal ? 5 : nil, height: axis == .vertical ? 5 : nil)
            .contentShape(Rectangle())
            .onHover { inside in
                if inside {
                    (axis == .horizontal ? NSCursor.resizeLeftRight : NSCursor.resizeUpDown).push()
                } else {
                    NSCursor.pop()
                }
            }
            .gesture(
                DragGesture(minimumDistance: 1, coordinateSpace: .global)
                    .onChanged { value in
                        let translation = axis == .horizontal ? value.translation.width : value.translation.height
                        onDrag(translation - lastTranslation)
                        lastTranslation = translation
                    }
                    .onEnded { _ in
                        lastTranslation = 0
                    })
    }
}

/// Draws the pane tree: a pane, or two subtrees either side of a draggable divider.
struct PaneLayoutView: View {
    let node: PaneLayoutNode

    @Environment(AppModel.self) private var model

    var body: some View {
        switch node {
        case .editorPane(let paneID):
            PaneView(paneID: paneID)
        case .split(let id, let orientation, let ratio, let first, let second):
            GeometryReader { proxy in
                // "Horizontal" is the Windows build's name for panes side by side.
                let isSideBySide = orientation == .horizontal
                let total = isSideBySide ? proxy.size.width : proxy.size.height
                let firstLength = max(0, (total - 5) * ratio)
                let layout = isSideBySide ? AnyLayout(HStackLayout(spacing: 0)) : AnyLayout(VStackLayout(spacing: 0))
                layout {
                    PaneLayoutView(node: first)
                        .frame(width: isSideBySide ? firstLength : nil, height: isSideBySide ? nil : firstLength)
                    DragHandle(axis: isSideBySide ? .horizontal : .vertical) { delta in
                        guard total > 5 else {
                            return
                        }
                        model.setSplitRatio(id, (firstLength + delta) / (total - 5))
                    }
                    PaneLayoutView(node: second)
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                }
            }
        }
    }
}

struct ToolbarView: View {
    @Environment(AppModel.self) private var model
    @Environment(\.theme) private var theme

    var body: some View {
        HStack(spacing: 4) {
            ToolbarButton(help: model.localization.get("toolbar.new")) {
                model.newUntitled()
            } label: {
                Text("＋").font(.system(size: 24))
            }
            iconButton("ToolbarOpenFile", "toolbar.open") { model.openFiles() }
            iconButton("ToolbarSplitLeftRight", "toolbar.splitHorizontal") { model.splitActivePane(.horizontal) }
            iconButton("ToolbarSplitTopBottom", "toolbar.splitVertical") { model.splitActivePane(.vertical) }
            iconButton("ToolbarClosePane", "toolbar.closePane") { model.closeActivePane() }
            iconButton("ToolbarSinglePane", "toolbar.resetLayout") { model.collapseToSinglePane() }
            Spacer(minLength: 0)
        }
        .padding(.horizontal, 7)
        .frame(height: 38)
        .background(theme.surface.color)
    }

    private func iconButton(_ image: String, _ helpKey: String, action: @escaping () -> Void) -> some View {
        ToolbarButton(help: model.localization.get(helpKey), action: action) {
            Image(image)
                .resizable()
                .interpolation(.high)
                .frame(width: 18, height: 18)
        }
    }
}

/// The borderless 34×30 button of the toolbar, tinted on hover and press.
struct ToolbarButton<Label: View>: View {
    let help: String
    let action: () -> Void
    @ViewBuilder let label: () -> Label

    @Environment(\.theme) private var theme
    @State private var isHovered = false

    var body: some View {
        Button(action: action) {
            label()
                .foregroundStyle(theme.primaryText.color)
                .frame(width: 34, height: 30)
                .contentShape(Rectangle())
        }
        .buttonStyle(HoverButtonStyle(isHovered: isHovered))
        .onHover { isHovered = $0 }
        .help(help)
    }
}

struct HoverButtonStyle: ButtonStyle {
    let isHovered: Bool
    @Environment(\.theme) private var theme
    @Environment(\.isEnabled) private var isEnabled

    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .background(
                RoundedRectangle(cornerRadius: 4)
                    .fill(configuration.isPressed ? theme.buttonPressed.color : (isHovered ? theme.buttonHover.color : .clear)))
            .opacity(isEnabled ? 1 : 0.45)
    }
}
