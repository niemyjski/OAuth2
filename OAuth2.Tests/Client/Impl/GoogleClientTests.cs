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

        [Test]
        public void ParseUserInfo_NoPicture_DoesNotThrow()
        {
            // arrange (uses Content const without picture)

            // act & assert
            _descendant.Invoking(x => x.ParseUserInfo(Content)).Should().NotThrow();
        }

        [Test]
        public void ParseUserInfo_ValidContent_ReturnsCorrectFields()
        {
            // arrange (uses ContentWithPicture const)

            // act
            var info = _descendant.ParseUserInfo(ContentWithPicture);

            // assert
            info.Id.Should().Be("id");
            info.FirstName.Should().Be("name");
            info.LastName.Should().Be("surname");
            info.Email.Should().Be("email");
            info.PhotoUri.Should().Be("picture");
        }

        [TestCase("true", true)]
        [TestCase("false", false)]
        public void ParseUserInfo_BooleanEmailVerified_PreservesClaim(string claim, bool expected)
        {
            var info = _descendant.ParseUserInfo(ContentWithClaims($"\"email_verified\":{claim},\"hd\":\"example.com\""));

            info.EmailVerified.Should().Be(expected);
            info.HostedDomain.Should().Be("example.com");
            info.Email.Should().Be("email");
        }

        [TestCase("true", true)]
        [TestCase("false", false)]
        public void ParseUserInfo_BooleanEmailVerifiedWithoutHostedDomain_PreservesClaim(string claim, bool expected)
        {
            var info = _descendant.ParseUserInfo(ContentWithClaims($"\"email_verified\":{claim}"));

            info.EmailVerified.Should().Be(expected);
            info.HostedDomain.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_MissingClaims_ReturnsNull()
        {
            var info = _descendant.ParseUserInfo(ContentWithPicture);

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
            var info = _descendant.ParseUserInfo(ContentWithClaims($"\"email_verified\":{claim},\"hd\":\"example.com\""));

            info.EmailVerified.Should().BeNull();
            info.HostedDomain.Should().Be("example.com");
            info.Email.Should().Be("email");
            info.PhotoUri.Should().Be("picture");
        }

        [TestCase("example.com")]
        [TestCase("Example.COM")]
        [TestCase("")]
        [TestCase(" example.com ")]
        public void ParseUserInfo_StringHostedDomain_PreservesClaimWithoutInferringVerification(string domain)
        {
            var info = _descendant.ParseUserInfo(ContentWithClaims($"\"hd\":\"{domain}\""));

            info.HostedDomain.Should().Be(domain);
            info.EmailVerified.Should().BeNull();
        }

        [TestCase("null")]
        [TestCase("true")]
        [TestCase("false")]
        [TestCase("1")]
        [TestCase("{}")]
        [TestCase("[]")]
        public void ParseUserInfo_NonStringHostedDomain_ReturnsNull(string claim)
        {
            var info = _descendant.ParseUserInfo(ContentWithClaims($"\"email_verified\":true,\"hd\":{claim}"));

            info.HostedDomain.Should().BeNull();
            info.EmailVerified.Should().BeTrue();
            info.Email.Should().Be("email");
        }

        [TestCase("user@gmail.com")]
        [TestCase("user@example.com")]
        public void ParseUserInfo_EmailDomain_DoesNotInferClaims(string email)
        {
            var info = _descendant.ParseUserInfo(ContentWithPicture.Replace("\"email\":\"email\"", $"\"email\":\"{email}\""));

            info.Email.Should().Be(email);
            info.EmailVerified.Should().BeNull();
            info.HostedDomain.Should().BeNull();
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
