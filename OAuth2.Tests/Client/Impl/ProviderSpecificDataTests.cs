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
    public class ProviderSpecificDataTests
    {
        [Test]
        public void ParseUserInfo_FacebookProfile_DoesNotInferGoogleClaims()
        {
            // arrange
            var client = new TestableFacebookClient();
            /* lang=json */
            const string content = "{\"id\":\"facebook-id\",\"first_name\":\"John\",\"last_name\":\"Doe\",\"email\":\"user@gmail.com\",\"picture\":{\"data\":{\"url\":\"https://example.com/photo.jpg\"}},\"verified\":true,\"hd\":\"example.com\"}";

            // act
            var info = client.ParseUserInfo(content);

            // assert
            info.Id.Should().Be("facebook-id");
            info.Email.Should().Be("user@gmail.com");
            info.FirstName.Should().Be("John");
            info.LastName.Should().Be("Doe");
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_MicrosoftProfile_DoesNotInferGoogleClaims()
        {
            // arrange
            var client = new TestableMicrosoftClient();
            /* lang=json */
            const string content = "{\"id\":\"microsoft-id\",\"givenName\":\"John\",\"surname\":\"Doe\",\"mail\":\"user@example.com\",\"hd\":\"example.com\"}";

            // act
            var info = client.ParseUserInfo(content);

            // assert
            info.Id.Should().Be("microsoft-id");
            info.Email.Should().Be("user@example.com");
            info.FirstName.Should().Be("John");
            info.LastName.Should().Be("Doe");
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().BeNull();
        }

        private class TestableFacebookClient : FacebookClient
        {
            public TestableFacebookClient()
                : base(Substitute.For<IRequestFactory>(), Substitute.For<IClientConfiguration>())
            {
            }

            public new UserInfo ParseUserInfo(string content)
            {
                return base.ParseUserInfo(content);
            }
        }

        private class TestableMicrosoftClient : MicrosoftClient
        {
            public TestableMicrosoftClient()
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
