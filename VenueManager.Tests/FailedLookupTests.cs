using System.Net;
using System.Net.Sockets;
using System.Text;
using VenueManager;
using Xunit;

public class FailedLookupTests
{
    private const string NothingListening = "http://127.0.0.1:1";

    private static XIVAppApiClient LinkedTo(string url)
    {
        var client = new XIVAppApiClient();
        client.ConfigureXvm(url, "secret");
        return client;
    }

    [Fact]
    public async Task A_failed_active_event_lookup_is_null_not_no_event()
    {
        Assert.Null(await LinkedTo(NothingListening).Venue.GetActiveEventAsync("v1"));
    }

    [Fact]
    public async Task A_failed_rooms_fetch_is_null_not_an_empty_list()
    {
        Assert.Null(await LinkedTo(NothingListening).Venue.GetRoomsAsync("v1"));
    }

    [Fact]
    public async Task Rooms_are_null_when_the_account_is_not_linked()
    {
        Assert.Null(await new XIVAppApiClient().Venue.GetRoomsAsync("v1"));
    }

    [Fact]
    public async Task A_server_error_is_a_failure_too()
    {
        using var api = new LocalApi(HttpStatusCode.InternalServerError, "{}");
        var client = LinkedTo(api.Url);
        Assert.Null(await client.Venue.GetActiveEventAsync("v1"));
        Assert.Null(await client.Venue.GetRoomsAsync("v1"));
    }

    [Fact]
    public async Task A_venue_with_no_rooms_is_an_empty_list()
    {
        using var api = new LocalApi(HttpStatusCode.OK, "[]");
        var rooms = await LinkedTo(api.Url).Venue.GetRoomsAsync("v1");
        Assert.NotNull(rooms);
        Assert.Empty(rooms);
    }

    [Fact]
    public async Task No_event_running_is_an_answer_not_a_failure()
    {
        using var api = new LocalApi(HttpStatusCode.OK, "null");
        var active = await LinkedTo(api.Url).Venue.GetActiveEventAsync("v1");
        Assert.NotNull(active);
        Assert.False(active.Active);
    }

    private sealed class LocalApi : IDisposable
    {
        private readonly HttpListener listener = new();

        public string Url { get; }

        public LocalApi(HttpStatusCode status, string body)
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            Url = $"http://127.0.0.1:{port}";
            listener.Prefixes.Add(Url + "/");
            listener.Start();
            _ = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    HttpListenerContext context;
                    try
                    {
                        context = await listener.GetContextAsync();
                    }
                    catch (Exception)
                    {
                        return;
                    }
                    var bytes = Encoding.UTF8.GetBytes(body);
                    context.Response.StatusCode = (int)status;
                    context.Response.ContentType = "application/json";
                    context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                    context.Response.Close();
                }
            });
        }

        public void Dispose() => listener.Close();
    }
}
