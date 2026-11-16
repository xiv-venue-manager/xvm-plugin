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

        var store = SessionStore.Default();
        var code = Environment.GetEnvironmentVariable("XVM_HARNESS_PAIRING_CODE");
        if (!string.IsNullOrEmpty(code))
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var result = XvmApiPairing.LinkAsync(http, XvmApiPairing.DevUrl, code).GetAwaiter().GetResult();
            if (!result.Success || string.IsNullOrEmpty(result.Secret))
            {
                Console.Error.WriteLine($"link failed: {result.Message}");
                return 1;
            }

            store.Save(result.Secret, result.PersonName);
        }

        var secret = store.LoadSecret();
        if (secret is null)
            Console.Error.WriteLine("Not linked. Generate a link code at https://dev.xivvenuemanager.com/dashboard/api-keys, then type: link <code>");

        using var host = PluginHost.Start(secret is null ? new HarnessOptions() : new HarnessOptions(XvmApiPairing.DevUrl, secret));
        new Cli(host, store).Run(Console.In, Console.Out);
        return 0;
    }
}
