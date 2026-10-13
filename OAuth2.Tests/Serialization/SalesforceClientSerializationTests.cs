using System.Collections.Specialized;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using OAuth2.Client.Impl;
using OAuth2.Configuration;
using OAuth2.Infrastructure;
using OAuth2.Models;
using OAuth2.Tests.TestHelpers;
using RestSharp;

namespace OAuth2.Tests.Serialization
{
    [TestFixture]
    public class SalesforceClientSerializationTests
    {
        private TestableSalesforceClient _client = null!;

        [SetUp]
        public void SetUp()
        {
            _client = new TestableSalesforceClient(Substitute.For<IRequestFactory>(), Substitute.For<IClientConfiguration>());
        }

        [Test]
        public async Task GetUserInfoAsync_IdentityResponse_PreservesIdentityUrlAndBearerHeader()
        {
            // Arrange
            using var handler = new MockHttpMessageHandler();
            // Documented token-response fields; dummy token and the sourced fixture's identity URL.
            // https://developer.salesforce.com/docs/platform/mobile-sdk/guide/oauth-using-identity-urls.html
            handler.EnqueueResponse(/* lang=json */ """{"access_token":"test-token","token_type":"Bearer","id":"https://login.salesforce.com/id/some-org-id/some-user-id"}""");
            handler.EnqueueResponse(ProviderResponses.SalesforceIdentity);
            using var httpClient = new HttpClient(handler);
            using var restClient = new RestClient(httpClient, new RestClientOptions("https://login.salesforce.com"));
            var factory = Substitute.For<IRequestFactory>();
            factory.CreateClient("https://login.salesforce.com").Returns(restClient);
            factory.CreateRequest(Arg.Any<string>(), Arg.Any<Method>()).Returns(call =>
                new RestRequest(call.Arg<string>(), call.Arg<Method>()));
            var configuration = new ClientConfiguration
            {
                ClientId = "client-id", ClientSecret = "client-secret",
                RedirectUri = "https://app.example.com/callback"
            };
            var client = new SalesforceClient(factory, configuration);
            var parameters = new NameValueCollection { { "code", "code" }, { "state", "state" } };

            // Act
            var info = await client.GetUserInfoAsync(parameters);

            // Assert
            info.ProviderName.Should().Be("Salesforce");
            info.Id.Should().Be("https://login.salesforce.com/id/some-org-id/some-user-id");
            info.Email.Should().Be("user@example.com");
            info.EmailVerified.Should().BeTrue();
            info.ProviderData.Should().BeNull();
            client.SalesforceProfileUrl.Should().Be("https://login.salesforce.com/id/some-org-id/some-user-id");
            client.State.Should().Be("state");
            handler.SentRequests.Should().HaveCount(2);
            handler.SentRequests[0].RequestUri!.AbsoluteUri.Should().Be("https://login.salesforce.com/services/oauth2/token");
            handler.SentRequests[1].RequestUri!.AbsoluteUri.Should().Be("https://login.salesforce.com/id/some-org-id/some-user-id");
            handler.SentRequests[1].Headers.Authorization!.Scheme.Should().Be("Bearer");
            handler.SentRequests[1].Headers.Authorization!.Parameter.Should().Be("test-token");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ParseUserInfo_BooleanVerification_PreservesClaim(bool verified)
        {
            // Arrange
            var response = JsonNode.Parse(ProviderResponses.SalesforceIdentity)!.AsObject();
            response["email_verified"] = verified;
            var content = response.ToJsonString();

            // Act
            var info = _client.ParseUserInfo(content);

            // Assert
            info.Email.Should().Be("user@example.com");
            info.EmailVerified.Should().Be(verified);
            info.ProviderData.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_MissingEmail_ReturnsNullEmail()
        {
            // Arrange
            var response = JsonNode.Parse(ProviderResponses.SalesforceIdentity)!.AsObject();
            response.Remove("email");
            var content = response.ToJsonString();

            // Act
            var info = _client.ParseUserInfo(content);

            // Assert
            info.Email.Should().BeNull();
            info.EmailVerified.Should().BeTrue();
        }

        [Test]
        public void ParseUserInfo_MissingVerification_ReturnsNull()
        {
            // Arrange
            var response = JsonNode.Parse(ProviderResponses.SalesforceIdentity)!.AsObject();
            response.Remove("email_verified");
            var content = response.ToJsonString();

            // Act
            var info = _client.ParseUserInfo(content);

            // Assert
            info.Email.Should().Be("user@example.com");
            info.EmailVerified.Should().BeNull();
        }

        [TestCase(/* lang=json */ "null")]
        [TestCase(/* lang=json */ "\"true\"")]
        [TestCase(/* lang=json */ "1")]
        [TestCase(/* lang=json */ "{}")]
        [TestCase(/* lang=json */ "[]")]
        public void ParseUserInfo_NonBooleanVerification_ReturnsNull(string claim)
        {
            // Arrange
            // Deliberately invalid claim types, not successful API response examples.
            var response = JsonNode.Parse(ProviderResponses.SalesforceIdentity)!.AsObject();
            response["email_verified"] = JsonNode.Parse(claim);
            var content = response.ToJsonString();

            // Act
            var info = _client.ParseUserInfo(content);

            // Assert
            info.Id.Should().Be("https://login.salesforce.com/id/some-org-id/some-user-id");
            info.Email.Should().Be("user@example.com");
            info.EmailVerified.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_RepeatedResponses_DoesNotReuseVerification()
        {
            // Arrange
            var first = _client.ParseUserInfo(ProviderResponses.SalesforceIdentity);
            var response = JsonNode.Parse(ProviderResponses.SalesforceIdentity)!.AsObject();
            response.Remove("email_verified");
            var content = response.ToJsonString();

            // Act
            var second = _client.ParseUserInfo(content);

            // Assert
            first.EmailVerified.Should().BeTrue();
            second.EmailVerified.Should().BeNull();
            second.Should().NotBeSameAs(first);
        }

        [Test]
        public void ParseUserInfo_ValidContent_MapsPhotosToAvatarUri()
        {
            // Arrange
            var content = ProviderResponses.SalesforceIdentity;

            // Act
            var info = _client.ParseUserInfo(content);

            // Assert
            info.AvatarUri.Small.Should().Be("https://example.com/profilephoto/thumb");
            info.AvatarUri.Normal.Should().Be("https://example.com/profilephoto/full");
            info.AvatarUri.Large.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_ValidContent_ReturnsCorrectFields()
        {
            // Arrange
            var content = ProviderResponses.SalesforceIdentity;

            // Act
            var info = _client.ParseUserInfo(content);

            // Assert
            info.Id.Should().Be("https://login.salesforce.com/id/some-org-id/some-user-id");
            info.FirstName.Should().Be("Example");
            info.LastName.Should().Be("User");
            info.Email.Should().Be("user@example.com");
            info.EmailVerified.Should().BeTrue();
            info.ProviderData.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_ValidContent_SerializesToValidJson()
        {
            // Arrange
            var content = ProviderResponses.SalesforceIdentity;

            // Act
            var info = _client.ParseUserInfo(content);
            var json = JsonSerializer.Serialize(info);
            using var doc = JsonDocument.Parse(json);

            // Assert
            doc.RootElement.GetProperty("Id").GetString().Should().Be("https://login.salesforce.com/id/some-org-id/some-user-id");
            doc.RootElement.GetProperty("Email").GetString().Should().Be("user@example.com");
            doc.RootElement.GetProperty("EmailVerified").GetBoolean().Should().BeTrue();
            doc.RootElement.GetProperty("AvatarUri").GetProperty("Small").GetString().Should().Be("https://example.com/profilephoto/thumb");
        }

        private class TestableSalesforceClient : SalesforceClient
        {
            public TestableSalesforceClient(IRequestFactory factory, IClientConfiguration configuration)
                : base(factory, configuration) { }

            public new UserInfo ParseUserInfo(string content) => base.ParseUserInfo(content);
        }
    }
}
