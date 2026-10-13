using System;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using OAuth2.Client.Impl;
using OAuth2.Configuration;
using OAuth2.Infrastructure;
using OAuth2.Models;

namespace OAuth2.Tests.Client.Impl
{
    [TestFixture]
    public class ProviderEmailVerificationTests
    {
        /* lang=json */
        private const string LinkedInContent = "{\"sub\":\"user-123\",\"email\":\"user@example.com\",\"given_name\":\"John\",\"family_name\":\"Doe\",\"picture\":\"https://example.com/photo.jpg\"}";
        /* lang=json */
        private const string SalesforceContent = "{\"id\":\"https://login.salesforce.com/id/org-id/user-id\",\"email\":\"user@example.com\",\"first_name\":\"John\",\"last_name\":\"Doe\",\"photos\":{\"thumbnail\":\"https://example.com/small.jpg\",\"picture\":\"https://example.com/photo.jpg\"}}";

        [Test]
        public void ParseUserInfo_BooleanEmailVerified_PreservesClaim(
            [Values("LinkedIn", "Salesforce")] string provider,
            [Values(true, false)] bool expectedVerification)
        {
            // arrange
            var parse = CreateParser(provider);
            var content = ContentWithClaims(provider, $"\"email_verified\":{JsonSerializer.Serialize(expectedVerification)}");
            var expectedId = ExpectedId(provider);

            // act
            var info = parse(content);

            // assert
            info.Id.Should().Be(expectedId);
            info.Email.Should().Be("user@example.com");
            info.EmailVerified.Should().Be(expectedVerification);
            info.FirstName.Should().Be("John");
            info.LastName.Should().Be("Doe");
            info.PhotoUri.Should().Be("https://example.com/photo.jpg");
            info.ProviderData.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_DifferentlyCasedVerification_ReturnsNull(
            [Values("LinkedIn", "Salesforce")] string provider,
            [Values("EMAIL_VERIFIED", "Email_Verified")] string propertyName)
        {
            // arrange
            var parse = CreateParser(provider);
            var content = ContentWithClaims(provider, $"\"{propertyName}\":true");

            // act
            var info = parse(content);

            // assert
            info.Email.Should().Be("user@example.com");
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_GoogleHostedDomain_DoesNotPromoteProviderData(
            [Values("LinkedIn", "Salesforce")] string provider)
        {
            // arrange
            var parse = CreateParser(provider);
            var content = ContentWithClaims(provider, "\"email_verified\":true,\"hd\":\"example.com\",\"organization_id\":\"org-id\"");

            // act
            var info = parse(content);

            // assert
            info.EmailVerified.Should().BeTrue();
            info.ProviderData.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_MissingEmail_DoesNotInventEmail(
            [Values("LinkedIn", "Salesforce")] string provider)
        {
            // arrange
            var parse = CreateParser(provider);
            var content = ContentWithClaims(provider, "\"email_verified\":true")
                .Replace("\"email\":\"user@example.com\",", String.Empty);

            // act
            var info = parse(content);

            // assert
            info.Email.Should().BeNull();
            info.EmailVerified.Should().BeTrue();
            info.ProviderData.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_MissingVerification_ReturnsNull(
            [Values("LinkedIn", "Salesforce")] string provider)
        {
            // arrange
            var parse = CreateParser(provider);
            var content = ContentWithClaims(provider, "\"verified\":true");

            // act
            var info = parse(content);

            // assert
            info.Email.Should().Be("user@example.com");
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_NonBooleanVerification_ReturnsNull(
            [Values("LinkedIn", "Salesforce")] string provider,
            [Values(/* lang=json */ "null", /* lang=json */ "\"true\"", /* lang=json */ "\"false\"", /* lang=json */ "\"TRUE\"", /* lang=json */ "\"\"", /* lang=json */ "0", /* lang=json */ "1", /* lang=json */ "{}", /* lang=json */ "[]")] string claim)
        {
            // arrange
            var parse = CreateParser(provider);
            var content = ContentWithClaims(provider, $"\"email_verified\":{claim}");

            // act
            var info = parse(content);

            // assert
            info.Email.Should().Be("user@example.com");
            info.EmailVerified.Should().BeNull();
            info.PhotoUri.Should().Be("https://example.com/photo.jpg");
            info.ProviderData.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_RepeatedResponses_DoesNotReuseVerification(
            [Values("LinkedIn", "Salesforce")] string provider)
        {
            // arrange
            var parse = CreateParser(provider);
            var first = parse(ContentWithClaims(provider, "\"email_verified\":true"));
            var content = ContentWithClaims(provider, "\"unrelated\":true");

            // act
            var second = parse(content);

            // assert
            first.EmailVerified.Should().BeTrue();
            second.EmailVerified.Should().BeNull();
            second.Should().NotBeSameAs(first);
        }

        private static string ContentWithClaims(string provider, string claims)
        {
            var content = provider == "LinkedIn" ? LinkedInContent : SalesforceContent;
            return content.Substring(0, content.Length - 1) + "," + claims + "}";
        }

        private static Func<string, UserInfo> CreateParser(string provider)
        {
            if (provider == "LinkedIn")
                return new TestableLinkedInClient().ParseUserInfo;

            if (provider == "Salesforce")
                return new TestableSalesforceClient().ParseUserInfo;

            throw new ArgumentOutOfRangeException(nameof(provider));
        }

        private static string ExpectedId(string provider)
        {
            return provider == "LinkedIn" ? "user-123" : "https://login.salesforce.com/id/org-id/user-id";
        }

        private class TestableLinkedInClient : LinkedInClient
        {
            public TestableLinkedInClient()
                : base(Substitute.For<IRequestFactory>(), Substitute.For<IClientConfiguration>())
            {
            }

            public new UserInfo ParseUserInfo(string content)
            {
                return base.ParseUserInfo(content);
            }
        }

        private class TestableSalesforceClient : SalesforceClient
        {
            public TestableSalesforceClient()
                : base(Substitute.For<IRequestFactory>(), Substitute.For<IClientConfiguration>())
            {
            }

            public new UserInfo ParseUserInfo(string content)
            {
                return base.ParseUserInfo(content);
            }
        }
    }
}
