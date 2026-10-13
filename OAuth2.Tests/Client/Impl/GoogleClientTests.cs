using System.Text.Json.Nodes;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using OAuth2.Client;
using OAuth2.Client.Impl;
using OAuth2.Configuration;
using OAuth2.Infrastructure;
using OAuth2.Models;
using OAuth2.Tests.TestHelpers;

namespace OAuth2.Tests.Client.Impl
{
    [TestFixture]
    public class GoogleClientTests
    {
        private GoogleClientDescendant _descendant = null!;

        [SetUp]
        public void SetUp()
        {
            _descendant = new GoogleClientDescendant(Substitute.For<IRequestFactory>(), Substitute.For<IClientConfiguration>());
        }

        [Test]
        public void AccessCodeEndpoint_Default_ReturnsCorrectEndpoint()
        {
            // Arrange
            // The client is initialized in SetUp.

            // Act
            var endpoint = _descendant.GetAccessCodeServiceEndpoint();

            // Assert
            endpoint.BaseUri.Should().Be("https://accounts.google.com");
            endpoint.Resource.Should().Be("/o/oauth2/v2/auth");
        }

        [Test]
        public void AccessTokenEndpoint_Default_ReturnsCorrectEndpoint()
        {
            // Arrange
            // The client is initialized in SetUp.

            // Act
            var endpoint = _descendant.GetAccessTokenServiceEndpoint();

            // Assert
            endpoint.BaseUri.Should().Be("https://oauth2.googleapis.com");
            endpoint.Resource.Should().Be("/token");
        }

        [Test]
        public void ParseUserInfo_HostedDomainWithoutVerification_DoesNotInferVerification()
        {
            // Arrange
            // Contract variation: omit verification and add the documented optional hd claim.
            var response = JsonNode.Parse(ProviderResponses.GoogleUserInfo)!.AsObject();
            response.Remove("email_verified");
            response["hd"] = "example.com";
            var content = response.ToJsonString();

            // Act
            var info = _descendant.ParseUserInfo(content);

            // Assert
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().ContainSingle();
            info.ProviderData.Should().Contain("hd", "example.com");
        }

        [Test]
        public void ParseUserInfo_MissingClaims_DoesNotInferClaimsFromEmail()
        {
            // Arrange
            var response = JsonNode.Parse(ProviderResponses.GoogleUserInfo)!.AsObject();
            response.Remove("email_verified");
            var content = response.ToJsonString();

            // Act
            var info = _descendant.ParseUserInfo(content);

            // Assert
            info.Email.Should().Be("example@gmail.com");
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_MissingOptionalFields_PreservesIdentityAndClaims()
        {
            // Arrange
            var response = JsonNode.Parse(ProviderResponses.GoogleUserInfo)!.AsObject();
            response.Remove("email");
            response.Remove("given_name");
            response.Remove("family_name");
            response.Remove("picture");
            response["hd"] = "example.com";
            var content = response.ToJsonString();

            // Act
            var info = _descendant.ParseUserInfo(content);

            // Assert
            info.Id.Should().Be("111111111111111111111");
            info.Email.Should().BeNull();
            info.EmailVerified.Should().BeTrue();
            info.ProviderData.Should().Contain("hd", "example.com");
            info.FirstName.Should().BeNull();
            info.LastName.Should().BeNull();
            info.PhotoUri.Should().BeNull();
            info.AvatarUri.Small.Should().BeEmpty();
            info.AvatarUri.Large.Should().BeEmpty();
        }

        [TestCase(/* lang=json */ "null")]
        [TestCase(/* lang=json */ "\"true\"")]
        [TestCase(/* lang=json */ "1")]
        [TestCase(/* lang=json */ "{}")]
        [TestCase(/* lang=json */ "[]")]
        public void ParseUserInfo_NonBooleanVerification_ReturnsNull(string claim)
        {
            // Arrange
            // Deliberately invalid claim types, not examples of successful Google responses.
            var response = JsonNode.Parse(ProviderResponses.GoogleUserInfo)!.AsObject();
            response["email_verified"] = JsonNode.Parse(claim);
            response["hd"] = "example.com";
            var content = response.ToJsonString();

            // Act
            var info = _descendant.ParseUserInfo(content);

            // Assert
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().Contain("hd", "example.com");
            info.Email.Should().Be("example@gmail.com");
            info.Id.Should().Be("111111111111111111111");
        }

        [TestCase(/* lang=json */ "null")]
        [TestCase(/* lang=json */ "true")]
        [TestCase(/* lang=json */ "1")]
        [TestCase(/* lang=json */ "{}")]
        [TestCase(/* lang=json */ "[]")]
        public void ParseUserInfo_NonStringHostedDomain_ReturnsNoProviderData(string claim)
        {
            // Arrange
            // Deliberately invalid claim types; verification must remain independent.
            var response = JsonNode.Parse(ProviderResponses.GoogleUserInfo)!.AsObject();
            response["hd"] = JsonNode.Parse(claim);
            var content = response.ToJsonString();

            // Act
            var info = _descendant.ParseUserInfo(content);

            // Assert
            info.ProviderData.Should().BeNull();
            info.EmailVerified.Should().BeTrue();
            info.Email.Should().Be("example@gmail.com");
        }

        [Test]
        public void ParseUserInfo_RepeatedResponses_DoesNotShareProviderData()
        {
            // Arrange
            var response = JsonNode.Parse(ProviderResponses.GoogleUserInfo)!.AsObject();
            response["hd"] = "example.com";
            var content = response.ToJsonString();
            var first = _descendant.ParseUserInfo(content);
            first.ProviderData!["hd"] = "changed.example";

            // Act
            var second = _descendant.ParseUserInfo(content);
            var withoutDomain = _descendant.ParseUserInfo(ProviderResponses.GoogleUserInfo);

            // Assert
            second.ProviderData.Should().NotBeSameAs(first.ProviderData);
            second.ProviderData.Should().ContainSingle();
            second.ProviderData.Should().Contain("hd", "example.com");
            withoutDomain.ProviderData.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_ValidContent_ReturnsCorrectFields()
        {
            // Arrange
            var content = ProviderResponses.GoogleUserInfo;

            // Act
            var info = _descendant.ParseUserInfo(content);

            // Assert
            info.Id.Should().Be("111111111111111111111");
            info.FirstName.Should().Be("Jared");
            info.LastName.Should().Be("Hanson");
            info.Email.Should().Be("example@gmail.com");
            info.EmailVerified.Should().BeTrue();
            info.PhotoUri.Should().Be("https://lh3.googleusercontent.com/-XxXXxxXxXXX/AAAAAAAAAAI/AAAAAAAAAAA/0000xxxxx0X/photo.jpg");
            info.AvatarUri.Small.Should().Be(info.PhotoUri + "?sz=36");
            info.AvatarUri.Large.Should().Be(info.PhotoUri + "?sz=300");
            info.ProviderData.Should().BeNull();
        }

        [TestCase(true, true)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(false, false)]
        public void ParseUserInfo_VerificationAndHostedDomain_PreservesIndependentClaims(bool verified, bool hasHostedDomain)
        {
            // Arrange
            // Contract variations of the sourced profile, not separate live captures.
            var response = JsonNode.Parse(ProviderResponses.GoogleUserInfo)!.AsObject();
            response["email_verified"] = verified;
            if (hasHostedDomain)
                response["hd"] = "example.com";
            var content = response.ToJsonString();

            // Act
            var info = _descendant.ParseUserInfo(content);

            // Assert
            info.EmailVerified.Should().Be(verified);
            if (hasHostedDomain)
            {
                info.ProviderData.Should().ContainSingle();
                info.ProviderData.Should().Contain("hd", "example.com");
            }
            else
            {
                info.ProviderData.Should().BeNull();
            }
        }

        [Test]
        public void UserInfoEndpoint_Default_ReturnsCorrectEndpoint()
        {
            // Arrange
            // The client is initialized in SetUp.

            // Act
            var endpoint = _descendant.GetUserInfoServiceEndpoint();

            // Assert
            endpoint.BaseUri.Should().Be("https://www.googleapis.com");
            endpoint.Resource.Should().Be("/oauth2/v3/userinfo");
        }

        private class GoogleClientDescendant : GoogleClient
        {
            public GoogleClientDescendant(IRequestFactory factory, IClientConfiguration configuration)
                : base(factory, configuration)
            {
            }

            public Endpoint GetAccessCodeServiceEndpoint() => AccessCodeServiceEndpoint;

            public Endpoint GetAccessTokenServiceEndpoint() => AccessTokenServiceEndpoint;

            public Endpoint GetUserInfoServiceEndpoint() => UserInfoServiceEndpoint;

            public new UserInfo ParseUserInfo(string content) => base.ParseUserInfo(content);
        }
    }
}
