import Foundation

public enum TextMetrics {
    public static func countCharacters(_ text: String?, mode: CharacterCountMode) -> Int {
        guard let text, !text.isEmpty else {
            return 0
        }

        var count = 0
        for character in text {
            if mode == .excludeWhitespace && character.unicodeScalars.allSatisfy(\.properties.isWhitespace) {
                continue
            }

            if mode == .excludeLineBreaks && character.unicodeScalars.allSatisfy(isLineBreak) {
                continue
            }

            count += 1
        }

        return count
    }

    private static func isLineBreak(_ scalar: Unicode.Scalar) -> Bool {
        scalar == "\r" || scalar == "\n" || scalar == "\u{2028}" || scalar == "\u{2029}"
    }
}
