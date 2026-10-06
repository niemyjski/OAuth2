using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using OAuth2.Client.Impl;
using OAuth2.Configuration;
using OAuth2.Infrastructure;
using OAuth2.Tests.TestHelpers;
using RestSharp;

namespace OAuth2.Tests.Client.Impl
{
    [TestFixture]
    public class GitHubEmailVerificationTests
    {
        /* lang=json */
        private const string PrivateProfile = "{\"id\":123,\"login\":\"test\",\"name\":\"John Doe\",\"email\":null,\"avatar_url\":\"https://example.com/avatar\"}";
        /* lang=json */
        private const string TokenResponse = "{\"access_token\":\"token\",\"token_type\":\"Bearer\"}";

        private MockHttpMessageHandler _handler = null!;
        private HttpClient _httpClient = null!;
        private RestClient _tokenClient = null!;
        private RestClient _apiClient = null!;
        private TestableGitHubClient _client = null!;

        [SetUp]
        public void SetUp()
        {
            _handler = new MockHttpMessageHandler();
            _httpClient = new HttpClient(_handler, disposeHandler: false);
            _tokenClient = new RestClient(_httpClient, new RestClientOptions("https://github.com"));
            _apiClient = new RestClient(_httpClient, new RestClientOptions("https://api.github.com"));
            var factory = Substitute.For<IRequestFactory>();
            factory.CreateClient("https://github.com").Returns(_tokenClient);
            factory.CreateClient("https://api.github.com").Returns(_apiClient);
            factory.CreateRequest(Arg.Any<string>(), Arg.Any<Method>()).Returns(call =>
                new RestRequest(call.Arg<string>(), call.Arg<Method>()));
            _client = new TestableGitHubClient(factory, new ClientConfiguration
            {
                ClientId = "client-id",
                ClientSecret = "client-secret",
                RedirectUri = "https://app.example.com/callback"
            });
        }

        [TestCase(/* lang=json */ "[]", null, null)]
        [TestCase(/* lang=json */ "null", null, null)]
        [TestCase(/* lang=json */ "[{\"email\":\"primary@example.com\",\"primary\":true,\"verified\":false},{\"email\":\"other@example.com\",\"verified\":true}]", "primary@example.com", false)]
        [TestCase(/* lang=json */ "[{\"email\":\"other@example.com\",\"verified\":true},{\"email\":\"primary@example.com\",\"primary\":true,\"verified\":false}]", "primary@example.com", false)]
        [TestCase(/* lang=json */ "[{\"email\":\"first@example.com\",\"verified\":false},{\"email\":\"verified@example.com\",\"verified\":true}]", "verified@example.com", true)]
        [TestCase(/* lang=json */ "[{\"email\":\"first@example.com\",\"verified\":false},{\"email\":\"second@example.com\",\"verified\":false}]", "first@example.com", false)]
        [TestCase(/* lang=json */ "[{\"email\":\"unknown@example.com\"}]", "unknown@example.com", null)]
        [TestCase(/* lang=json */ "[{\"email\":\"primary@example.com\",\"primary\":true},{\"email\":\"other@example.com\",\"verified\":true}]", "primary@example.com", null)]
        [TestCase(/* lang=json */ "[{\"email\":\"\",\"primary\":true,\"verified\":true},{\"email\":\"fallback@example.com\",\"verified\":false}]", "fallback@example.com", false)]
        [TestCase(/* lang=json */ "[{\"EMAIL\":\"case@example.com\",\"PRIMARY\":true,\"VERIFIED\":true}]", "case@example.com", true)]
        [TestCase(/* lang=json */ "[{\"primary\":true,\"verified\":true}]", null, null)]
        public async Task GetUserInfoAsync_EmailEndpoint_PreservesSelectedVerification(string emails, string? expectedEmail, bool? expectedVerification)
        {
            // arrange
            EnqueuePrivateProfile(emails);
            var parameters = new NameValueCollection { { "code", "code" } };

            // act
            var info = await _client.GetUserInfoAsync(parameters);

            // assert
            info.Id.Should().Be("123");
            info.ProviderName.Should().Be("GitHub");
            info.Email.Should().Be(expectedEmail);
            info.EmailVerified.Should().Be(expectedVerification);
            info.ProviderData.Should().BeNull();
            _handler.SentRequests.Should().HaveCount(3);
            _handler.SentRequests[2].RequestUri!.AbsolutePath.Should().Be("/user/emails");
            _handler.SentRequests[2].Headers.Authorization!.Scheme.Should().Be("Bearer");
            _handler.SentRequests[2].Headers.Authorization!.Parameter.Should().Be("token");
        }

        [Test]
        public async Task GetUserInfoAsync_OverriddenEmailParser_HonorsVerificationAndSelection()
        {
            // arrange
            _client.UseCustomEmails = true;
            EnqueuePrivateProfile(/* lang=json */ "[]");
            var parameters = new NameValueCollection { { "code", "code" } };

            // act
            var info = await _client.GetUserInfoAsync(parameters);

            // assert
            info.Email.Should().Be("override@example.com");
            info.EmailVerified.Should().BeFalse();
            _handler.SentRequests.Should().HaveCount(3);
        }

        [Test]
        public async Task GetUserInfoAsync_PublicEmail_DoesNotInferVerificationOrAddRequests()
        {
            // arrange
            _handler.EnqueueResponse(TokenResponse);
            _handler.EnqueueResponse(/* lang=json */ "{\"id\":123,\"login\":\"test\",\"email\":\"public@example.com\",\"avatar_url\":null,\"verified\":true,\"email_verified\":true,\"hd\":\"example.com\"}");
            var parameters = new NameValueCollection { { "code", "code" } };

            // act
            var info = await _client.GetUserInfoAsync(parameters);

            // assert
            info.Email.Should().Be("public@example.com");
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().BeNull();
            _handler.SentRequests.Should().HaveCount(2);
            _handler.SentRequests[1].RequestUri!.AbsolutePath.Should().Be("/user");
        }

        [Test]
        public async Task GetUserInfoAsync_RepeatedResponses_DoesNotReuseVerification()
        {
            // arrange
            EnqueuePrivateProfile(/* lang=json */ "[{\"email\":\"first@example.com\",\"primary\":true,\"verified\":true}]");
            await _client.GetUserInfoAsync(new NameValueCollection { { "code", "first-code" } });
            EnqueuePrivateProfile(/* lang=json */ "[{\"email\":\"second@example.com\",\"primary\":true}]");
            var parameters = new NameValueCollection { { "code", "second-code" } };

            // act
            var info = await _client.GetUserInfoAsync(parameters);

            // assert
            info.Email.Should().Be("second@example.com");
            info.EmailVerified.Should().BeNull();
            _handler.SentRequests.Should().HaveCount(6);
        }

        [TearDown]
        public void TearDown()
        {
            _apiClient?.Dispose();
            _tokenClient?.Dispose();
            _httpClient?.Dispose();
            _handler?.Dispose();
        }

        private void EnqueuePrivateProfile(string emails)
        {
            _handler.EnqueueResponse(TokenResponse);
            _handler.EnqueueResponse(PrivateProfile);
            _handler.EnqueueResponse(emails);
        }

        private class TestableGitHubClient : GitHubClient
        {
            public TestableGitHubClient(IRequestFactory factory, IClientConfiguration configuration)
                : base(factory, configuration)
            {
            }

            public bool UseCustomEmails { get; set; }

            protected override List<UserEmails> ParseEmailAddresses(string content)
            {
                return UseCustomEmails
                    ? new List<UserEmails> { new UserEmails { Email = "override@example.com", Primary = true, Verified = false } }
                    : base.ParseEmailAddresses(content);
            }
        }
    }
}
