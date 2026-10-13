using System.Collections.Specialized;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using OAuth2.Client;
using OAuth2.Client.Impl;
using OAuth2.Tests.TestHelpers;

namespace OAuth2.Tests.Client.Impl;

[TestFixture]
public class ProviderUserInfoTests : OAuth2ClientTestBase
{
    /* lang=json */
    private const string LinkedInContent = "{\"sub\":\"user-123\",\"email\":\"user@example.com\",\"given_name\":\"John\",\"family_name\":\"Doe\",\"picture\":\"https://example.com/photo.jpg\"}";
    /* lang=json */
    private const string SalesforceContent = "{\"id\":\"https://login.salesforce.com/id/org-id/user-id\",\"email\":\"user@example.com\",\"first_name\":\"John\",\"last_name\":\"Doe\",\"photos\":{\"thumbnail\":\"https://example.com/small.jpg\",\"picture\":\"https://example.com/photo.jpg\"}}";

    [Test]
    public async Task GetUserInfoAsync_GoogleResponse_PreservesProviderAndClaims()
    {
        // arrange
        Handler.EnqueueResponse(/* lang=json */ "{\"access_token\":\"token\",\"token_type\":\"Bearer\"}");
        Handler.EnqueueResponse(/* lang=json */ "{\"sub\":\"user-123\",\"email\":\"user@example.com\",\"email_verified\":true,\"hd\":\"example.com\"}");
        ConfigureEndpoints("https://oauth2.googleapis.com", "https://www.googleapis.com");
        var client = new GoogleClient(Factory, Configuration);
        var parameters = new NameValueCollection { { "code", "code" }, { "state", "state" } };

        // act
        var info = await client.GetUserInfoAsync(parameters);

        // assert
        info.ProviderName.Should().Be("Google");
        info.Id.Should().Be("user-123");
        info.Email.Should().Be("user@example.com");
        info.EmailVerified.Should().BeTrue();
        info.ProviderData.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Key = "hd", Value = "example.com" });
        info.FirstName.Should().BeNull();
        info.LastName.Should().BeNull();
        client.State.Should().Be("state");
        Handler.SentRequests.Should().HaveCount(2);
        Handler.SentRequests[0].RequestUri!.AbsoluteUri.Should().Be("https://oauth2.googleapis.com/token");
        Handler.SentRequests[1].RequestUri!.AbsoluteUri.Should().Be("https://www.googleapis.com/oauth2/v3/userinfo");
        Handler.SentRequests[1].Headers.Authorization!.Scheme.Should().Be("Bearer");
        Handler.SentRequests[1].Headers.Authorization!.Parameter.Should().Be("token");
    }

    [Test]
    public async Task GetUserInfoAsync_VerificationResponse_PreservesClaimsAndBearerHeader(
        [Values("LinkedIn", "Salesforce")] string provider,
        [Values(true, false)] bool expectedVerification)
    {
        // arrange
        Handler.EnqueueResponse(/* lang=json */ "{\"access_token\":\"token\",\"token_type\":\"Bearer\",\"id\":\"https://login.salesforce.com/id/org-id/user-id\"}");
        Handler.EnqueueResponse(ContentWithClaims(provider, $"\"email_verified\":{JsonSerializer.Serialize(expectedVerification)}"));
        var tokenBaseUri = provider == "LinkedIn" ? "https://www.linkedin.com" : "https://login.salesforce.com";
        var userBaseUri = provider == "LinkedIn" ? "https://api.linkedin.com" : "https://login.salesforce.com";
        var expectedUserPath = provider == "LinkedIn" ? "/v2/userinfo" : "/id/org-id/user-id";
        ConfigureEndpoints(tokenBaseUri, userBaseUri);
        OAuth2Client client = provider == "LinkedIn"
            ? new LinkedInClient(Factory, Configuration)
            : new SalesforceClient(Factory, Configuration);
        var parameters = new NameValueCollection { { "code", "code" }, { "state", "state" } };
        var expectedId = provider == "LinkedIn" ? "user-123" : "https://login.salesforce.com/id/org-id/user-id";

        // act
        var info = await client.GetUserInfoAsync(parameters);

        // assert
        info.Id.Should().Be(expectedId);
        info.ProviderName.Should().Be(provider);
        info.Email.Should().Be("user@example.com");
        info.EmailVerified.Should().Be(expectedVerification);
        info.ProviderData.Should().BeNull();
        client.State.Should().Be("state");
        Handler.SentRequests.Should().HaveCount(2);
        Handler.SentRequests[1].RequestUri!.AbsoluteUri.Should().Be(userBaseUri + expectedUserPath);
        Handler.SentRequests[1].Headers.Authorization!.Scheme.Should().Be("Bearer");
        Handler.SentRequests[1].Headers.Authorization!.Parameter.Should().Be("token");
    }

    private static string ContentWithClaims(string provider, string claims)
    {
        var content = provider == "LinkedIn" ? LinkedInContent : SalesforceContent;
        return content.Substring(0, content.Length - 1) + "," + claims + "}";
    }
}
