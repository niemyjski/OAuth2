using System.Collections.Specialized;
using System.Net.Http;
using System.Text.Json;
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
    public class GoogleClientTests
    {
        /* lang=json */
        private const string Content = "{\"email\":\"email\",\"given_name\":\"name\",\"family_name\":\"surname\",\"sub\":\"id\"}";
        /* lang=json */
        private const string ContentWithPicture = "{\"email\":\"email\",\"given_name\":\"name\",\"family_name\":\"surname\",\"sub\":\"id\",\"picture\":\"picture\"}";

        [Test]
        public void AccessCodeEndpoint_Default_ReturnsCorrectEndpoint()
        {
            // arrange
            var client = CreateClient();

            // act
            var endpoint = client.GetAccessCodeServiceEndpoint();

            // assert
            endpoint.BaseUri.Should().Be("https://accounts.google.com");
            endpoint.Resource.Should().Be("/o/oauth2/v2/auth");
        }

        [Test]
        public void AccessTokenEndpoint_Default_ReturnsCorrectEndpoint()
        {
            // arrange
            var client = CreateClient();

            // act
            var endpoint = client.GetAccessTokenServiceEndpoint();

            // assert
            endpoint.BaseUri.Should().Be("https://oauth2.googleapis.com");
            endpoint.Resource.Should().Be("/token");
        }

        [Test]
        public async Task GetUserInfoAsync_GoogleResponse_PreservesProviderAndClaims()
        {
            // arrange
            using var handler = new MockHttpMessageHandler();
            handler.EnqueueResponse(/* lang=json */ "{\"access_token\":\"token\",\"token_type\":\"Bearer\"}");
            handler.EnqueueResponse(/* lang=json */ "{\"sub\":\"user-123\",\"email\":\"user@example.com\",\"email_verified\":true,\"hd\":\"example.com\"}");
            using var httpClient = new HttpClient(handler);
            using var tokenClient = new RestClient(httpClient, new RestClientOptions("https://oauth2.googleapis.com"));
            using var userInfoClient = new RestClient(httpClient, new RestClientOptions("https://www.googleapis.com"));
            var factory = Substitute.For<IRequestFactory>();
            factory.CreateClient("https://oauth2.googleapis.com").Returns(tokenClient);
            factory.CreateClient("https://www.googleapis.com").Returns(userInfoClient);
            factory.CreateRequest(Arg.Any<string>(), Arg.Any<Method>()).Returns(call =>
                new RestRequest(call.Arg<string>(), call.Arg<Method>()));
            var configuration = new ClientConfiguration
            {
                ClientId = "client-id",
                ClientSecret = "client-secret",
                RedirectUri = "https://app.example.com/callback"
            };
            var client = new GoogleClient(factory, configuration);
            var parameters = new NameValueCollection { { "code", "code" }, { "state", "state" } };

            // act
            var info = await client.GetUserInfoAsync(parameters);

            // assert
            info.ProviderName.Should().Be("Google");
            info.Id.Should().Be("user-123");
            info.Email.Should().Be("user@example.com");
            info.EmailVerified.Should().BeTrue();
            info.ProviderData.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Key = "hd", Value = "example.com" });
            info.FirstName.Should().BeNull();
            info.LastName.Should().BeNull();
            client.State.Should().Be("state");
            handler.SentRequests.Should().HaveCount(2);
            handler.SentRequests[1].Headers.Authorization!.Scheme.Should().Be("Bearer");
            handler.SentRequests[1].Headers.Authorization!.Parameter.Should().Be("token");
        }

        [TestCase(/* lang=json */ "true", true)]
        [TestCase(/* lang=json */ "false", false)]
        public void ParseUserInfo_BooleanEmailVerifiedWithHostedDomain_PreservesClaim(string claim, bool expected)
        {
            // arrange
            var client = CreateClient();
            var content = ContentWithClaims($"\"email_verified\":{claim},\"hd\":\"example.com\"");

            // act
            var info = client.ParseUserInfo(content);

            // assert
            info.EmailVerified.Should().Be(expected);
            info.ProviderData.Should().Contain("hd", "example.com");
            info.Email.Should().Be("email");
        }

        [TestCase(/* lang=json */ "true", true)]
        [TestCase(/* lang=json */ "false", false)]
        public void ParseUserInfo_BooleanEmailVerifiedWithoutHostedDomain_PreservesClaim(string claim, bool expected)
        {
            // arrange
            var client = CreateClient();
            var content = ContentWithClaims($"\"email_verified\":{claim}");

            // act
            var info = client.ParseUserInfo(content);

            // assert
            info.EmailVerified.Should().Be(expected);
            info.ProviderData.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_ClaimsWithoutEmail_PreservesClaims()
        {
            // arrange
            var client = CreateClient();
            /* lang=json */
            const string content = "{\"given_name\":\"name\",\"family_name\":\"surname\",\"sub\":\"id\",\"email_verified\":true,\"hd\":\"example.com\"}";

            // act
            var info = client.ParseUserInfo(content);

            // assert
            info.Id.Should().Be("id");
            info.Email.Should().BeNull();
            info.EmailVerified.Should().BeTrue();
            info.ProviderData.Should().Contain("hd", "example.com");
        }

        [Test]
        public void ParseUserInfo_ClaimsWithoutPicture_PreservesClaims()
        {
            // arrange
            var client = CreateClient();
            /* lang=json */
            const string content = "{\"email\":\"email\",\"given_name\":\"name\",\"family_name\":\"surname\",\"sub\":\"id\",\"email_verified\":false,\"hd\":\"example.com\"}";

            // act
            var info = client.ParseUserInfo(content);

            // assert
            info.Email.Should().Be("email");
            info.EmailVerified.Should().BeFalse();
            info.ProviderData.Should().Contain("hd", "example.com");
            info.PhotoUri.Should().BeNull();
            info.AvatarUri.Small.Should().BeEmpty();
            info.AvatarUri.Large.Should().BeEmpty();
        }

        [TestCase("EMAIL_VERIFIED", "HD")]
        [TestCase("Email_Verified", "Hd")]
        public void ParseUserInfo_DifferentlyCasedClaims_DoesNotRecognizeClaims(string emailVerifiedName, string hostedDomainName)
        {
            // arrange
            var client = CreateClient();
            var content = ContentWithClaims($"\"{emailVerifiedName}\":true,\"{hostedDomainName}\":\"example.com\"");

            // act
            var info = client.ParseUserInfo(content);

            // assert
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().BeNull();
            info.Id.Should().Be("id");
            info.Email.Should().Be("email");
        }

        [TestCase("user@gmail.com")]
        [TestCase("user@example.com")]
        public void ParseUserInfo_EmailDomain_DoesNotInferClaims(string email)
        {
            // arrange
            var client = CreateClient();
            var content = ContentWithPicture.Replace("\"email\":\"email\"", $"\"email\":{JsonSerializer.Serialize(email)}");

            // act
            var info = client.ParseUserInfo(content);

            // assert
            info.Email.Should().Be(email);
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_MissingClaims_ReturnsNull()
        {
            // arrange
            var client = CreateClient();
            var content = ContentWithPicture;

            // act
            var info = client.ParseUserInfo(content);

            // assert
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().BeNull();
        }

        [TestCase(/* lang=json */ "{\"sub\":\"id\",\"email_verified\":true,\"hd\":\"example.com\"}", null, null)]
        [TestCase(/* lang=json */ "{\"sub\":\"id\",\"given_name\":\"name\",\"email_verified\":true,\"hd\":\"example.com\"}", "name", null)]
        [TestCase(/* lang=json */ "{\"sub\":\"id\",\"family_name\":\"surname\",\"email_verified\":true,\"hd\":\"example.com\"}", null, "surname")]
        public void ParseUserInfo_MissingProfileNames_PreservesIdentityAndClaims(string content, string? firstName, string? lastName)
        {
            // arrange
            var client = CreateClient();

            // act
            var info = client.ParseUserInfo(content);

            // assert
            info.Id.Should().Be("id");
            info.FirstName.Should().Be(firstName);
            info.LastName.Should().Be(lastName);
            info.EmailVerified.Should().BeTrue();
            info.ProviderData.Should().Contain("hd", "example.com");
        }

        [TestCase(/* lang=json */ "null")]
        [TestCase(/* lang=json */ "\"true\"")]
        [TestCase(/* lang=json */ "\"false\"")]
        [TestCase(/* lang=json */ "\"TRUE\"")]
        [TestCase(/* lang=json */ "\"\"")]
        [TestCase(/* lang=json */ "1")]
        [TestCase(/* lang=json */ "0")]
        [TestCase(/* lang=json */ "{}")]
        [TestCase(/* lang=json */ "[]")]
        public void ParseUserInfo_NonBooleanEmailVerified_ReturnsNull(string claim)
        {
            // arrange
            var client = CreateClient();
            var content = ContentWithClaims($"\"email_verified\":{claim},\"hd\":\"example.com\"");

            // act
            var info = client.ParseUserInfo(content);

            // assert
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().Contain("hd", "example.com");
            info.Email.Should().Be("email");
            info.PhotoUri.Should().Be("picture");
        }

        [TestCase(/* lang=json */ "null")]
        [TestCase(/* lang=json */ "true")]
        [TestCase(/* lang=json */ "false")]
        [TestCase(/* lang=json */ "1")]
        [TestCase(/* lang=json */ "{}")]
        [TestCase(/* lang=json */ "[]")]
        public void ParseUserInfo_NonStringHostedDomain_ReturnsNoProviderData(string claim)
        {
            // arrange
            var client = CreateClient();
            var content = ContentWithClaims($"\"email_verified\":true,\"hd\":{claim}");

            // act
            var info = client.ParseUserInfo(content);

            // assert
            info.ProviderData.Should().BeNull();
            info.EmailVerified.Should().BeTrue();
            info.Email.Should().Be("email");
        }

        [Test]
        public void ParseUserInfo_NoPicture_ReturnsNullPhoto()
        {
            // arrange
            var client = CreateClient();
            var content = Content;

            // act
            var info = client.ParseUserInfo(content);

            // assert
            info.PhotoUri.Should().BeNull();
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_RepeatedResponses_DoesNotShareProviderData()
        {
            // arrange
            var client = CreateClient();
            var content = ContentWithClaims("\"hd\":\"example.com\"");
            var first = client.ParseUserInfo(content);
            first.ProviderData!["hd"] = "changed.example";

            // act
            var second = client.ParseUserInfo(content);
            var withoutClaims = client.ParseUserInfo(ContentWithPicture);

            // assert
            second.ProviderData.Should().NotBeSameAs(first.ProviderData);
            second.ProviderData.Should().ContainSingle();
            second.ProviderData.Should().Contain("hd", "example.com");
            second.ProviderData.Should().NotContainKey("HD");
            withoutClaims.ProviderData.Should().BeNull();
        }

        [TestCase("example.com")]
        [TestCase("Example.COM")]
        [TestCase("")]
        [TestCase(" example.com ")]
        [TestCase("\"example.com\"")]
        [TestCase("example\\domain")]
        public void ParseUserInfo_StringHostedDomain_PreservesClaimWithoutInferringVerification(string domain)
        {
            // arrange
            var client = CreateClient();
            var content = ContentWithClaims($"\"hd\":{JsonSerializer.Serialize(domain)}");

            // act
            var info = client.ParseUserInfo(content);

            // assert
            info.ProviderData.Should().ContainSingle();
            info.ProviderData.Should().Contain("hd", domain);
            info.EmailVerified.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_UnknownResponseFields_DoesNotCopyUnselectedData()
        {
            // arrange
            var client = CreateClient();
            /* lang=json */
            const string content = "{\"sub\":\"id\",\"email_verified\":true,\"hd\":\"example.com\",\"access_token\":\"sensitive\",\"custom\":{\"value\":1}}";

            // act
            var info = client.ParseUserInfo(content);

            // assert
            info.EmailVerified.Should().BeTrue();
            info.ProviderData.Should().ContainSingle();
            info.ProviderData.Should().Contain("hd", "example.com");
        }

        [Test]
        public void ParseUserInfo_ValidContent_ReturnsCorrectFields()
        {
            // arrange
            var client = CreateClient();
            var content = ContentWithPicture;

            // act
            var info = client.ParseUserInfo(content);

            // assert
            info.Id.Should().Be("id");
            info.FirstName.Should().Be("name");
            info.LastName.Should().Be("surname");
            info.Email.Should().Be("email");
            info.PhotoUri.Should().Be("picture");
        }

        [Test]
        public void UserInfoEndpoint_Default_ReturnsCorrectEndpoint()
        {
            // arrange
            var client = CreateClient();

            // act
            var endpoint = client.GetUserInfoServiceEndpoint();

            // assert
            endpoint.BaseUri.Should().Be("https://www.googleapis.com");
            endpoint.Resource.Should().Be("/oauth2/v3/userinfo");
        }

        private static string ContentWithClaims(string claims)
        {
            return ContentWithPicture.Substring(0, ContentWithPicture.Length - 1) + "," + claims + "}";
        }

        private static GoogleClientDescendant CreateClient()
        {
            return new GoogleClientDescendant(Substitute.For<IRequestFactory>(), Substitute.For<IClientConfiguration>());
        }

        class GoogleClientDescendant : GoogleClient
        {
            public GoogleClientDescendant(IRequestFactory factory, IClientConfiguration configuration)
                : base(factory, configuration)
            {
            }

            public Endpoint GetAccessCodeServiceEndpoint()
            {
                return AccessCodeServiceEndpoint;
            }

            public Endpoint GetAccessTokenServiceEndpoint()
            {
                return AccessTokenServiceEndpoint;
            }

            public Endpoint GetUserInfoServiceEndpoint()
            {
                return UserInfoServiceEndpoint;
            }

            public new UserInfo ParseUserInfo(string content)
            {
                return base.ParseUserInfo(content);
            }
        }
    }
}
