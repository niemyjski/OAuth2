using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using NSubstitute;
using NUnit.Framework;
using OAuth2.Configuration;
using OAuth2.Infrastructure;
using RestSharp;

namespace OAuth2.Tests.TestHelpers;

public abstract class OAuth2ClientTestBase
{
    private readonly List<RestClient> _clients = new();
    private HttpClient _httpClient = null!;

    private protected MockHttpMessageHandler Handler { get; private set; } = null!;
    protected IRequestFactory Factory { get; private set; } = null!;
    protected ClientConfiguration Configuration { get; private set; } = null!;

    [SetUp]
    public virtual void SetUp()
    {
        Handler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(Handler, disposeHandler: false);
        Factory = Substitute.For<IRequestFactory>();
        Factory.CreateRequest(Arg.Any<string>(), Arg.Any<Method>()).Returns(call =>
            new RestRequest(call.Arg<string>(), call.Arg<Method>()));
        Configuration = new ClientConfiguration
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            RedirectUri = "https://app.example.com/callback"
        };
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var client in _clients)
            client.Dispose();

        _clients.Clear();
        _httpClient?.Dispose();
        Handler?.Dispose();
    }

    protected void ConfigureEndpoints(params string[] baseUris)
    {
        foreach (var baseUri in baseUris.Distinct())
        {
            var client = new RestClient(_httpClient, new RestClientOptions(baseUri));
            _clients.Add(client);
            Factory.CreateClient(baseUri).Returns(client);
        }
    }
}
