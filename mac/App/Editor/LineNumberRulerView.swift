import AppKit

/// The line-number gutter. A wrapped line is numbered once, on its first visual row.
final class LineNumberRulerView: NSRulerView {
    private weak var textView: NSTextView?
    var textColor: NSColor = .secondaryLabelColor { didSet { needsDisplay = true } }
    var backgroundColor: NSColor = .textBackgroundColor { didSet { needsDisplay = true } }
    var font: NSFont = .monospacedDigitSystemFont(ofSize: 12, weight: .regular) { didSet { updateThickness() } }

    /// The gap between the numbers and the dotted rule on the gutter's right edge. The text view's
    /// own inset leaves the same gap on the rule's other side.
    private static let numberToRule: CGFloat = 14

    init(textView: NSTextView, scrollView: NSScrollView) {
        self.textView = textView
        super.init(scrollView: scrollView, orientation: .verticalRuler)
        clientView = textView
        // Since macOS 14 views no longer clip by default, and an unclipped ruler lets the text draw over it.
        clipsToBounds = true
        updateThickness()

        NotificationCenter.default.addObserver(self, selector: #selector(refresh), name: NSText.didChangeNotification, object: textView)
        NotificationCenter.default.addObserver(self, selector: #selector(refresh), name: NSView.boundsDidChangeNotification, object: scrollView.contentView)
        NotificationCenter.default.addObserver(self, selector: #selector(refresh), name: NSView.frameDidChangeNotification, object: textView)
    }

    @available(*, unavailable)
    required init(coder: NSCoder) {
        fatalError("init(coder:) is not used")
    }

    @objc private func refresh() {
        updateThickness()
        needsDisplay = true
    }

    private func updateThickness() {
        let lineCount = max(1, textView?.string.utf16.reduce(into: 1) { count, unit in if unit == 0x0A { count += 1 } } ?? 1)
        let digits = max(2, String(lineCount).count)
        let digitWidth = ("8" as NSString).size(withAttributes: [.font: font]).width
        let thickness = ceil(digitWidth * CGFloat(digits)) + 12 + Self.numberToRule
        if abs(ruleThickness - thickness) > 0.5 {
            ruleThickness = thickness
        }
    }

    override func drawHashMarksAndLabels(in rect: NSRect) {
        backgroundColor.setFill()
        bounds.fill()

        guard let textView, let layoutManager = textView.layoutManager, let container = textView.textContainer,
              let visibleRect = scrollView?.contentView.bounds else {
            return
        }

        let content = textView.string as NSString
        let attributes: [NSAttributedString.Key: Any] = [.font: font, .foregroundColor: textColor]
        let originY = textView.textContainerOrigin.y

        func draw(_ number: Int, lineFragment: NSRect) {
            let label = String(number) as NSString
            let size = label.size(withAttributes: attributes)
            let y = convert(NSPoint(x: 0, y: lineFragment.minY + originY), from: textView).y
            label.draw(at: NSPoint(x: ruleThickness - size.width - Self.numberToRule, y: y + (lineFragment.height - size.height) / 2), withAttributes: attributes)
        }

        let rule = NSBezierPath()
        rule.move(to: NSPoint(x: ruleThickness - 0.5, y: rect.minY))
        rule.line(to: NSPoint(x: ruleThickness - 0.5, y: rect.maxY))
        rule.lineWidth = 1
        // Phased on the document, not the visible part, so the dots hold still while scrolling.
        rule.setLineDash([1, 1], count: 2, phase: rect.minY.truncatingRemainder(dividingBy: 2))
        textColor.withAlphaComponent(0.5).setStroke()
        rule.stroke()

        let glyphRange = layoutManager.glyphRange(forBoundingRect: visibleRect, in: container)
        let characterRange = layoutManager.characterRange(forGlyphRange: glyphRange, actualGlyphRange: nil)

        var lineNumber = 1
        var scan = 0
        while scan < characterRange.location {
            var end = 0
            content.getLineStart(nil, end: &end, contentsEnd: nil, for: NSRange(location: scan, length: 0))
            if end > characterRange.location {
                break
            }
            scan = end
            lineNumber += 1
        }

        var location = scan
        while location < content.length, location <= NSMaxRange(characterRange) {
            var end = 0
            content.getLineStart(nil, end: &end, contentsEnd: nil, for: NSRange(location: location, length: 0))
            let glyphIndex = layoutManager.glyphIndexForCharacter(at: location)
            draw(lineNumber, lineFragment: layoutManager.lineFragmentRect(forGlyphAt: glyphIndex, effectiveRange: nil))
            location = end
            lineNumber += 1
        }

        // The empty line after a trailing line break (or an empty document) has no glyphs of its own.
        let extra = layoutManager.extraLineFragmentRect
        if extra.height > 0, extra.maxY >= visibleRect.minY - originY, extra.minY <= visibleRect.maxY {
            draw(lineNumber, lineFragment: extra)
        }
    }
}
