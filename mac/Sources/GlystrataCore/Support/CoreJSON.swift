import Foundation

/// JSON settings shared by every file Glystrata persists. The shapes match what the Windows build
/// writes with System.Text.Json (camelCase keys, enums as numbers, ISO-8601 UTC dates) so a `.gss`
/// snapshot sidecar synced between the two platforms opens on both.
enum CoreJSON {
    static func makeEncoder() -> JSONEncoder {
        let encoder = JSONEncoder()
        // sortedKeys keeps output stable between writes, and happens to put a sidecar's "notice"
        // first, where a person or tool opening the file reads it before anything else.
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys, .withoutEscapingSlashes]
        encoder.dateEncodingStrategy = .custom { date, encoder in
            var container = encoder.singleValueContainer()
            try container.encode(formatDate(date))
        }
        return encoder
    }

    static func makeDecoder() -> JSONDecoder {
        let decoder = JSONDecoder()
        // Comments and trailing commas: a hand-edited settings file should still load.
        decoder.allowsJSON5 = true
        decoder.dateDecodingStrategy = .custom { decoder in
            let container = try decoder.singleValueContainer()
            let text = try container.decode(String.self)
            guard let date = parseDate(text) else {
                throw DecodingError.dataCorruptedError(in: container, debugDescription: "Invalid date: \(text)")
            }
            return date
        }
        return decoder
    }

    static func formatDate(_ date: Date) -> String {
        date.formatted(Date.ISO8601FormatStyle(includingFractionalSeconds: true))
    }

    /// .NET writes up to seven fractional digits and Foundation's own parsers are strict about how
    /// many they accept, so the fields are picked apart by hand.
    static func parseDate(_ text: String) -> Date? {
        let pattern = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(\.\d+)?(Z|[+-]\d{2}:?\d{2})?$/
        guard let match = text.wholeMatch(of: pattern) else {
            return nil
        }

        var components = DateComponents()
        components.year = Int(match.1)
        components.month = Int(match.2)
        components.day = Int(match.3)
        components.hour = Int(match.4)
        components.minute = Int(match.5)
        components.second = Int(match.6)

        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(secondsFromGMT: 0)!
        guard var date = calendar.date(from: components) else {
            return nil
        }

        if let fraction = match.7, let seconds = Double("0" + fraction) {
            date.addTimeInterval(seconds)
        }

        if let zone = match.8, zone != "Z" {
            let digits = zone.dropFirst().filter(\.isNumber)
            guard digits.count == 4, let hours = Int(digits.prefix(2)), let minutes = Int(digits.suffix(2)) else {
                return nil
            }
            let offset = TimeInterval(hours * 3600 + minutes * 60)
            date.addTimeInterval(zone.hasPrefix("-") ? offset : -offset)
        }

        return date
    }
}
