import Foundation
import Testing
@testable import GlystrataCore

@Suite("Settings form and file")
struct SettingsFormTests {
    @Test("hex colours are normalised the way the Windows parser does")
    func hexNormalize() {
        #expect(HexColor.normalize("#abcdef") == "#ABCDEF")
        #expect(HexColor.normalize(" #1f2328 ") == "#1F2328")
        #expect(HexColor.normalize("#f80") == "#FF8800")
        #expect(HexColor.normalize("#80112233") == "#112233")
        #expect(HexColor.normalize("#8123") == "#112233")
        #expect(HexColor.normalize("112233") == nil)
        #expect(HexColor.normalize("#12345") == nil)
        #expect(HexColor.normalize("#GGGGGG") == nil)
        #expect(HexColor.normalize("") == nil)
        #expect(HexColor.normalize(nil) == nil)
    }

    @Test("an untouched form commits to the same settings")
    func roundTrip() throws {
        let form = SettingsForm(settings: AppSettings(), systemKind: .light)
        let committed = try form.commit().get()
        #expect(committed.snapshotIntervalMinutes == 5)
        #expect(committed.previewTypography == PreviewTypography())
        #expect(committed.groupColors == GroupColorPalette.defaults)
        #expect(abs(committed.groupSelectionTint - 0.14) < 1e-9)
        #expect(committed.lightEditorPalette.colors.mapValues { $0.uppercased() } == EditorColorPalette.lightDefault.colors.mapValues { $0.uppercased() })
    }

    @Test("validation reports the first bad field in the Windows order")
    func validation() {
        var form = SettingsForm(settings: AppSettings(), systemKind: .light)
        form.snapshotInterval = "abc"
        form.editorColors["plain"] = "nope"
        #expect(form.commit() == .failure(.invalidInteger))

        form.snapshotInterval = "5"
        form.typography["h1"] = "0"
        #expect(form.commit() == .failure(.invalidNumber))
        form.typography["h1"] = "nan"
        #expect(form.commit() == .failure(.invalidNumber))

        form.typography["h1"] = "32"
        #expect(form.commit() == .failure(.invalidColor("nope")))

        form.editorColors["plain"] = "#000"
        form.groupColors[1] = "green"
        #expect(form.commit() == .failure(.invalidColor("green")))
    }

    @Test("out-of-range integers are clamped, not rejected")
    func clamping() throws {
        var form = SettingsForm(settings: AppSettings(), systemKind: .light)
        form.snapshotInterval = "500"
        form.maxSnapshots = "0"
        form.tintPercent = 50
        let committed = try form.commit().get()
        #expect(committed.snapshotIntervalMinutes == 120)
        #expect(committed.maxSnapshotsPerFile == 1)
        #expect(committed.groupSelectionTint == 0.5)
    }

    @Test("colour fields edit the palette of the resolved theme and survive a theme switch")
    func themeSwitch() throws {
        var form = SettingsForm(settings: AppSettings(), systemKind: .light)
        #expect(form.editingTheme == .light)
        form.editorColors["plain"] = "#112233"
        form.editorColors["link"] = "oops"

        form.setTheme(.dark)
        #expect(form.editorColors["plain"] == EditorColorPalette.darkDefault.colors["plain"])
        form.editorColors["plain"] = "#445566"

        form.setTheme(.light)
        #expect(form.editorColors["plain"] == "#112233")
        // The half-typed value was not stored, so the saved colour came back.
        #expect(form.editorColors["link"] == EditorColorPalette.lightDefault.colors["link"])

        form.setTheme(.dark)
        let committed = try form.commit().get()
        #expect(committed.theme == .dark)
        #expect(committed.darkEditorPalette.colors["plain"] == "#445566")
        #expect(committed.lightEditorPalette.colors["plain"] == "#112233")
    }

    @Test("switching between system and the theme it resolves to keeps the typed colours")
    func systemToSameTheme() {
        var form = SettingsForm(settings: AppSettings(), systemKind: .dark)
        form.editorColors["plain"] = "oops"
        form.setTheme(.dark)
        #expect(form.editorColors["plain"] == "oops")
    }

    @Test("the last group colour cannot be removed")
    func groupColors() {
        var form = SettingsForm(settings: AppSettings(), systemKind: .light)
        form.addGroupColor()
        #expect(form.groupColors.count == 6)
        #expect(form.groupColors.last == SettingsForm.newGroupColor)
        for _ in 0..<10 {
            form.removeGroupColor(at: 0)
        }
        #expect(form.groupColors.count == 1)
    }

    @Test("an exported file imports back unchanged")
    func exportImport() throws {
        var settings = AppSettings()
        settings.theme = .dark
        settings.language = .english
        settings.groupColors = ["#112233", "#445566"]
        settings.snapshotIntervalMinutes = 30
        let data = try SettingsFile.export(settings)
        let text = String(decoding: data, as: UTF8.self)
        #expect(text.contains("\"kind\" : \"glystrata-settings\""))
        #expect(text.contains("\"theme\" : 1"))
        #expect(try SettingsFile.import(data) == settings)
    }

    @Test("import rejects files that are not settings")
    func importRejects() {
        func attempt(_ json: String) -> SettingsImportError? {
            do {
                _ = try SettingsFile.import(Data(json.utf8))
                return nil
            } catch {
                return error
            }
        }

        #expect(attempt("[1,2]") == .invalid)
        #expect(attempt("not json") == .invalid)
        #expect(attempt("{\"kind\":\"glystrata-settings\",\"snapshots\":[]}") == .notSettings)
        #expect(attempt("{\"theme\":1}") == .notSettings)
        #expect(attempt("{\"kind\":\"other\"}") == .notSettings)
        #expect(attempt("{\"kind\":5}") == .invalid)
        #expect(attempt("{\"kind\":\"glystrata-settings\",\"schemaVersion\":2}") == .tooNew)
        #expect(attempt("{\"kind\":\"glystrata-settings\",\"theme\":\"dark\"}") == .invalid)
        #expect(attempt("{\"kind\":\"glystrata-settings\"}") == nil)
    }

    @Test("import accepts comments and trailing commas, and clamps values")
    func importLenient() throws {
        let json = """
        {
          // exported by hand
          "kind": "glystrata-settings",
          "snapshotIntervalMinutes": 999,
        }
        """
        let settings = try SettingsFile.import(Data(json.utf8))
        #expect(settings.snapshotIntervalMinutes == 120)
    }
}
