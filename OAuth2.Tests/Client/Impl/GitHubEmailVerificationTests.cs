using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        // Redacted token-response excerpt, not a real credential or captured session.
        // https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps
        /* lang=json */
        private const string TokenResponse = """{"access_token":"test-token","token_type":"bearer","scope":"user:email"}""";

        private GitHubClient _client = null!;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            ConfigureEndpoints("https://github.com", "https://api.github.com");
            _client = new GitHubClient(Factory, Configuration);
        }

        [Test]
        public async Task GetUserInfoAsync_DocumentedEmailResponse_PreservesVerification()
        {
            // Arrange
            EnqueuePrivateProfile(ProviderResponses.GitHubEmails);
            var parameters = new NameValueCollection { { "code", "code" } };

            // Act
            var info = await _client.GetUserInfoAsync(parameters);

            // Assert
            info.Id.Should().Be("1");
            info.ProviderName.Should().Be("GitHub");
            info.Email.Should().Be("octocat@github.com");
            info.EmailVerified.Should().BeTrue();
            info.ProviderData.Should().BeNull();
            Handler.SentRequests.Should().HaveCount(3);
            Handler.SentRequests[2].RequestUri!.AbsoluteUri.Should().Be("https://api.github.com/user/emails");
            Handler.SentRequests[2].Headers.Authorization!.Scheme.Should().Be("Bearer");
            Handler.SentRequests[2].Headers.Authorization!.Parameter.Should().Be("test-token");
        }

        [TestCase(HttpStatusCode.Unauthorized)]
        [TestCase(HttpStatusCode.Forbidden)]
        [TestCase(HttpStatusCode.TooManyRequests)]
        [TestCase(HttpStatusCode.InternalServerError)]
        public async Task GetUserInfoAsync_EmailEndpointFailure_RejectsClaims(HttpStatusCode statusCode)
        {
            // Arrange
            Handler.EnqueueResponse(TokenResponse);
            var profile = JsonNode.Parse(ProviderResponses.GitHubUser)!.AsObject();
            profile["email"] = null;
            Handler.EnqueueResponse(profile.ToJsonString());
            // Deliberately pair an error status with plausible claims: the status must win.
            Handler.EnqueueResponse(statusCode, ProviderResponses.GitHubEmails);
            var parameters = new NameValueCollection { { "code", "code" } };

            // Act
            var action = () => _client.GetUserInfoAsync(parameters);

            // Assert
            var exception = await action.Should().ThrowAsync<UnexpectedResponseException>();
            exception.Which.Response!.StatusCode.Should().Be(statusCode);
            Handler.SentRequests.Should().HaveCount(3);
            Handler.SentRequests[2].RequestUri!.AbsoluteUri.Should().Be("https://api.github.com/user/emails");
        }

        // Synthetic selection and missing-field cases using the documented email-record schema.
        // These are not additional captured API responses.
        [TestCase(/* lang=json */ "[]", null, null)]
        [TestCase(/* lang=json */ "null", null, null)]
        [TestCase(/* lang=json */ """[{"email":"primary@example.com","primary":true,"verified":false},{"email":"other@example.com","primary":false,"verified":true}]""", "primary@example.com", false)]
        [TestCase(/* lang=json */ """[{"email":"other@example.com","primary":false,"verified":true},{"email":"primary@example.com","primary":true,"verified":false}]""", "primary@example.com", false)]
        [TestCase(/* lang=json */ """[{"email":"first@example.com","primary":false,"verified":false},{"email":"verified@example.com","primary":false,"verified":true}]""", "verified@example.com", true)]
        [TestCase(/* lang=json */ """[{"email":"first@example.com","primary":false,"verified":false},{"email":"second@example.com","primary":false,"verified":false}]""", "first@example.com", false)]
        [TestCase(/* lang=json */ """[{"email":"unknown@example.com","primary":false}]""", "unknown@example.com", null)]
        [TestCase(/* lang=json */ """[{"email":"primary@example.com","primary":true},{"email":"other@example.com","primary":false,"verified":true}]""", "primary@example.com", null)]
        [TestCase(/* lang=json */ """[{"email":"","primary":true,"verified":true},{"email":"fallback@example.com","primary":false,"verified":false}]""", "fallback@example.com", false)]
        [TestCase(/* lang=json */ """[{"primary":true,"verified":true}]""", null, null)]
        public async Task GetUserInfoAsync_EmailSelection_PreservesSelectedVerification(string emails, string? expectedEmail, bool? expectedVerification)
        {
            // Arrange
            EnqueuePrivateProfile(emails);
            var parameters = new NameValueCollection { { "code", "code" } };

            // Act
            var info = await _client.GetUserInfoAsync(parameters);

            // Assert
            info.Email.Should().Be(expectedEmail);
            info.EmailVerified.Should().Be(expectedVerification);
            info.ProviderData.Should().BeNull();
            Handler.SentRequests.Should().HaveCount(3);
        }

        [TestCase(/* lang=json */ "null")]
        [TestCase(/* lang=json */ "\"true\"")]
        [TestCase(/* lang=json */ "1")]
        [TestCase(/* lang=json */ "0")]
        [TestCase(/* lang=json */ "{}")]
        [TestCase(/* lang=json */ "[]")]
        public async Task GetUserInfoAsync_MalformedVerification_RejectsClaims(string verification)
        {
            // Arrange
            // Deliberately invalid types; retain the existing typed parser's rejection behavior.
            var emails = JsonNode.Parse(ProviderResponses.GitHubEmails)!.AsArray();
            emails[0]!["verified"] = JsonNode.Parse(verification);
            EnqueuePrivateProfile(emails.ToJsonString());
            var parameters = new NameValueCollection { { "code", "code" } };

            // Act
            var action = () => _client.GetUserInfoAsync(parameters);

            // Assert
            await action.Should().ThrowAsync<JsonException>();
            Handler.SentRequests.Should().HaveCount(3);
        }

        [Test]
        public async Task GetUserInfoAsync_OverriddenEmailParser_HonorsVerificationAndSelection()
        {
            // Arrange
            var client = new GitHubClientDescendant(Factory, Configuration);
            EnqueuePrivateProfile(ProviderResponses.GitHubEmails);
            var parameters = new NameValueCollection { { "code", "code" } };

            // Act
            var info = await client.GetUserInfoAsync(parameters);

            // Assert
            info.Email.Should().Be("override@example.com");
            info.EmailVerified.Should().BeFalse();
            Handler.SentRequests.Should().HaveCount(3);
        }

        [Test]
        public async Task GetUserInfoAsync_PublicEmail_DoesNotInferVerificationOrAddRequests()
        {
            // Arrange
            Handler.EnqueueResponse(TokenResponse);
            Handler.EnqueueResponse(ProviderResponses.GitHubUser);
            var parameters = new NameValueCollection { { "code", "code" } };

            // Act
            var info = await _client.GetUserInfoAsync(parameters);

            // Assert
            info.Id.Should().Be("1");
            info.Email.Should().Be("octocat@github.com");
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().BeNull();
            Handler.SentRequests.Should().HaveCount(2);
            Handler.SentRequests[1].RequestUri!.AbsoluteUri.Should().Be("https://api.github.com/user");
        }

        [Test]
        public async Task GetUserInfoAsync_RepeatedResponses_DoesNotReuseVerification()
        {
            // Arrange
            EnqueuePrivateProfile(ProviderResponses.GitHubEmails);
            await _client.GetUserInfoAsync(new NameValueCollection { { "code", "first-code" } });
            var emails = JsonNode.Parse(ProviderResponses.GitHubEmails)!.AsArray();
            emails[0]!.AsObject().Remove("verified");
            EnqueuePrivateProfile(emails.ToJsonString());
            var parameters = new NameValueCollection { { "code", "second-code" } };

            // Act
            var info = await _client.GetUserInfoAsync(parameters);

            // Assert
            info.Email.Should().Be("octocat@github.com");
            info.EmailVerified.Should().BeNull();
            Handler.SentRequests.Should().HaveCount(6);
        }

        private void EnqueuePrivateProfile(string emails)
        {
            Handler.EnqueueResponse(TokenResponse);
            // Contract variation: a private profile has no public email, triggering the existing fallback.
            var profile = JsonNode.Parse(ProviderResponses.GitHubUser)!.AsObject();
            profile["email"] = null;
            Handler.EnqueueResponse(profile.ToJsonString());
            Handler.EnqueueResponse(emails);
        }

        private class GitHubClientDescendant : GitHubClient
        {
            public GitHubClientDescendant(IRequestFactory factory, IClientConfiguration configuration)
                : base(factory, configuration)
            {
            }

            protected override List<UserEmails> ParseEmailAddresses(string content)
            {
                return new List<UserEmails> { new UserEmails { Email = "override@example.com", Primary = true, Verified = false } };
            }
        }
    }
}
