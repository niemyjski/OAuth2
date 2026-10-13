using System.Collections.Specialized;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using OAuth2.Client.Impl;
using OAuth2.Tests.TestHelpers;

namespace OAuth2.Tests.Client.Impl;

[TestFixture]
public class ProviderUserInfoTests : OAuth2ClientTestBase
{
    [Test]
    public async Task GetUserInfoAsync_GoogleResponse_PreservesProviderAndClaims()
    {
        // Arrange
        // Redacted token-response excerpt; no authenticated session or real credential is used.
        // https://developers.google.com/identity/protocols/oauth2/web-server#exchange-authorization-code
        Handler.EnqueueResponse(/* lang=json */ """{"access_token":"test-token","expires_in":3600,"token_type":"Bearer"}""");
        var response = JsonNode.Parse(ProviderResponses.GoogleUserInfo)!.AsObject();
        response["hd"] = "example.com"; // Contract variation: Google's documented optional claim.
        Handler.EnqueueResponse(response.ToJsonString());
        ConfigureEndpoints("https://oauth2.googleapis.com", "https://www.googleapis.com");
        var client = new GoogleClient(Factory, Configuration);
        var parameters = new NameValueCollection { { "code", "code" }, { "state", "state" } };

        // Act
        var info = await client.GetUserInfoAsync(parameters);

        // Assert
        info.ProviderName.Should().Be("Google");
        info.Id.Should().Be("111111111111111111111");
        info.Email.Should().Be("example@gmail.com");
        info.EmailVerified.Should().BeTrue();
        info.ProviderData.Should().ContainSingle();
        info.ProviderData.Should().Contain("hd", "example.com");
        client.State.Should().Be("state");
        Handler.SentRequests.Should().HaveCount(2);
        Handler.SentRequests[0].RequestUri!.AbsoluteUri.Should().Be("https://oauth2.googleapis.com/token");
        Handler.SentRequests[1].RequestUri!.AbsoluteUri.Should().Be("https://www.googleapis.com/oauth2/v3/userinfo");
        Handler.SentRequests[1].Headers.Authorization!.Scheme.Should().Be("Bearer");
        Handler.SentRequests[1].Headers.Authorization!.Parameter.Should().Be("test-token");
    }

    [Test]
    public async Task GetUserInfoAsync_LinkedInResponse_PreservesProviderAndClaims()
    {
        // Arrange
        // Redacted LinkedIn token-response excerpt. Salesforce's identity URL does not belong here.
        // https://learn.microsoft.com/en-us/linkedin/shared/authentication/authorization-code-flow
        Handler.EnqueueResponse(/* lang=json */ """{"access_token":"test-token","expires_in":5184000}""");
        Handler.EnqueueResponse(ProviderResponses.LinkedInUserInfo);
        ConfigureEndpoints("https://www.linkedin.com", "https://api.linkedin.com");
        var client = new LinkedInClient(Factory, Configuration);
        var parameters = new NameValueCollection { { "code", "code" }, { "state", "state" } };

        // Act
        var info = await client.GetUserInfoAsync(parameters);

        // Assert
        info.ProviderName.Should().Be("LinkedIn");
        info.Id.Should().Be("782bbtaQ");
        info.Email.Should().Be("doe@email.com");
        info.EmailVerified.Should().BeTrue();
        info.ProviderData.Should().BeNull();
        client.State.Should().Be("state");
        Handler.SentRequests.Should().HaveCount(2);
        Handler.SentRequests[0].RequestUri!.AbsoluteUri.Should().Be("https://www.linkedin.com/oauth/v2/accessToken");
        Handler.SentRequests[1].RequestUri!.AbsoluteUri.Should().Be("https://api.linkedin.com/v2/userinfo");
        Handler.SentRequests[1].Headers.Authorization!.Scheme.Should().Be("Bearer");
        Handler.SentRequests[1].Headers.Authorization!.Parameter.Should().Be("test-token");
    }

    [Test]
    public async Task GetUserInfoAsync_SalesforceResponse_PreservesIdentityUrlAndClaims()
    {
        // Arrange
        // Redacted token-response excerpt. The identity URL matches the sourced identity fixture.
        // https://developer.salesforce.com/docs/platform/mobile-sdk/guide/oauth-using-identity-urls.html
        Handler.EnqueueResponse(/* lang=json */ """{"access_token":"test-token","token_type":"Bearer","id":"https://login.salesforce.com/id/some-org-id/some-user-id"}""");
        Handler.EnqueueResponse(ProviderResponses.SalesforceIdentity);
        ConfigureEndpoints("https://login.salesforce.com");
        var client = new SalesforceClient(Factory, Configuration);
        var parameters = new NameValueCollection { { "code", "code" }, { "state", "state" } };

        // Act
        var info = await client.GetUserInfoAsync(parameters);

        // Assert
        info.ProviderName.Should().Be("Salesforce");
        info.Id.Should().Be("https://login.salesforce.com/id/some-org-id/some-user-id");
        info.Email.Should().Be("user@example.com");
        info.EmailVerified.Should().BeTrue();
        info.ProviderData.Should().BeNull();
        client.SalesforceProfileUrl.Should().Be("https://login.salesforce.com/id/some-org-id/some-user-id");
        client.State.Should().Be("state");
        Handler.SentRequests.Should().HaveCount(2);
        Handler.SentRequests[0].RequestUri!.AbsoluteUri.Should().Be("https://login.salesforce.com/services/oauth2/token");
        Handler.SentRequests[1].RequestUri!.AbsoluteUri.Should().Be("https://login.salesforce.com/id/some-org-id/some-user-id");
        Handler.SentRequests[1].Headers.Authorization!.Scheme.Should().Be("Bearer");
        Handler.SentRequests[1].Headers.Authorization!.Parameter.Should().Be("test-token");
    }
}
