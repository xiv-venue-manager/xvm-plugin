namespace VenueManager.Harness;

public static class Program
{
    public static int Main(string[] args)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var problem = Preflight.Check(
            Environment.GetEnvironmentVariable("DALAMUD_HOME") ?? Path.Combine(home, ".xlcore/dalamud/Hooks/dev"),
            Environment.GetEnvironmentVariable("AETHERPHONE_HARNESS_CACHE") ?? Path.Combine(home, ".aetherphone-harness"));
        if (problem is not null)
        {
            Console.Error.WriteLine(problem);
            return 1;
        }

        var url = Environment.GetEnvironmentVariable("XVM_HARNESS_URL");
        var secret = Environment.GetEnvironmentVariable("XVM_HARNESS_SECRET");
        var config = Environment.GetEnvironmentVariable("XVM_HARNESS_CONFIG");
        if (!string.IsNullOrEmpty(config) && (!string.IsNullOrEmpty(url) || !string.IsNullOrEmpty(secret)))
        {
            Console.Error.WriteLine("XVM_HARNESS_CONFIG cannot be combined with XVM_HARNESS_URL or XVM_HARNESS_SECRET.");
            return 1;
        }

        var options = new HarnessOptions(url, secret, string.IsNullOrEmpty(config) ? null : config);

        using var host = PluginHost.Start(options);
        new Cli(host).Run(Console.In, Console.Out);
        return 0;
    }
}
