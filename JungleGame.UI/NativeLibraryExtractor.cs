using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JungleGame.UI;

/// <summary>
/// Extracts the embedded WPF native libraries to a per-version temp directory and
/// points the native loader at them, so the published output is a single EXE.
/// Runs via a module initializer, i.e. before Main and before any WPF component
/// is created.
/// </summary>
internal static class NativeLibraryExtractor
{
    [ModuleInitializer]
    internal static void Initialize() => EnsureExtracted();

    // Load order: dependencies first (vcruntime is a dependency of the others)
    private static readonly string[] LibraryNames =
    {
        "vcruntime140_cor3.dll",
        "D3DCompiler_47_cor3.dll",
        "PenImc_cor3.dll",
        "wpfgfx_cor3.dll",
        "PresentationNative_cor3.dll"
    };

    private static readonly string TargetDirectory = Path.Combine(
        Path.GetTempPath(), "JungleGame", typeof(NativeLibraryExtractor).Assembly.GetName().Version?.ToString() ?? "1.0");

    public static void EnsureExtracted()
    {
        foreach (var name in LibraryNames)
            EnsureLibrary(name);

        // Pre-load the libraries by full path: WPF's own loader does not honor
        // SetDllDirectory, but LoadLibrary calls by name find already-loaded modules.
        foreach (var name in LibraryNames)
        {
            if (LoadLibrary(Path.Combine(TargetDirectory, name)) == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    $"Failed to pre-load native library {name} (Win32 error {Marshal.GetLastWin32Error()}).");
            }
        }

        SetDllDirectory(TargetDirectory);
    }

    private static void EnsureLibrary(string fileName)
    {
        var targetPath = Path.Combine(TargetDirectory, fileName);
        if (File.Exists(targetPath) && new FileInfo(targetPath).Length > 0)
            return;

        Directory.CreateDirectory(TargetDirectory);

        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("native." + fileName)
            ?? throw new InvalidOperationException($"Embedded native library {fileName} not found.");

        // Concurrent launches may race here; whichever writes first wins
        try
        {
            using var file = File.Create(targetPath);
            stream.CopyTo(file);
        }
        catch (IOException)
        {
            if (!File.Exists(targetPath))
                throw;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);
}
