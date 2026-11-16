namespace VenueManager.Harness;

public sealed class Cli
{
    private readonly PluginHost host;

    public Cli(PluginHost host) => this.host = host;

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
}
