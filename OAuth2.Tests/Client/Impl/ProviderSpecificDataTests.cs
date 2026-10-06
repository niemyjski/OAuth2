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
    public class ProviderSpecificDataTests
    {
        [Test]
        public void ParseUserInfo_DigitalOceanToken_DoesNotAssumeAccountEndpointClaims()
        {
            // arrange
            var client = new TestableDigitalOceanClient();
            /* lang=json */
            const string content = "{\"uid\":\"digitalocean-id\",\"info\":{\"name\":\"John Doe\",\"email\":\"user@example.com\"},\"email_verified\":true,\"hd\":\"example.com\"}";

            // act
            var info = client.ParseUserInfo(content);

            // assert
            info.Id.Should().Be("digitalocean-id");
            info.Email.Should().Be("user@example.com");
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().BeNull();
        }

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
        public void ParseUserInfo_LoginCidadaoPerson_DoesNotAssumeOpenIdClaims()
        {
            // arrange
            var client = new TestableLoginCidadaoClient();
            /* lang=json */
            const string content = "{\"first_name\":\"John\",\"last_name\":\"Doe\",\"cpf\":null,\"email\":\"user@example.com\",\"email_verified\":true,\"hd\":\"example.com\"}";

            // act
            var info = client.ParseUserInfo(content);

            // assert
            info.Should().BeOfType<LoginCidadaoClient.Cidadao>();
            info.Email.Should().Be("user@example.com");
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_MailRuPhoneVerification_DoesNotInferEmailVerification()
        {
            // arrange
            var client = new TestableMailRuClient();
            /* lang=json */
            const string content = "[{\"uid\":\"mailru-id\",\"first_name\":\"John\",\"last_name\":\"Doe\",\"email\":\"user@mail.ru\",\"pic\":null,\"is_verified\":1,\"hd\":\"mail.ru\"}]";

            // act
            var info = client.ParseUserInfo(content);

            // assert
            info.Id.Should().Be("mailru-id");
            info.Email.Should().Be("user@mail.ru");
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_MicrosoftProfile_DoesNotInferGoogleClaims()
        {
            // arrange
            var client = new TestableMicrosoftClient();
            /* lang=json */
            const string content = "{\"id\":\"microsoft-id\",\"givenName\":\"John\",\"surname\":\"Doe\",\"mail\":\"user@example.com\",\"hd\":\"example.com\",\"tenantId\":\"tenant-id\"}";

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

        [TestCase("verified")]
        [TestCase("unverified")]
        [TestCase("blocked")]
        [TestCase("legacy")]
        public void ParseUserInfo_TodoistAccountStatus_DoesNotInferEmailVerification(string status)
        {
            // arrange
            var client = new TestableTodoistClient();
            var content = JsonSerializer.Serialize(new
            {
                id = "todoist-id",
                email = "user@example.com",
                verification_status = status,
                business_account_id = "business-id"
            });

            // act
            var info = client.ParseUserInfo(content);

            // assert
            info.Id.Should().Be("todoist-id");
            info.Email.Should().Be("user@example.com");
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().BeNull();
        }

        [Test]
        public void ParseUserInfo_UberMobileVerification_DoesNotInferEmailVerification()
        {
            // arrange
            var client = new TestableUberClient();
            /* lang=json */
            const string content = "{\"first_name\":\"John\",\"last_name\":\"Doe\",\"email\":\"user@example.com\",\"mobile_verified\":true}";

            // act
            var info = client.ParseUserInfo(content);

            // assert
            info.Email.Should().Be("user@example.com");
            info.FirstName.Should().Be("John");
            info.LastName.Should().Be("Doe");
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().BeNull();
        }

        private class TestableDigitalOceanClient : DigitalOceanClient
        {
            public TestableDigitalOceanClient()
                : base(Substitute.For<IRequestFactory>(), Substitute.For<IClientConfiguration>())
            {
            }

            public new UserInfo ParseUserInfo(string content)
            {
                return base.ParseUserInfo(content);
            }
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

        private class TestableLoginCidadaoClient : LoginCidadaoClient
        {
            public TestableLoginCidadaoClient()
                : base(Substitute.For<IRequestFactory>(), Substitute.For<IClientConfiguration>())
            {
            }

            public new UserInfo ParseUserInfo(string content)
            {
                return base.ParseUserInfo(content);
            }
        }

        private class TestableMailRuClient : MailRuClient
        {
            public TestableMailRuClient()
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

        private class TestableTodoistClient : TodoistClient
        {
            public TestableTodoistClient()
                : base(Substitute.For<IRequestFactory>(), Substitute.For<IClientConfiguration>())
            {
            }

            public new UserInfo ParseUserInfo(string content)
            {
                return base.ParseUserInfo(content);
            }
        }

        private class TestableUberClient : UberClient
        {
            public TestableUberClient()
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
