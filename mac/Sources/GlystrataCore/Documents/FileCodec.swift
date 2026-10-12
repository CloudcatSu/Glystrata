import Foundation

public struct FileCodec: Sendable {
    public init() {}

    public func read(path: String) throws -> DecodedText {
        let fullPath = PathUtilities.fullPath(path)
        let data = try Data(contentsOf: URL(fileURLWithPath: fullPath))
        return try decode(data, fingerprint: FileFingerprint.read(path: fullPath))
    }

    public func decode(_ data: Data, fingerprint: FileFingerprint) throws -> DecodedText {
        let encoding: FileEncodingKind
        let body: Data
        let rawText: String?

        if data.starts(with: [0xEF, 0xBB, 0xBF]) {
            encoding = .utf8WithBom
            body = data.dropFirst(3)
            rawText = String(data: body, encoding: .utf8)
        } else if data.starts(with: [0xFF, 0xFE]) {
            encoding = .utf16LittleEndian
            body = data.dropFirst(2)
            rawText = String(data: body, encoding: .utf16LittleEndian)
        } else if data.starts(with: [0xFE, 0xFF]) {
            throw GlystrataError.unsupportedTextEncoding("UTF-16 Big Endian 目前不在支援範圍內。")
        } else {
            encoding = .utf8
            body = data
            rawText = String(data: body, encoding: .utf8)
        }

        guard let rawText else {
            throw GlystrataError.unsupportedTextEncoding("無法以 UTF-8 或 UTF-16 LE 讀取檔案。")
        }

        return DecodedText(
            text: Self.normalizeLineEndings(rawText),
            encoding: encoding,
            lineEnding: Self.detectLineEnding(rawText),
            fingerprint: fingerprint)
    }

    public func encode(_ text: String, encoding: FileEncodingKind, lineEnding: LineEndingKind) -> Data {
        let normalized = Self.normalizeLineEndings(text)
        let serialized: String
        switch lineEnding {
        case .crLf:
            serialized = normalized.replacingOccurrences(of: "\n", with: "\r\n", options: .literal)
        case .cr:
            serialized = normalized.replacingOccurrences(of: "\n", with: "\r", options: .literal)
        case .lf:
            serialized = normalized
        }

        switch encoding {
        case .utf8:
            return Data(serialized.utf8)
        case .utf8WithBom:
            return Data([0xEF, 0xBB, 0xBF]) + Data(serialized.utf8)
        case .utf16LittleEndian:
            return Data([0xFF, 0xFE]) + (serialized.data(using: .utf16LittleEndian) ?? Data())
        }
    }

    public static func normalizeLineEndings(_ text: String) -> String {
        text.replacingOccurrences(of: "\r\n", with: "\n", options: .literal)
            .replacingOccurrences(of: "\r", with: "\n", options: .literal)
    }

    public static func detectLineEnding(_ text: String) -> LineEndingKind {
        var crlf = 0
        var lf = 0
        var cr = 0
        var previousWasCR = false

        for unit in text.utf16 {
            if unit == 0x0D {
                cr += 1
                previousWasCR = true
                continue
            }

            if unit == 0x0A {
                if previousWasCR {
                    cr -= 1
                    crlf += 1
                } else {
                    lf += 1
                }
            }
            previousWasCR = false
        }

        if crlf == 0 && lf == 0 && cr == 0 {
            return .lf
        }

        if crlf >= lf && crlf >= cr {
            return .crLf
        }

        return lf >= cr ? .lf : .cr
    }
}
