using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using NUnit.Framework;
using OAuth2.Models;

namespace OAuth2.Tests.Serialization
{
    [TestFixture]
    public class UserInfoSerializationTests
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = null,
            WriteIndented = false
        };

        [Test]
        public void Deserialize_LegacyUserInfo_DefaultsNewClaimsToNull()
        {
            // arrange
            /* lang=json */
            const string json = "{\"Id\":\"user-123\",\"ProviderName\":\"Google\",\"Email\":\"test@example.com\",\"FirstName\":\"John\",\"LastName\":\"Doe\"}";

            // act
            var info = JsonSerializer.Deserialize<UserInfo>(json, Options);

            // assert
            info.Should().NotBeNull();
            info!.Id.Should().Be("user-123");
            info.ProviderName.Should().Be("Google");
            info.Email.Should().Be("test@example.com");
            info.FirstName.Should().Be("John");
            info.LastName.Should().Be("Doe");
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().BeNull();
        }

        [Test]
        public void Deserialize_ProviderData_DoesNotPromoteProviderSpecificClaims()
        {
            // arrange
            /* lang=json */
            const string json = "{\"Id\":\"user-123\",\"ProviderName\":\"Other\",\"ProviderData\":{\"email_verified\":\"true\",\"hd\":\"example.com\"}}";

            // act
            var info = JsonSerializer.Deserialize<UserInfo>(json, Options);

            // assert
            info.Should().NotBeNull();
            info!.ProviderName.Should().Be("Other");
            info.EmailVerified.Should().BeNull();
            info.ProviderData.Should().Contain("email_verified", "true");
            info.ProviderData.Should().Contain("hd", "example.com");
        }

        [Test]
        public void Roundtrip_AvatarInfo_DeserializesToEquivalentObject()
        {
            // arrange
            var original = new AvatarInfo
            {
                Small = "small.jpg",
                Normal = "normal.jpg",
                Large = "large.jpg"
            };

            // act
            var json = JsonSerializer.Serialize(original, Options);
            var deserialized = JsonSerializer.Deserialize<AvatarInfo>(json, Options);

            // assert
            deserialized.Should().NotBeNull();
            deserialized!.Small.Should().Be(original.Small);
            deserialized.Normal.Should().Be(original.Normal);
            deserialized.Large.Should().Be(original.Large);
        }

        [Test]
        public void Roundtrip_CamelCaseNamingPolicy_PreservesClaims()
        {
            // arrange
            var options = new JsonSerializerOptions(Options)
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var original = new UserInfo
            {
                ProviderName = "Google",
                EmailVerified = true,
                ProviderData = new Dictionary<string, string> { ["hd"] = "Example.COM" }
            };

            // act
            var json = JsonSerializer.Serialize(original, options);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var deserialized = JsonSerializer.Deserialize<UserInfo>(json, options);

            // assert
            root.GetProperty("emailVerified").GetBoolean().Should().BeTrue();
            root.GetProperty("providerData").GetProperty("hd").GetString().Should().Be("Example.COM");
            root.TryGetProperty("EmailVerified", out _).Should().BeFalse();
            root.TryGetProperty("ProviderData", out _).Should().BeFalse();
            deserialized.Should().NotBeNull();
            deserialized!.ProviderName.Should().Be("Google");
            deserialized.EmailVerified.Should().BeTrue();
            deserialized.ProviderData.Should().Contain("hd", "Example.COM");
        }

        [TestCase(JsonIgnoreCondition.WhenWritingNull)]
        [TestCase(JsonIgnoreCondition.WhenWritingDefault)]
        public void Roundtrip_FalseVerificationWithIgnoreCondition_PreservesFalse(JsonIgnoreCondition ignoreCondition)
        {
            // arrange
            var options = new JsonSerializerOptions(Options)
            {
                DefaultIgnoreCondition = ignoreCondition
            };
            var original = new UserInfo { EmailVerified = false };

            // act
            var json = JsonSerializer.Serialize(original, options);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var deserialized = JsonSerializer.Deserialize<UserInfo>(json, options);

            // assert
            root.GetProperty("EmailVerified").GetBoolean().Should().BeFalse();
            root.TryGetProperty("ProviderData", out _).Should().BeFalse();
            deserialized.Should().NotBeNull();
            deserialized!.EmailVerified.Should().BeFalse();
            deserialized.ProviderData.Should().BeNull();
        }

        [TestCase("Example.COM")]
        [TestCase("")]
        [TestCase(" example.com ")]
        [TestCase("\"example.com\"")]
        [TestCase("example\\domain")]
        [TestCase("例え.テスト")]
        public void Roundtrip_ProviderData_PreservesProviderAndStringValues(string domain)
        {
            // arrange
            var original = new UserInfo
            {
                ProviderName = "Google",
                ProviderData = new Dictionary<string, string> { ["hd"] = domain }
            };

            // act
            var json = JsonSerializer.Serialize(original, Options);
            var deserialized = JsonSerializer.Deserialize<UserInfo>(json, Options);

            // assert
            deserialized.Should().NotBeNull();
            deserialized!.ProviderName.Should().Be("Google");
            deserialized.ProviderData.Should().ContainSingle();
            deserialized.ProviderData.Should().Contain("hd", domain);
            deserialized.EmailVerified.Should().BeNull();
        }

        [Test]
        public void Roundtrip_ProviderDataWithMixedCaseKeys_PreservesExactKeys()
        {
            // arrange
            var original = new UserInfo
            {
                ProviderData = new Dictionary<string, string>
                {
                    ["hd"] = "lower.example",
                    ["HD"] = "upper.example"
                }
            };

            // act
            var json = JsonSerializer.Serialize(original, Options);
            var deserialized = JsonSerializer.Deserialize<UserInfo>(json, Options);

            // assert
            deserialized.Should().NotBeNull();
            deserialized!.ProviderData.Should().HaveCount(2);
            deserialized.ProviderData.Should().Contain("hd", "lower.example");
            deserialized.ProviderData.Should().Contain("HD", "upper.example");
        }

        [TestCase(true, "example.com")]
        [TestCase(false, "example.com")]
        [TestCase(null, null)]
        [TestCase(true, null)]
        [TestCase(false, null)]
        [TestCase(null, "example.com")]
        [TestCase(true, "")]
        public void Roundtrip_UserInfoClaims_PreservesVerificationAndProviderData(bool? emailVerified, string? hostedDomain)
        {
            // arrange
            var original = new UserInfo
            {
                Id = "user-123",
                ProviderName = "Google",
                Email = "test@example.com",
                EmailVerified = emailVerified,
                ProviderData = hostedDomain == null ? null : new Dictionary<string, string> { ["hd"] = hostedDomain }
            };

            // act
            var json = JsonSerializer.Serialize(original, Options);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var deserialized = JsonSerializer.Deserialize<UserInfo>(json, Options);

            // assert
            if (emailVerified.HasValue)
                root.GetProperty("EmailVerified").GetBoolean().Should().Be(emailVerified.Value);
            else
                root.GetProperty("EmailVerified").ValueKind.Should().Be(JsonValueKind.Null);

            root.TryGetProperty("HostedDomain", out _).Should().BeFalse();
            deserialized.Should().NotBeNull();
            deserialized!.Id.Should().Be(original.Id);
            deserialized.ProviderName.Should().Be(original.ProviderName);
            deserialized.Email.Should().Be(original.Email);
            deserialized.EmailVerified.Should().Be(emailVerified);
            if (hostedDomain == null)
            {
                root.GetProperty("ProviderData").ValueKind.Should().Be(JsonValueKind.Null);
                deserialized.ProviderData.Should().BeNull();
            }
            else
            {
                root.GetProperty("ProviderData").GetProperty("hd").GetString().Should().Be(hostedDomain);
                deserialized.ProviderData.Should().Contain("hd", hostedDomain);
            }
        }

        [Test]
        public void Serialize_AvatarInfo_ContainsAllSizeFields()
        {
            // arrange
            var avatar = new AvatarInfo
            {
                Small = "small.jpg",
                Normal = "normal.jpg",
                Large = "large.jpg"
            };

            // act
            var json = JsonSerializer.Serialize(avatar, Options);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // assert
            root.GetProperty("Small").GetString().Should().Be("small.jpg");
            root.GetProperty("Normal").GetString().Should().Be("normal.jpg");
            root.GetProperty("Large").GetString().Should().Be("large.jpg");
        }

        [Test]
        public void Serialize_DefaultUserInfo_ContainsNullAvatarFields()
        {
            // arrange
            var userInfo = new UserInfo();

            // act
            var json = JsonSerializer.Serialize(userInfo, Options);
            using var doc = JsonDocument.Parse(json);
            var avatar = doc.RootElement.GetProperty("AvatarUri");

            // assert
            avatar.GetProperty("Small").ValueKind.Should().Be(JsonValueKind.Null);
            avatar.GetProperty("Normal").ValueKind.Should().Be(JsonValueKind.Null);
            avatar.GetProperty("Large").ValueKind.Should().Be(JsonValueKind.Null);
        }

        [Test]
        public void Serialize_FullyPopulatedUserInfo_ContainsAllFieldValues()
        {
            // arrange
            var userInfo = new UserInfo
            {
                Id = "user-123",
                ProviderName = "TestProvider",
                Email = "test@example.com",
                EmailVerified = true,
                ProviderData = new Dictionary<string, string> { ["custom"] = "value" },
                FirstName = "John",
                LastName = "Doe",
                AvatarUri =
                {
                    Small = "https://example.com/photo_small.jpg",
                    Normal = "https://example.com/photo_normal.jpg",
                    Large = "https://example.com/photo_large.jpg"
                }
            };

            // act
            var json = JsonSerializer.Serialize(userInfo, Options);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // assert
            root.GetProperty("Id").GetString().Should().Be("user-123");
            root.GetProperty("ProviderName").GetString().Should().Be("TestProvider");
            root.GetProperty("Email").GetString().Should().Be("test@example.com");
            root.GetProperty("EmailVerified").GetBoolean().Should().BeTrue();
            root.GetProperty("ProviderData").GetProperty("custom").GetString().Should().Be("value");
            root.GetProperty("FirstName").GetString().Should().Be("John");
            root.GetProperty("LastName").GetString().Should().Be("Doe");
            root.GetProperty("PhotoUri").GetString().Should().Be("https://example.com/photo_normal.jpg");
        }

        [Test]
        public void Serialize_FullyPopulatedUserInfo_ContainsNestedAvatarUri()
        {
            // arrange
            var userInfo = new UserInfo
            {
                AvatarUri =
                {
                    Small = "https://example.com/photo_small.jpg",
                    Normal = "https://example.com/photo_normal.jpg",
                    Large = "https://example.com/photo_large.jpg"
                }
            };

            // act
            var json = JsonSerializer.Serialize(userInfo, Options);
            using var doc = JsonDocument.Parse(json);
            var avatar = doc.RootElement.GetProperty("AvatarUri");

            // assert
            avatar.GetProperty("Small").GetString().Should().Be("https://example.com/photo_small.jpg");
            avatar.GetProperty("Normal").GetString().Should().Be("https://example.com/photo_normal.jpg");
            avatar.GetProperty("Large").GetString().Should().Be("https://example.com/photo_large.jpg");
        }

        [TestCase("Facebook")]
        [TestCase("GitHub")]
        [TestCase("LinkedIn")]
        [TestCase("Microsoft")]
        public void Serialize_NonGoogleProvider_DoesNotExposeHostedDomain(string providerName)
        {
            // arrange
            var userInfo = new UserInfo { ProviderName = providerName };

            // act
            var json = JsonSerializer.Serialize(userInfo, Options);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // assert
            root.GetProperty("ProviderName").GetString().Should().Be(providerName);
            root.GetProperty("EmailVerified").ValueKind.Should().Be(JsonValueKind.Null);
            root.GetProperty("ProviderData").ValueKind.Should().Be(JsonValueKind.Null);
            root.TryGetProperty("HostedDomain", out _).Should().BeFalse();
            root.TryGetProperty("hd", out _).Should().BeFalse();
        }

        [TestCase(JsonIgnoreCondition.WhenWritingNull)]
        [TestCase(JsonIgnoreCondition.WhenWritingDefault)]
        public void Serialize_NullClaimsWithIgnoreCondition_OmitsClaims(JsonIgnoreCondition ignoreCondition)
        {
            // arrange
            var options = new JsonSerializerOptions(Options)
            {
                DefaultIgnoreCondition = ignoreCondition
            };
            var userInfo = new UserInfo { Id = "user-123" };

            // act
            var json = JsonSerializer.Serialize(userInfo, options);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // assert
            root.GetProperty("Id").GetString().Should().Be("user-123");
            root.TryGetProperty("EmailVerified", out _).Should().BeFalse();
            root.TryGetProperty("ProviderData", out _).Should().BeFalse();
        }

        [Test]
        public void Serialize_SameUserInfoTwice_ProducesIdenticalOutput()
        {
            // arrange
            var userInfo = new UserInfo
            {
                Id = "user-123",
                ProviderName = "TestProvider",
                Email = "test@example.com",
                EmailVerified = true,
                ProviderData = new Dictionary<string, string> { ["custom"] = "value" },
                FirstName = "John",
                LastName = "Doe",
                AvatarUri =
                {
                    Small = "https://example.com/photo_small.jpg",
                    Normal = "https://example.com/photo_normal.jpg",
                    Large = "https://example.com/photo_large.jpg"
                }
            };

            // act
            var json1 = JsonSerializer.Serialize(userInfo, Options);
            var json2 = JsonSerializer.Serialize(userInfo, Options);

            // assert
            json1.Should().Be(json2);
        }

        [Test]
        public void Serialize_UserInfo_UsesPascalCasePropertyNames()
        {
            // arrange
            var userInfo = new UserInfo
            {
                Id = "1",
                ProviderName = "Test",
                Email = "e@t.com",
                FirstName = "F",
                LastName = "L",
                AvatarUri = { Normal = "http://test.com/pic.jpg" }
            };

            // act
            var json = JsonSerializer.Serialize(userInfo, Options);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // assert
            root.TryGetProperty("Id", out _).Should().BeTrue();
            root.TryGetProperty("ProviderName", out _).Should().BeTrue();
            root.TryGetProperty("Email", out _).Should().BeTrue();
            root.TryGetProperty("EmailVerified", out _).Should().BeTrue();
            root.TryGetProperty("ProviderData", out _).Should().BeTrue();
            root.TryGetProperty("FirstName", out _).Should().BeTrue();
            root.TryGetProperty("LastName", out _).Should().BeTrue();
            root.TryGetProperty("PhotoUri", out _).Should().BeTrue();
            root.TryGetProperty("AvatarUri", out _).Should().BeTrue();
            root.TryGetProperty("id", out _).Should().BeFalse();
            root.TryGetProperty("provider_name", out _).Should().BeFalse();
            root.TryGetProperty("firstName", out _).Should().BeFalse();
            root.TryGetProperty("email_verified", out _).Should().BeFalse();
            root.TryGetProperty("providerData", out _).Should().BeFalse();
            root.TryGetProperty("hd", out _).Should().BeFalse();
        }

        [Test]
        public void Serialize_UserInfoWithEmptyStrings_SerializesAsEmptyStrings()
        {
            // arrange
            var userInfo = new UserInfo
            {
                Id = "1",
                Email = "",
                FirstName = "",
                LastName = ""
            };

            // act
            var json = JsonSerializer.Serialize(userInfo, Options);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // assert
            root.GetProperty("Email").GetString().Should().BeEmpty();
            root.GetProperty("FirstName").GetString().Should().BeEmpty();
            root.GetProperty("LastName").GetString().Should().BeEmpty();
        }

        [Test]
        public void Serialize_UserInfoWithNormalAvatar_PhotoUriMatchesAvatarUriNormal()
        {
            // arrange
            var userInfo = new UserInfo
            {
                AvatarUri = { Normal = "https://example.com/pic.jpg" }
            };

            // act
            var json = JsonSerializer.Serialize(userInfo, Options);
            using var doc = JsonDocument.Parse(json);

            // assert
            doc.RootElement.GetProperty("PhotoUri").GetString().Should().Be("https://example.com/pic.jpg");
            doc.RootElement.GetProperty("AvatarUri").GetProperty("Normal").GetString()
                .Should().Be("https://example.com/pic.jpg");
        }

        [Test]
        public void Serialize_UserInfoWithNullFields_SerializesAsJsonNull()
        {
            // arrange
            var userInfo = new UserInfo { Id = "1" };

            // act
            var json = JsonSerializer.Serialize(userInfo, Options);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // assert
            root.GetProperty("Email").ValueKind.Should().Be(JsonValueKind.Null);
            root.GetProperty("EmailVerified").ValueKind.Should().Be(JsonValueKind.Null);
            root.GetProperty("ProviderData").ValueKind.Should().Be(JsonValueKind.Null);
            root.GetProperty("FirstName").ValueKind.Should().Be(JsonValueKind.Null);
            root.GetProperty("LastName").ValueKind.Should().Be(JsonValueKind.Null);
        }

        [Test]
        public void Serialize_UserInfoWithSpecialCharacters_PreservesCharacters()
        {
            // arrange
            var userInfo = new UserInfo
            {
                Id = "id-with-special/chars",
                FirstName = "José",
                LastName = "O'Brien",
                Email = "user+tag@example.com"
            };

            // act
            var json = JsonSerializer.Serialize(userInfo, Options);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // assert
            root.GetProperty("Id").GetString().Should().Be("id-with-special/chars");
            root.GetProperty("FirstName").GetString().Should().Be("José");
            root.GetProperty("LastName").GetString().Should().Be("O'Brien");
            root.GetProperty("Email").GetString().Should().Be("user+tag@example.com");
        }

        [Test]
        public void Serialize_UserInfoWithUnicodeAvatarUrls_PreservesUnicode()
        {
            // arrange
            var userInfo = new UserInfo
            {
                AvatarUri =
                {
                    Small = "https://example.com/pic?name=José",
                    Normal = "https://example.com/pic?name=José",
                    Large = "https://example.com/pic?name=José"
                }
            };

            // act
            var json = JsonSerializer.Serialize(userInfo, Options);
            using var doc = JsonDocument.Parse(json);
            var avatar = doc.RootElement.GetProperty("AvatarUri");

            // assert
            avatar.GetProperty("Small").GetString().Should().Contain("José");
        }
    }
}
