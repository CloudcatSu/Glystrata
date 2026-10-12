// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "Glystrata",
    platforms: [
        // Observation (@Observable) sets the floor at macOS 14.
        .macOS(.v14)
    ],
    products: [
        .library(name: "GlystrataCore", targets: ["GlystrataCore"])
    ],
    dependencies: [
        .package(url: "https://github.com/swiftlang/swift-markdown.git", from: "0.9.0")
    ],
    targets: [
        .target(
            name: "GlystrataCore",
            dependencies: [.product(name: "Markdown", package: "swift-markdown")]),
        .testTarget(name: "GlystrataCoreTests", dependencies: ["GlystrataCore"])
    ]
)
