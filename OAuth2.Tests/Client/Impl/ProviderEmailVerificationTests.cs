using System;
using System.Text.Json.Nodes;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using OAuth2.Client.Impl;
using OAuth2.Configuration;
using OAuth2.Infrastructure;
using OAuth2.Models;
using OAuth2.Tests.TestHelpers;

namespace OAuth2.Tests.Client.Impl
{
    [TestFixture]
    public class ProviderEmailVerificationTests
    {
        [Test]
        public void ParseUserInfo_BooleanVerification_PreservesClaim(
            [Values("LinkedIn", "Salesforce")] string provider,
            [Values(true, false)] bool verified)
        {
            // Arrange
            var parse = CreateParser(provider);
            var response = JsonNode.Parse(GetContent(provider))!.AsObject();
            response["email_verified"] = verified;
            var content = response.ToJsonString();

            // Act
            var info = parse(content);

            // Assert
            info.EmailVerified.Should().Be(verified);
            info.ProviderData.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_MissingEmail_PreservesVerification(
            [Values("LinkedIn", "Salesforce")] string provider)
        {
            // Arrange
            var parse = CreateParser(provider);
            var response = JsonNode.Parse(GetContent(provider))!.AsObject();
            response.Remove("email");
            var content = response.ToJsonString();

            // Act
            var info = parse(content);

            // Assert
            info.Email.Should().BeNull();
            info.EmailVerified.Should().BeTrue();
        }

        [Test]
        public void ParseUserInfo_MissingVerification_ReturnsNull(
            [Values("LinkedIn", "Salesforce")] string provider)
        {
            // Arrange
            var parse = CreateParser(provider);
            var response = JsonNode.Parse(GetContent(provider))!.AsObject();
            response.Remove("email_verified");
            var content = response.ToJsonString();

            // Act
            var info = parse(content);

            // Assert
            info.Email.Should().NotBeNullOrEmpty();
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_NonBooleanVerification_ReturnsNull(
            [Values("LinkedIn", "Salesforce")] string provider,
            [Values(/* lang=json */ "null", /* lang=json */ "\"true\"", /* lang=json */ "1", /* lang=json */ "{}", /* lang=json */ "[]")] string claim)
        {
            // Arrange
            // Deliberately invalid claim types, not published successful responses.
            var parse = CreateParser(provider);
            var response = JsonNode.Parse(GetContent(provider))!.AsObject();
            response["email_verified"] = JsonNode.Parse(claim);
            var content = response.ToJsonString();

            // Act
            var info = parse(content);

            // Assert
            info.Email.Should().NotBeNullOrEmpty();
            info.EmailVerified.Should().BeNull();
            info.PhotoUri.Should().NotBeNullOrEmpty();
            info.ProviderData.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_RepeatedResponses_DoesNotReuseVerification(
            [Values("LinkedIn", "Salesforce")] string provider)
        {
            // Arrange
            var parse = CreateParser(provider);
            var first = parse(GetContent(provider));
            var response = JsonNode.Parse(GetContent(provider))!.AsObject();
            response.Remove("email_verified");
            var content = response.ToJsonString();

            // Act
            var second = parse(content);

            // Assert
            first.EmailVerified.Should().BeTrue();
            second.EmailVerified.Should().BeNull();
            second.Should().NotBeSameAs(first);
        }

        [TestCase("LinkedIn", "782bbtaQ", "doe@email.com", "John", "Doe", "https://media.licdn-ei.com/dms/image/C5F03AQHqK8v7tB1HCQ/profile-displayphoto-shrink_100_100/0/")]
        [TestCase("Salesforce", "https://login.salesforce.com/id/some-org-id/some-user-id", "user@example.com", "Example", "User", "https://example.com/profilephoto/full")]
        public void ParseUserInfo_SourcedResponse_PreservesProfile(string provider, string id, string email, string firstName, string lastName, string photoUri)
        {
            // Arrange
            var parse = CreateParser(provider);
            var content = GetContent(provider);

            // Act
            var info = parse(content);

            // Assert
            info.Id.Should().Be(id);
            info.Email.Should().Be(email);
            info.FirstName.Should().Be(firstName);
            info.LastName.Should().Be(lastName);
            info.PhotoUri.Should().Be(photoUri);
            info.EmailVerified.Should().BeTrue();
            info.ProviderData.Should().BeNull();
        }

        private static Func<string, UserInfo> CreateParser(string provider)
        {
            var factory = Substitute.For<IRequestFactory>();
            var configuration = Substitute.For<IClientConfiguration>();
            if (provider == "LinkedIn")
                return new LinkedInClientDescendant(factory, configuration).ParseUserInfo;
            if (provider == "Salesforce")
                return new SalesforceClientDescendant(factory, configuration).ParseUserInfo;
            throw new ArgumentOutOfRangeException(nameof(provider));
        }

        private static string GetContent(string provider) => provider switch
        {
            "LinkedIn" => ProviderResponses.LinkedInUserInfo,
            "Salesforce" => ProviderResponses.SalesforceIdentity,
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };

        private class LinkedInClientDescendant : LinkedInClient
        {
            public LinkedInClientDescendant(IRequestFactory factory, IClientConfiguration configuration)
                : base(factory, configuration)
            {
            }

            public new UserInfo ParseUserInfo(string content) => base.ParseUserInfo(content);
        }

        private class SalesforceClientDescendant : SalesforceClient
        {
            public SalesforceClientDescendant(IRequestFactory factory, IClientConfiguration configuration)
                : base(factory, configuration)
            {
            }

            public new UserInfo ParseUserInfo(string content) => base.ParseUserInfo(content);
        }
    }
}
