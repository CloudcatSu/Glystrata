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
    targets: [
        .target(name: "GlystrataCore"),
        .testTarget(name: "GlystrataCoreTests", dependencies: ["GlystrataCore"])
    ]
)
