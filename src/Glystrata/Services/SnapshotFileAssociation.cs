using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Glystrata.Services;

/// <summary>Registers a distinct Windows Explorer icon for snapshot sidecar files (".gss"), scoped to the
/// current user (HKEY_CURRENT_USER — no admin rights needed). Runs once per launch; cheap and idempotent.</summary>
public static class SnapshotFileAssociation
{
    public const string Extension = ".gss";
    private const string ProgId = "Glystrata.SnapshotFile";

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, int uFlags, nint dwItem1, nint dwItem2);

    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const int SHCNF_IDLIST = 0x0000;

    public static void EnsureRegistered()
    {
        try
        {
            var iconPath = ExtractIcon();
            if (iconPath is null)
            {
                return;
            }

            using var classesKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes");
            if (classesKey is null)
            {
                return;
            }

            // Don't steal an extension a different, already-installed program owns.
            using (var extensionKey = classesKey.OpenSubKey(Extension))
            {
                var existingProgId = extensionKey?.GetValue(null) as string;
                if (existingProgId is not null && !string.Equals(existingProgId, ProgId, StringComparison.Ordinal))
                {
                    return;
                }
            }

            using (var extensionKey = classesKey.CreateSubKey(Extension))
            {
                extensionKey.SetValue(null, ProgId);
            }

            using (var progIdKey = classesKey.CreateSubKey(ProgId))
            {
                progIdKey.SetValue(null, "Glystrata Snapshot");
                using var iconKey = progIdKey.CreateSubKey("DefaultIcon");
                iconKey.SetValue(null, $"{iconPath},0");
            }

            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, 0, 0);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Icon association is cosmetic; failing quietly here shouldn't block startup.
        }
    }

    private static string? ExtractIcon()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Glystrata",
            "icons");
        Directory.CreateDirectory(directory);
        var iconPath = Path.Combine(directory, "SnapshotFile.ico");

        if (File.Exists(iconPath))
        {
            return iconPath;
        }

        var uri = new Uri("pack://application:,,,/Resources/Icons/SnapshotFile.ico", UriKind.Absolute);
        var resource = Application.GetResourceStream(uri) ?? throw new FileNotFoundException("找不到快照圖示資源。");
        using (resource.Stream)
        using (var file = File.Create(iconPath))
        {
            resource.Stream.CopyTo(file);
        }

        return iconPath;
    }
}
