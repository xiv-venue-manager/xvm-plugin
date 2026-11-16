using System.Text.Json;
using System.Text.Json.Serialization;

namespace VenueManager.Harness;

public sealed class SessionStore
{
    private sealed record Session(
        [property: JsonPropertyName("secret")] string? Secret,
        [property: JsonPropertyName("person")] string? Person);

    public SessionStore(string path) => Path = path;

    public string Path { get; }

    public static SessionStore Default()
    {
        var cache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        if (string.IsNullOrEmpty(cache))
            cache = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");
        return new SessionStore(System.IO.Path.Combine(cache, "xvm-harness", "dev-session.json"));
    }

    public string? LoadSecret()
    {
        try
        {
            var secret = JsonSerializer.Deserialize<Session>(File.ReadAllText(Path))?.Secret;
            return string.IsNullOrEmpty(secret) ? null : secret;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save(string secret, string? personName)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!);
        File.Delete(Path);
        using var stream = new FileStream(Path, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
        });
        JsonSerializer.Serialize(stream, new Session(secret, personName));
    }

    public void Delete() => File.Delete(Path);
}
