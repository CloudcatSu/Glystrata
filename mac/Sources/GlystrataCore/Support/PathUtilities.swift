import Foundation

enum PathUtilities {
    /// Absolute path with "." and ".." collapsed. Purely lexical: symlinks are left alone so the
    /// path the user opened is the path we show and store.
    static func fullPath(_ path: String) -> String {
        URL(fileURLWithPath: path).standardizedFileURL.path
    }

    /// Key for "is this the same file?". Case-folded because the default macOS volume format is
    /// case-insensitive, and precomposed because macOS APIs hand back the same file name in either
    /// composed or decomposed form depending on where the path came from.
    static func canonicalKey(_ path: String) -> String {
        fullPath(path).precomposedStringWithCanonicalMapping.uppercased()
    }

    static func fileName(_ path: String) -> String {
        (path as NSString).lastPathComponent
    }

    static func directory(_ path: String) -> String {
        (path as NSString).deletingLastPathComponent
    }

    static func fileExists(_ path: String) -> Bool {
        var isDirectory: ObjCBool = false
        return FileManager.default.fileExists(atPath: path, isDirectory: &isDirectory) && !isDirectory.boolValue
    }

    static func directoryExists(_ path: String) -> Bool {
        var isDirectory: ObjCBool = false
        return FileManager.default.fileExists(atPath: path, isDirectory: &isDirectory) && isDirectory.boolValue
    }
}

extension String {
    /// Length in UTF-16 code units — the unit NSTextView ranges, caret offsets and the Windows
    /// build's string indices are all measured in.
    var utf16Length: Int { utf16.count }

    /// Code-unit equality. Swift's `==` treats canonically equivalent strings as equal, which would
    /// make a composed-to-decomposed edit look like "nothing changed".
    func ordinalEquals(_ other: String) -> Bool {
        utf8.elementsEqual(other.utf8)
    }
}
