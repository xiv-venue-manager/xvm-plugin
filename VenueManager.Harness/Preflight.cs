using System.Security.Cryptography;

namespace VenueManager.Harness;

public static class Preflight
{
    private const string Bindings = "Dalamud.Bindings.ImGui.dll";

    public static string? Check(string installedDalamudDir, string cacheRoot)
    {
        var installed = Path.Combine(installedDalamudDir, Bindings);
        var cached = Path.Combine(cacheRoot, "dalamud", Bindings);
        var native = Path.Combine(cacheRoot, "native", "libcimgui.so");

        foreach (var file in new[] { installed, cached, native })
        {
            if (!File.Exists(file)) return $"Missing {file}. Run the Aetherphone harness bootstrap.";
        }

        return Hash(installed) == Hash(cached)
            ? null
            : "The installed Dalamud ImGui bindings differ from the harness cache. Refresh the Aetherphone harness bootstrap, then retry.";
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
