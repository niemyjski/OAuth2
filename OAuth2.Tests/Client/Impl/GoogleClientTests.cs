using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using OAuth2.Client;
using OAuth2.Client.Impl;
using OAuth2.Configuration;
using OAuth2.Infrastructure;
using OAuth2.Models;

namespace OAuth2.Tests.Client.Impl
{
    [TestFixture]
    public class GoogleClientTests
    {
        /* lang=json */
        private const string Content = "{\"email\":\"email\",\"given_name\":\"name\",\"family_name\":\"surname\",\"sub\":\"id\"}";
        /* lang=json */
        private const string ContentWithPicture = "{\"email\":\"email\",\"given_name\":\"name\",\"family_name\":\"surname\",\"sub\":\"id\",\"picture\":\"picture\"}";

        private GoogleClientDescendant _descendant = null!;

        [SetUp]
        public void SetUp()
        {
            _descendant = new GoogleClientDescendant(Substitute.For<IRequestFactory>(), Substitute.For<IClientConfiguration>());
        }

        [Test]
        public void AccessCodeEndpoint_Default_ReturnsCorrectEndpoint()
        {
            // arrange

            // act
            var endpoint = _descendant.GetAccessCodeServiceEndpoint();

            // assert
            endpoint.BaseUri.Should().Be("https://accounts.google.com");
            endpoint.Resource.Should().Be("/o/oauth2/v2/auth");
        }

        [Test]
        public void AccessTokenEndpoint_Default_ReturnsCorrectEndpoint()
        {
            // arrange

            // act
            var endpoint = _descendant.GetAccessTokenServiceEndpoint();

            // assert
            endpoint.BaseUri.Should().Be("https://oauth2.googleapis.com");
            endpoint.Resource.Should().Be("/token");
        }

        [TestCase("true", true)]
        [TestCase("false", false)]
        public void ParseUserInfo_BooleanEmailVerifiedWithHostedDomain_PreservesClaim(string claim, bool expected)
        {
            // arrange
            var content = ContentWithClaims($"\"email_verified\":{claim},\"hd\":\"example.com\"");

            // act
            var info = _descendant.ParseUserInfo(content);

            // assert
            info.EmailVerified.Should().Be(expected);
            info.HostedDomain.Should().Be("example.com");
            info.Email.Should().Be("email");
        }

        [TestCase("true", true)]
        [TestCase("false", false)]
        public void ParseUserInfo_BooleanEmailVerifiedWithoutHostedDomain_PreservesClaim(string claim, bool expected)
        {
            // arrange
            var content = ContentWithClaims($"\"email_verified\":{claim}");

            // act
            var info = _descendant.ParseUserInfo(content);

            // assert
            info.EmailVerified.Should().Be(expected);
            info.HostedDomain.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_ClaimsWithoutEmail_PreservesClaims()
        {
            // arrange
            /* lang=json */
            const string content = "{\"given_name\":\"name\",\"family_name\":\"surname\",\"sub\":\"id\",\"email_verified\":true,\"hd\":\"example.com\"}";

            // act
            var info = _descendant.ParseUserInfo(content);

            // assert
            info.Id.Should().Be("id");
            info.Email.Should().BeNull();
            info.EmailVerified.Should().BeTrue();
            info.HostedDomain.Should().Be("example.com");
        }

        [Test]
        public void ParseUserInfo_ClaimsWithoutPicture_PreservesClaims()
        {
            // arrange
            /* lang=json */
            const string content = "{\"email\":\"email\",\"given_name\":\"name\",\"family_name\":\"surname\",\"sub\":\"id\",\"email_verified\":false,\"hd\":\"example.com\"}";

            // act
            var info = _descendant.ParseUserInfo(content);

            // assert
            info.Email.Should().Be("email");
            info.EmailVerified.Should().BeFalse();
            info.HostedDomain.Should().Be("example.com");
            info.PhotoUri.Should().BeNull();
            info.AvatarUri.Small.Should().BeEmpty();
            info.AvatarUri.Large.Should().BeEmpty();
        }

        [TestCase("EMAIL_VERIFIED", "HD")]
        [TestCase("Email_Verified", "Hd")]
        public void ParseUserInfo_DifferentlyCasedClaims_DoesNotRecognizeClaims(string emailVerifiedName, string hostedDomainName)
        {
            // arrange
            var content = ContentWithClaims($"\"{emailVerifiedName}\":true,\"{hostedDomainName}\":\"example.com\"");

            // act
            var info = _descendant.ParseUserInfo(content);

            // assert
            info.EmailVerified.Should().BeNull();
            info.HostedDomain.Should().BeNull();
            info.Id.Should().Be("id");
            info.Email.Should().Be("email");
        }

        [TestCase("user@gmail.com")]
        [TestCase("user@example.com")]
        public void ParseUserInfo_EmailDomain_DoesNotInferClaims(string email)
        {
            // arrange
            var content = ContentWithPicture.Replace("\"email\":\"email\"", $"\"email\":\"{email}\"");

            // act
            var info = _descendant.ParseUserInfo(content);

            // assert
            info.Email.Should().Be(email);
            info.EmailVerified.Should().BeNull();
            info.HostedDomain.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_MissingClaims_ReturnsNull()
        {
            // arrange
            var content = ContentWithPicture;

            // act
            var info = _descendant.ParseUserInfo(content);

            // assert
            info.EmailVerified.Should().BeNull();
            info.HostedDomain.Should().BeNull();
        }

        [TestCase("null")]
        [TestCase("\"true\"")]
        [TestCase("\"false\"")]
        [TestCase("\"TRUE\"")]
        [TestCase("\"\"")]
        [TestCase("1")]
        [TestCase("0")]
        [TestCase("{}")]
        [TestCase("[]")]
        public void ParseUserInfo_NonBooleanEmailVerified_ReturnsNull(string claim)
        {
            // arrange
            var content = ContentWithClaims($"\"email_verified\":{claim},\"hd\":\"example.com\"");

            // act
            var info = _descendant.ParseUserInfo(content);

            // assert
            info.EmailVerified.Should().BeNull();
            info.HostedDomain.Should().Be("example.com");
            info.Email.Should().Be("email");
            info.PhotoUri.Should().Be("picture");
        }

        [TestCase("null")]
        [TestCase("true")]
        [TestCase("false")]
        [TestCase("1")]
        [TestCase("{}")]
        [TestCase("[]")]
        public void ParseUserInfo_NonStringHostedDomain_ReturnsNull(string claim)
        {
            // arrange
            var content = ContentWithClaims($"\"email_verified\":true,\"hd\":{claim}");

            // act
            var info = _descendant.ParseUserInfo(content);

            // assert
            info.HostedDomain.Should().BeNull();
            info.EmailVerified.Should().BeTrue();
            info.Email.Should().Be("email");
        }

        [Test]
        public void ParseUserInfo_NoPicture_DoesNotThrow()
        {
            // arrange
            var content = Content;

            // act
            var info = _descendant.ParseUserInfo(content);

            // assert
            info.Should().NotBeNull();
            info.PhotoUri.Should().BeNull();
            info.EmailVerified.Should().BeNull();
            info.HostedDomain.Should().BeNull();
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
            var content = ContentWithClaims($"\"hd\":{JsonSerializer.Serialize(domain)}");

            // act
            var info = _descendant.ParseUserInfo(content);

            // assert
            info.HostedDomain.Should().Be(domain);
            info.EmailVerified.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_ValidContent_ReturnsCorrectFields()
        {
            // arrange
            var content = ContentWithPicture;

            // act
            var info = _descendant.ParseUserInfo(content);

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

            // act
            var endpoint = _descendant.GetUserInfoServiceEndpoint();

            // assert
            endpoint.BaseUri.Should().Be("https://www.googleapis.com");
            endpoint.Resource.Should().Be("/oauth2/v3/userinfo");
        }

        private static string ContentWithClaims(string claims)
        {
            return ContentWithPicture.Substring(0, ContentWithPicture.Length - 1) + "," + claims + "}";
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
