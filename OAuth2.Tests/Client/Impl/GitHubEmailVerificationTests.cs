using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using OAuth2.Client;
using OAuth2.Client.Impl;
using OAuth2.Configuration;
using OAuth2.Infrastructure;
using OAuth2.Tests.TestHelpers;

namespace OAuth2.Tests.Client.Impl
{
    [TestFixture]
    public class GitHubEmailVerificationTests : OAuth2ClientTestBase
    {
        /* lang=json */
        private const string PrivateProfile = "{\"id\":123,\"login\":\"test\",\"name\":\"John Doe\",\"email\":null,\"avatar_url\":\"https://example.com/avatar\"}";
        /* lang=json */
        private const string TokenResponse = "{\"access_token\":\"token\",\"token_type\":\"Bearer\"}";

        private TestableGitHubClient _client = null!;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            ConfigureEndpoints("https://github.com", "https://api.github.com");
            _client = new TestableGitHubClient(Factory, Configuration);
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
            Handler.SentRequests.Should().HaveCount(3);
            Handler.SentRequests[2].RequestUri!.AbsolutePath.Should().Be("/user/emails");
            Handler.SentRequests[2].Headers.Authorization!.Scheme.Should().Be("Bearer");
            Handler.SentRequests[2].Headers.Authorization!.Parameter.Should().Be("token");
        }

        [TestCase(HttpStatusCode.Unauthorized)]
        [TestCase(HttpStatusCode.Forbidden)]
        [TestCase(HttpStatusCode.TooManyRequests)]
        [TestCase(HttpStatusCode.InternalServerError)]
        public async Task GetUserInfoAsync_EmailEndpointFailure_RejectsClaims(HttpStatusCode statusCode)
        {
            // arrange
            Handler.EnqueueResponse(TokenResponse);
            Handler.EnqueueResponse(PrivateProfile);
            Handler.EnqueueResponse(statusCode, /* lang=json */ "[{\"email\":\"user@example.com\",\"primary\":true,\"verified\":true}]");
            var parameters = new NameValueCollection { { "code", "code" } };

            // act
            var action = () => _client.GetUserInfoAsync(parameters);

            // assert
            var exception = await action.Should().ThrowAsync<UnexpectedResponseException>();
            exception.Which.Response!.StatusCode.Should().Be(statusCode);
            Handler.SentRequests.Should().HaveCount(3);
            Handler.SentRequests[2].RequestUri!.AbsoluteUri.Should().Be("https://api.github.com/user/emails");
        }

        [TestCase("null")]
        [TestCase("\"true\"")]
        [TestCase("1")]
        [TestCase("0")]
        [TestCase("{}")]
        [TestCase("[]")]
        public async Task GetUserInfoAsync_MalformedVerification_RejectsClaims(string verification)
        {
            // arrange
            EnqueuePrivateProfile("[{\"email\":\"user@example.com\",\"verified\":" + verification + "}]");
            var parameters = new NameValueCollection { { "code", "code" } };

            // act
            var action = () => _client.GetUserInfoAsync(parameters);

            // assert
            await action.Should().ThrowAsync<JsonException>();
            Handler.SentRequests.Should().HaveCount(3);
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
            Handler.SentRequests.Should().HaveCount(3);
        }

        [Test]
        public async Task GetUserInfoAsync_PublicEmail_DoesNotInferVerificationOrAddRequests()
        {
            // arrange
            Handler.EnqueueResponse(TokenResponse);
            Handler.EnqueueResponse(/* lang=json */ "{\"id\":123,\"login\":\"test\",\"email\":\"public@example.com\",\"avatar_url\":null,\"verified\":true,\"email_verified\":true,\"hd\":\"example.com\"}");
            var parameters = new NameValueCollection { { "code", "code" } };

            // act
            var info = await _client.GetUserInfoAsync(parameters);

            // assert
            info.Email.Should().Be("public@example.com");
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().BeNull();
            Handler.SentRequests.Should().HaveCount(2);
            Handler.SentRequests[1].RequestUri!.AbsolutePath.Should().Be("/user");
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
            Handler.SentRequests.Should().HaveCount(6);
        }

        private void EnqueuePrivateProfile(string emails)
        {
            Handler.EnqueueResponse(TokenResponse);
            Handler.EnqueueResponse(PrivateProfile);
            Handler.EnqueueResponse(emails);
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
