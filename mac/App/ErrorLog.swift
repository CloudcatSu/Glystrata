import AppKit
import Darwin
import GlystrataCore

/// Appends to `errors.log` in the app's own folder whenever an error goes unhandled, as the Windows
/// build does. Unlike Windows, an exception AppKit caught is only logged and the app carries on
/// (Cloud's decision, 2026/11/25): an interface hiccup should not take unsaved text with it. A fatal
/// error still ends the app after its entry, and macOS still writes its own crash report.
enum ErrorLog {
    // Prepared at launch: a crashing process may not allocate, take locks or format dates.
    nonisolated(unsafe) private static var path: UnsafeMutablePointer<CChar>?
    nonisolated(unsafe) private static var utcOffset = 0
    nonisolated(unsafe) private static var line: UnsafeMutablePointer<UInt8>?
    nonisolated(unsafe) private static var frames: UnsafeMutablePointer<UnsafeMutableRawPointer?>?
    nonisolated(unsafe) private static var exceptionWritten = false
    private static let lineCapacity = 128
    private static let frameCapacity = 64

    static func install(directory: String) {
        try? FileManager.default.createDirectory(atPath: directory, withIntermediateDirectories: true)
        path = strdup((directory as NSString).appendingPathComponent("errors.log"))
        utcOffset = TimeZone.current.secondsFromGMT()
        line = .allocate(capacity: lineCapacity)
        frames = .allocate(capacity: frameCapacity)

        logReportedExceptions()
        NSSetUncaughtExceptionHandler { exception in
            ErrorLog.write(exception)
            // The runtime aborts next; that signal needs no second entry.
            ErrorLog.exceptionWritten = true
        }
        for signal in [SIGABRT, SIGBUS, SIGFPE, SIGILL, SIGSEGV, SIGTRAP] {
            Darwin.signal(signal, signalHandler)
        }
    }

    /// AppKit hands an exception it caught to `reportException(_:)`, then keeps running. That is the
    /// place Apple suggests a subclass override; SwiftUI runs its own NSApplication subclass and
    /// ignores NSPrincipalClass, so the method is wrapped instead, keeping AppKit's own handling.
    private static func logReportedExceptions() {
        let selector = #selector(NSApplication.reportException(_:))
        guard let method = class_getInstanceMethod(NSApplication.self, selector) else {
            return
        }
        typealias Report = @convention(c) (NSApplication, Selector, NSException) -> Void
        let original = unsafeBitCast(method_getImplementation(method), to: Report.self)
        let wrapper: @convention(block) (NSApplication, NSException) -> Void = { application, exception in
            write(exception)
            original(application, selector, exception)
        }
        method_setImplementation(method, imp_implementationWithBlock(wrapper))
    }

    private static func write(_ exception: NSException) {
        let stamp = ISO8601DateFormatter.string(
            from: Date(), timeZone: .current, formatOptions: [.withInternetDateTime, .withFractionalSeconds])
        let text = "[\(stamp)] Unhandled exception \(exception.name.rawValue): \(exception.reason ?? "")\n"
            + exception.callStackSymbols.joined(separator: "\n") + "\n"
        guard let path else {
            return
        }
        let file = open(path, O_WRONLY | O_APPEND | O_CREAT, 0o644)
        guard file >= 0 else {
            return
        }
        var bytes = Array(text.utf8)
        _ = Darwin.write(file, &bytes, bytes.count)
        close(file)
    }

    private static let signalHandler: @convention(c) (Int32) -> Void = { signal in
        if !ErrorLog.exceptionWritten {
            ErrorLog.write(signal: signal)
        }
        // Let the signal end the app as it would have, so the system's crash report is unchanged.
        Darwin.signal(signal, SIG_DFL)
        raise(signal)
    }

    /// Only async-signal-safe calls from here on.
    private static func write(signal: Int32) {
        guard let path, let line, let frames else {
            return
        }
        let file = open(path, O_WRONLY | O_APPEND | O_CREAT, 0o644)
        guard file >= 0 else {
            return
        }
        var text = ByteWriter(start: line, capacity: lineCapacity)
        let time = LogTimestamp(epochSeconds: Int(Darwin.time(nil)), utcOffsetSeconds: utcOffset)
        text.append("[")
        text.append(time.year, digits: 4)
        text.append("-")
        text.append(time.month, digits: 2)
        text.append("-")
        text.append(time.day, digits: 2)
        text.append("T")
        text.append(time.hour, digits: 2)
        text.append(":")
        text.append(time.minute, digits: 2)
        text.append(":")
        text.append(time.second, digits: 2)
        text.append(utcOffset < 0 ? "-" : "+")
        text.append(abs(utcOffset) / 3600, digits: 2)
        text.append(":")
        text.append(abs(utcOffset) % 3600 / 60, digits: 2)
        text.append("] Fatal signal ")
        text.append(Int(signal), digits: 1)
        text.append(" (")
        text.append(name(of: signal))
        text.append(")\n")
        _ = Darwin.write(file, line, text.count)
        let depth = backtrace(frames, Int32(frameCapacity))
        backtrace_symbols_fd(frames, depth, file)
        close(file)
    }

    private static func name(of signal: Int32) -> StaticString {
        switch signal {
        case SIGABRT: return "SIGABRT"
        case SIGBUS: return "SIGBUS"
        case SIGFPE: return "SIGFPE"
        case SIGILL: return "SIGILL"
        case SIGSEGV: return "SIGSEGV"
        case SIGTRAP: return "SIGTRAP"
        default: return "signal"
        }
    }
}

/// Fills a buffer prepared in advance, without allocating.
private struct ByteWriter {
    let start: UnsafeMutablePointer<UInt8>
    let capacity: Int
    private(set) var count = 0

    init(start: UnsafeMutablePointer<UInt8>, capacity: Int) {
        self.start = start
        self.capacity = capacity
    }

    mutating func append(_ text: StaticString) {
        for index in 0..<text.utf8CodeUnitCount where count < capacity {
            start[count] = text.utf8Start[index]
            count += 1
        }
    }

    /// Zero-padded to at least `digits`.
    mutating func append(_ number: Int, digits: Int) {
        var value = max(number, 0)
        var width = 1
        var scale = 1
        while value / scale >= 10 {
            scale *= 10
            width += 1
        }
        for _ in width..<max(digits, width) where count < capacity {
            start[count] = UInt8(ascii: "0")
            count += 1
        }
        while scale > 0, count < capacity {
            start[count] = UInt8(ascii: "0") + UInt8(value / scale)
            value %= scale
            scale /= 10
            count += 1
        }
    }
}
