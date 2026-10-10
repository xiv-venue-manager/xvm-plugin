using System.Net;
using System.Text;
using VenueManager;
using Xunit;

public class XvmApiPairingTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses = new();
        public List<(HttpMethod Method, string Url, string? Authorization, string Body)> Requests { get; } = new();

        public FakeHandler Respond(HttpStatusCode status, string json = "{}")
        {
            _responses.Enqueue(() => new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
            return this;
        }

        public FakeHandler Repeat(HttpStatusCode status, int times, string json = "{}")
        {
            for (var i = 0; i < times; i++) Respond(status, json);
            return this;
        }

        public FakeHandler Throw()
        {
            _responses.Enqueue(() => throw new HttpRequestException("no route"));
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method, request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(), body));
            return _responses.Dequeue()();
        }
    }

    private const string Exchanged = "{\"secret\":\"s3cret\",\"credential\":{\"id\":7}}";
    private const string Me = "{\"kind\":\"api_key\",\"client\":\"plugin\",\"name\":\"plugin key\",\"venue_narrow\":null,\"person\":{\"id\":3,\"display_name\":\"Ehno\"},\"memberships\":[]}";

    private static (HttpClient Http, FakeHandler Handler) Client(FakeHandler handler) => (new HttpClient(handler), handler);

    [Theory]
    [InlineData(" abcd-1234 ", "ABCD-1234")]
    [InlineData("ab cd 12 34", "ABCD-1234")]
    [InlineData("ABCD1234", "ABCD-1234")]
    [InlineData("abc", "ABC")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeCode_upper_cases_and_puts_the_hyphen_xvm_api_stores_after_four_characters(string? raw, string expected)
    {
        Assert.Equal(expected, XvmApiPairing.NormalizeCode(raw));
    }

    [Fact]
    public async Task Links_by_exchanging_the_code_then_verifying_it_with_me()
    {
        var (http, handler) = Client(new FakeHandler().Respond(HttpStatusCode.Created, Exchanged).Respond(HttpStatusCode.OK, Me));

        var result = await XvmApiPairing.LinkAsync(http, "https://api.test/", " abcd-1234 ");

        Assert.True(result.Success);
        Assert.Equal("s3cret", result.Secret);
        Assert.Equal("Ehno", result.PersonName);
        Assert.Equal("https://api.test/pairing/exchange", handler.Requests[0].Url);
        Assert.Contains("\"code\":\"ABCD-1234\"", handler.Requests[0].Body);
        Assert.Contains("\"client\":\"plugin\"", handler.Requests[0].Body);
        Assert.Equal("https://api.test/me", handler.Requests[1].Url);
        Assert.Equal("Bearer s3cret", handler.Requests[1].Authorization);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, PairingOutcome.UnknownCode)]
    [InlineData(HttpStatusCode.Conflict, PairingOutcome.CodeUsedOrExpired)]
    [InlineData(HttpStatusCode.TooManyRequests, PairingOutcome.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, PairingOutcome.Failed)]
    public async Task Maps_exchange_failures_to_an_outcome_and_stores_no_secret(HttpStatusCode status, PairingOutcome expected)
    {
        var (http, handler) = Client(new FakeHandler().Respond(status));

        var result = await XvmApiPairing.LinkAsync(http, "https://api.test", "ABCD1234");

        Assert.Equal(expected, result.Outcome);
        Assert.Null(result.Secret);
        Assert.False(string.IsNullOrEmpty(result.Message));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Reports_an_unreachable_server()
    {
        var (http, _) = Client(new FakeHandler().Throw());
        var result = await XvmApiPairing.LinkAsync(http, "https://api.test", "ABCD1234");
        Assert.Equal(PairingOutcome.Unreachable, result.Outcome);
    }

    [Fact]
    public async Task Does_not_keep_a_secret_that_me_rejects()
    {
        var (http, _) = Client(new FakeHandler().Respond(HttpStatusCode.Created, Exchanged).Repeat(HttpStatusCode.Unauthorized, 6));
        var result = await XvmApiPairing.LinkAsync(http, "https://api.test", "ABCD1234", TimeSpan.Zero);
        Assert.Equal(PairingOutcome.Failed, result.Outcome);
        Assert.Null(result.Secret);
    }

    [Fact]
    public async Task Retries_me_while_the_server_has_not_yet_committed_the_new_credential()
    {
        var (http, handler) = Client(new FakeHandler()
            .Respond(HttpStatusCode.Created, Exchanged)
            .Respond(HttpStatusCode.Unauthorized)
            .Respond(HttpStatusCode.Unauthorized)
            .Respond(HttpStatusCode.OK, Me));

        var result = await XvmApiPairing.LinkAsync(http, "https://api.test", "ABCD1234", TimeSpan.Zero);

        Assert.True(result.Success);
        Assert.Equal("s3cret", result.Secret);
        Assert.Equal(4, handler.Requests.Count);
        Assert.All(handler.Requests.Skip(1), r => Assert.Equal("Bearer s3cret", r.Authorization));
    }

    [Fact]
    public async Task Gives_up_on_me_after_six_attempts()
    {
        var (http, handler) = Client(new FakeHandler().Respond(HttpStatusCode.Created, Exchanged).Repeat(HttpStatusCode.Unauthorized, 10));

        var result = await XvmApiPairing.LinkAsync(http, "https://api.test", "ABCD1234", TimeSpan.Zero);

        Assert.Equal(PairingOutcome.Failed, result.Outcome);
        Assert.Equal(7, handler.Requests.Count);
    }

    [Fact]
    public async Task Does_not_retry_me_for_other_failures()
    {
        var (http, handler) = Client(new FakeHandler().Respond(HttpStatusCode.Created, Exchanged).Respond(HttpStatusCode.InternalServerError));

        var result = await XvmApiPairing.LinkAsync(http, "https://api.test", "ABCD1234", TimeSpan.Zero);

        Assert.Equal(PairingOutcome.Failed, result.Outcome);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Explains_a_rejected_me_with_the_status_the_servers_reason_and_the_secret_length_but_never_the_secret()
    {
        var reason = "{\"detail\":\"Invalid credential\"}";
        var (http, _) = Client(new FakeHandler().Respond(HttpStatusCode.Created, Exchanged).Repeat(HttpStatusCode.Unauthorized, 6, reason));

        var result = await XvmApiPairing.LinkAsync(http, "https://api.test", "ABCD1234", TimeSpan.Zero);

        Assert.Contains("/me", result.Detail);
        Assert.Contains("401", result.Detail);
        Assert.Contains("Invalid credential", result.Detail);
        Assert.Contains("secret length 6", result.Detail);
        Assert.DoesNotContain("s3cret", result.Detail);
    }

    [Fact]
    public async Task Explains_an_unexpected_exchange_failure_with_its_status_and_reason()
    {
        var (http, _) = Client(new FakeHandler().Respond(HttpStatusCode.InternalServerError, "{\"detail\":\"boom\"}"));
        var result = await XvmApiPairing.LinkAsync(http, "https://api.test", "ABCD1234");
        Assert.Contains("exchange", result.Detail);
        Assert.Contains("500", result.Detail);
        Assert.Contains("boom", result.Detail);
    }

    [Fact]
    public async Task An_empty_code_is_unknown_and_sends_nothing()
    {
        var (http, handler) = Client(new FakeHandler());
        var result = await XvmApiPairing.LinkAsync(http, "https://api.test", "  ");
        Assert.Equal(PairingOutcome.UnknownCode, result.Outcome);
        Assert.Empty(handler.Requests);
    }
}
