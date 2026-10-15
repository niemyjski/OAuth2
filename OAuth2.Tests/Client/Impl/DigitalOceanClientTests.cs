using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using OAuth2.Client;
using OAuth2.Client.Impl;
using OAuth2.Configuration;
using OAuth2.Infrastructure;
using OAuth2.Models;
using OAuth2.Tests.TestHelpers;
using RestSharp;

namespace OAuth2.Tests.Client.Impl
{
    [TestFixture]
    public class DigitalOceanClientTests
    {
        // Existing legacy regression fixture; retained to protect historical numeric uid handling.
        /* lang=json */
        private const string Content = "{\"access_token\":\"yada\",\"token_type\":\"bearer\",\"expires_in\":2592000,\"refresh_token\":\"yada\",\"scope\":\"read\",\"uid\":123456,\"info\":{\"name\":\"first.last\",\"email\":\"first.last@domain.com\"}}";

        // Current published token-response shape, checked 2026-10-13.
        // Tokens and email replaced with dummy values; UUID examples and field types retained.
        // https://docs.digitalocean.com/reference/api/oauth/#response
        /* lang=json */
        private const string CurrentContent = """
            {
              "access_token": "test-token",
              "token_type": "bearer",
              "expires_in": 2592000,
              "refresh_token": "test-refresh-token",
              "scope": "read",
              "info": {
                "name": "Sammy the Shark",
                "email": "sammy@example.com",
                "uuid": "EXAMPLE0-a636-11ec-9e9d-3381ceabe039",
                "team_uuid": "EXAMPLE1-a636-11ec-a6ac-1323bf96ef4d",
                "team_name": "My Team"
              }
            }
            """;

        private DigitalOceanClientDescendant _descendant = null!;

        [SetUp]
        public void SetUp()
        {
            _descendant = new DigitalOceanClientDescendant(Substitute.For<IRequestFactory>(), Substitute.For<IClientConfiguration>());
        }

        [Test]
        public void AccessCodeEndpoint_Default_ReturnsCorrectEndpoint()
        {
            // Arrange
            // The client is initialized in SetUp.

            // Act
            var endpoint = _descendant.GetAccessCodeServiceEndpoint();

            // Assert
            endpoint.BaseUri.Should().Be("https://cloud.digitalocean.com");
            endpoint.Resource.Should().Be("/v1/oauth/authorize");
        }

        [Test]
        public void AccessTokenEndpoint_Default_ReturnsCorrectEndpoint()
        {
            // Arrange
            // The client is initialized in SetUp.

            // Act
            var endpoint = _descendant.GetAccessTokenServiceEndpoint();

            // Assert
            endpoint.BaseUri.Should().Be("https://cloud.digitalocean.com");
            endpoint.Resource.Should().Be("/v1/oauth/token");
        }

        [Test]
        public async Task GetUserInfoAsync_DocumentedToken_UsesSingleTokenRequest()
        {
            // Arrange
            using var handler = new MockHttpMessageHandler();
            handler.EnqueueResponse(CurrentContent);
            using var httpClient = new HttpClient(handler);
            using var restClient = new RestClient(httpClient, new RestClientOptions("https://cloud.digitalocean.com"));
            var factory = Substitute.For<IRequestFactory>();
            factory.CreateClient("https://cloud.digitalocean.com").Returns(restClient);
            factory.CreateRequest(Arg.Any<string>(), Arg.Any<Method>()).Returns(call =>
                new RestRequest(call.Arg<string>(), call.Arg<Method>()));
            var configuration = new ClientConfiguration
            {
                ClientId = "client-id", ClientSecret = "client-secret",
                RedirectUri = "https://app.example.com/callback", Scope = "read"
            };
            var client = new DigitalOceanClient(factory, configuration);
            var parameters = new NameValueCollection { { "code", "code" }, { "state", "state" } };

            // Act
            var info = await client.GetUserInfoAsync(parameters);

            // Assert
            info.Id.Should().Be("EXAMPLE0-a636-11ec-9e9d-3381ceabe039");
            info.Email.Should().Be("sammy@example.com");
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().BeNull();
            client.State.Should().Be("state");
            handler.SentRequests.Should().ContainSingle();
            handler.SentRequests[0].RequestUri!.AbsoluteUri.Should().Be("https://cloud.digitalocean.com/v1/oauth/token");
        }

        [Test]
        public void ParseUserInfo_DocumentedToken_ReturnsUserUuid()
        {
            // Arrange
            var content = CurrentContent;

            // Act
            var info = _descendant.ParseUserInfo(content);

            // Assert
            info.Id.Should().Be("EXAMPLE0-a636-11ec-9e9d-3381ceabe039");
            info.FirstName.Should().Be("Sammy the Shark");
            info.LastName.Should().BeEmpty();
            info.Email.Should().Be("sammy@example.com");
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().BeNull();
        }

        [TestCase(/* lang=json */ "123456")]
        [TestCase(/* lang=json */ "\"123456\"")]
        public void ParseUserInfo_LegacyAndCurrentIdentifiers_PreservesLegacyId(string legacyId)
        {
            // Arrange
            // Compatibility variation: an existing uid must win over a different UUID.
            var response = JsonNode.Parse(CurrentContent)!.AsObject();
            response["uid"] = JsonNode.Parse(legacyId);
            var content = response.ToJsonString();

            // Act
            var info = _descendant.ParseUserInfo(content);

            // Assert
            info.Id.Should().Be("123456");
        }

        [Test]
        public void ParseUserInfo_TeamUuidWithoutUserIdentifier_ThrowsKeyNotFound()
        {
            // Arrange
            var response = JsonNode.Parse(CurrentContent)!.AsObject();
            response["info"]!.AsObject().Remove("uuid");
            var content = response.ToJsonString();

            // Act
            Action action = () => _descendant.ParseUserInfo(content);

            // Assert
            action.Should().Throw<KeyNotFoundException>();
        }

        [Test]
        public void ParseUserInfo_ValidContent_ReturnsCorrectFields()
        {
            // Arrange
            var content = Content;

            // Act
            var info = _descendant.ParseUserInfo(content);

            // Assert
            info.Id.Should().Be("123456");
            info.FirstName.Should().Be("first.last");
            info.LastName.Should().BeEmpty();
            info.Email.Should().Be("first.last@domain.com");
        }

        [Test]
        public void UserInfoEndpoint_Default_ThrowsNotImplemented()
        {
            // Arrange
            // User info is read from the token response, without a separate endpoint.

            // Act
            Action action = () => _descendant.GetUserInfoServiceEndpoint();

            // Assert
            action.Should().Throw<NotImplementedException>();
        }

        private class DigitalOceanClientDescendant : DigitalOceanClient
        {
            public DigitalOceanClientDescendant(IRequestFactory factory, IClientConfiguration configuration)
                : base(factory, configuration) { }

            public Endpoint GetAccessCodeServiceEndpoint() => AccessCodeServiceEndpoint;
            public Endpoint GetAccessTokenServiceEndpoint() => AccessTokenServiceEndpoint;
            public Endpoint GetUserInfoServiceEndpoint() => UserInfoServiceEndpoint;
            public new UserInfo ParseUserInfo(string content) => base.ParseUserInfo(content);
        }
    }
}
