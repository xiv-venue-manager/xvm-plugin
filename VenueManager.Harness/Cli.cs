namespace VenueManager.Harness;

public sealed class Cli
{
    private readonly PluginHost host;
    private readonly SessionStore? store;
    private readonly HttpMessageHandler? httpHandler;

    public Cli(PluginHost host, SessionStore? store = null, HttpMessageHandler? httpHandler = null)
    {
        this.host = host;
        this.store = store;
        this.httpHandler = httpHandler;
    }

    public void Run(TextReader input, TextWriter output)
    {
        string? line;
        while ((line = input.ReadLine()) is not null)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;

            var space = trimmed.IndexOf(' ');
            var verb = space < 0 ? trimmed : trimmed[..space];
            var rest = space < 0 ? "" : trimmed[(space + 1)..].Trim();

            if (verb == "quit") return;
            Handle(verb, rest, output);
        }
    }

    public static int ClampWait(int seconds) => Math.Clamp(seconds, 0, 60);

    private void Handle(string verb, string rest, TextWriter output)
    {
        switch (verb)
        {
            case "open":
                host.Open(rest.Length == 0 ? "Main" : rest);
                host.Step(4);
                output.WriteLine("ok");
                break;
            case "step":
                host.Step(int.TryParse(rest, out var frames) ? frames : 1);
                output.WriteLine("ok");
                break;
            case "wait":
                Thread.Sleep(TimeSpan.FromSeconds(ClampWait(int.TryParse(rest, out var seconds) ? seconds : 2)));
                host.Step(2);
                output.WriteLine("ok");
                break;
            case "tree":
                foreach (var row in host.Tree()) output.WriteLine(row);
                break;
            case "tabs":
                foreach (var name in host.Tabs) output.WriteLine(name);
                break;
            case "tab":
                if (host.SelectTab(rest))
                {
                    host.Step(3);
                    output.WriteLine($"tab: {rest}");
                }
                else
                {
                    output.WriteLine($"no tab: {rest}");
                }

                break;
            case "tap":
                output.WriteLine(host.Tap(rest) ? $"tapped: {rest}" : $"not found: {rest}");
                break;
            case "command":
                output.WriteLine(host.Command(rest) ? "ran" : "no handler");
                break;
            case "state":
                output.WriteLine($"tab: {host.CurrentTab}");
                output.WriteLine($"log lines: {host.Log.Count}");
                output.WriteLine($"linked: {(host.Linked ? "yes" : "no")}");
                output.WriteLine($"link rejected: {(host.LinkRejected ? "yes" : "no")}");
                break;
            case "link":
                Link(rest, output);
                break;
            case "unlink":
                store?.Delete();
                host.ClearSession();
                output.WriteLine("unlinked");
                break;
            case "shot":
                File.WriteAllBytes(rest, Png.Encode(host.Imgui.Screenshot(), host.Width, host.Height));
                output.WriteLine($"wrote {rest}");
                break;
            default:
                output.WriteLine($"unknown command: {verb}");
                break;
        }
    }

    private void Link(string code, TextWriter output)
    {
        using var http = new HttpClient(httpHandler ?? new HttpClientHandler(), httpHandler is null) { Timeout = TimeSpan.FromSeconds(15) };
        var result = XvmApiPairing.LinkAsync(http, host.ApiUrl, code).GetAwaiter().GetResult();
        if (!result.Success || string.IsNullOrEmpty(result.Secret))
        {
            output.WriteLine($"link failed: {result.Message}");
            return;
        }

        store?.Save(result.Secret, result.PersonName);
        host.ApplySession(result.Secret);
        output.WriteLine($"linked as {result.PersonName ?? "unknown"}{(store is null ? " (not saved)" : "")}");
    }
}
