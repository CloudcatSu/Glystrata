import Foundation

/// A scratch directory that is removed when the value goes out of scope.
final class TemporaryDirectory {
    let path: String

    init() throws {
        let url = FileManager.default.temporaryDirectory.appending(path: "Glystrata.Tests-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        path = url.path
    }

    deinit {
        try? FileManager.default.removeItem(atPath: path)
    }

    func file(_ name: String) -> String {
        (path as NSString).appendingPathComponent(name)
    }

    @discardableResult
    func write(_ text: String, to name: String) throws -> String {
        let target = file(name)
        try Data(text.utf8).write(to: URL(fileURLWithPath: target))
        return target
    }
}

func isHidden(_ path: String) -> Bool {
    (try? URL(fileURLWithPath: path).resourceValues(forKeys: [.isHiddenKey]).isHidden) ?? false
}

func setHidden(_ path: String, _ hidden: Bool) throws {
    var url = URL(fileURLWithPath: path)
    var values = URLResourceValues()
    values.isHidden = hidden
    try url.setResourceValues(values)
}

func readText(_ path: String) throws -> String {
    try String(contentsOfFile: path, encoding: .utf8)
}
