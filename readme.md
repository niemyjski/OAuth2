# OAuth2

[![Build](https://github.com/titarenko/OAuth2/workflows/Build/badge.svg)](https://github.com/titarenko/OAuth2/actions)
[![CodeQL](https://github.com/titarenko/OAuth2/workflows/CodeQL/badge.svg)](https://github.com/titarenko/OAuth2/actions/workflows/codeql.yml)
[![NuGet](https://img.shields.io/nuget/v/OAuth2.svg?style=flat)](https://www.nuget.org/packages/OAuth2/)

OAuth2 is a library for user authentication using third-party services (OAuth/OAuth2 protocol) such as Google, Facebook and so on.

## Standard Flow

1. Generate a login URL and render a page with it
2. Define a callback endpoint that the third-party service redirects to after successful authentication
3. Retrieve user info on callback from the third-party service

## Installation

Install the [OAuth2 NuGet package](https://www.nuget.org/packages/OAuth2/):

```shell
dotnet add package OAuth2
```

## Usage Example (ASP.NET Core Minimal API)

```csharp
using System.Collections.Specialized;
using OAuth2.Client.Impl;
using OAuth2.Configuration;
using OAuth2.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Helper to create a GoogleClient instance
GoogleClient CreateGoogleClient()
{
    return new GoogleClient(new RequestFactory(), new ClientConfiguration
    {
        ClientId = app.Configuration["Google:ClientId"]!,
        ClientSecret = app.Configuration["Google:ClientSecret"]!,
        RedirectUri = "https://localhost:5001/auth/google/callback",
        Scope = "profile email"
    });
}

// Step 1: Redirect the user to Google's login page
app.MapGet("/auth/google", async () =>
{
    var client = CreateGoogleClient();
    var loginUri = await client.GetLoginLinkUriAsync();
    return Results.Redirect(loginUri);
});

// Step 2: Handle the callback after authentication
app.MapGet("/auth/google/callback", async (HttpContext context) =>
{
    var code = context.Request.Query["code"].ToString();
    if (string.IsNullOrEmpty(code))
        return Results.BadRequest("Missing authorization code.");

    var client = CreateGoogleClient();
    var userInfo = await client.GetUserInfoAsync(new NameValueCollection { { "code", code } });

    return Results.Ok(new
    {
        userInfo.Id,
        userInfo.FirstName,
        userInfo.LastName,
        userInfo.Email,
        AvatarUri = userInfo.AvatarUri?.ToString()
    });
});

app.Run();
```

## Email verification and provider-specific data

`UserInfo.EmailVerified` is an optional, provider-reported verification status: `true`, `false`, or `null` when unknown or not mapped by the client. Email verification is an [OpenID Connect standard claim](https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims), not a Google-specific concept. Providers can use different verification methods, so a shared property does not imply identical assurance across providers.

| Client | Source of `EmailVerified` | Scope of support |
|--------|---------------------------|------------------|
| `GoogleClient` | `email_verified` in userinfo | Only JSON booleans; absent, null, or non-boolean claims produce null. |
| `LinkedInClient` | `email_verified` in `/v2/userinfo` | Same strict, nullable handling of the optional claim. |
| `SalesforceClient` | `email_verified` in the identity URL response | Same strict, nullable handling; the existing identity URL and user ID are preserved. |
| `GitHubClient` | `verified` on the selected `/user/emails` record | Preserves the existing private-email fallback and selection order. A public profile email alone leaves verification unknown; no extra request or permission is added. |

GitHub verification belongs to the selected address, not any other verified address in the response. Its existing typed email parser and protected `bool Verified` API remain compatible; omitted verification is distinguished from explicitly false. See the [provider-claims reference](docs/provider-claims.md) for endpoint documentation, malformed-value behavior, and providers whose API support requires a separate migration. Clients not listed above leave `EmailVerified` null.

Provider-specific string values belong in the optional `UserInfo.ProviderData` dictionary. Interpret its keys together with `ProviderName`; they are not universal claims. The built-in clients copy only explicitly mapped values, not complete provider responses, arbitrary JSON objects, or tokens. There is no shared `HostedDomain` property.

`GoogleClient` maps only a string-valued `hd` claim to `ProviderData["hd"]`, preserving its contents unchanged, including case, whitespace, and empty strings. An absent, null, or non-string claim leaves `ProviderData` null. This is Google's hosted-domain claim, not a generic tenant identifier or a domain inferred from the email address. Other built-in clients leave `ProviderData` null. Google also permits missing `given_name` and `family_name`; these become null without discarding the remaining user information. See [Google's userinfo claim reference](https://developers.google.com/identity/openid-connect/reference#userinfo).

Read Google's hosted domain explicitly in its provider context:

```csharp
string? googleHostedDomain = null;
if (userInfo.ProviderName == "Google" &&
    userInfo.ProviderData != null &&
    userInfo.ProviderData.TryGetValue("hd", out var hostedDomain))
{
    googleHostedDomain = hostedDomain;
}
```

This reads metadata; it does not authorize access or link an account. A correctly typed string is not a validated or approved domain. Use `ProviderName` and `Id` (Google's `sub`) to identify a provider account, and apply an explicit application policy for account linking and domain restrictions. Google's authority over an email address depends on the account type; an external email address can remain marked verified even after ownership changes. A hosted-domain string alone does not establish email authority. See [Google's verification guidance](https://developers.google.com/identity/gsi/web/guides/verify-google-id-token). Preserving userinfo claims does not add ID-token validation or an automatic account-linking flow.

Older serialized `UserInfo` objects deserialize with both new properties null. Existing properties remain available; serialization includes the new properties according to the serializer's naming and null/default settings. With default `System.Text.Json` options, they appear as `EmailVerified` and `ProviderData`, including null values. Provider data stays nested rather than being flattened into the shared model. No provider-specific subclass or polymorphic serializer configuration is required; dictionary-key policies remain the consuming application's responsibility.

## Supported Services

| Provider | Client Class | Status | API Version | Auth Endpoint | Last Verified | Docs |
|----------|-------------|--------|-------------|---------------|---------------|------|
| GitHub | `GitHubClient` | Active | REST (default: 2022-11-28) | `github.com/login/oauth/authorize` | 2026-04-23 | [Docs](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps) |
| Google | `GoogleClient` | Active | OAuth2 v2 / UserInfo v3 | `accounts.google.com/o/oauth2/v2/auth` | 2026-04-23 | [Docs](https://developers.google.com/identity/protocols/oauth2/web-server) |
| Facebook | `FacebookClient` | Active | Graph API v25.0 | `www.facebook.com/v25.0/dialog/oauth` | 2026-04-23 | [Docs](https://developers.facebook.com/docs/facebook-login/guides/advanced/manual-flow) |
| Microsoft | `MicrosoftClient` | Active | Identity Platform v2.0 / Graph v1.0 | `login.microsoftonline.com/common/oauth2/v2.0/authorize` | 2026-04-23 | [Docs](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-auth-code-flow) |
| Asana | `AsanaClient` | Active | API v1 | `app.asana.com/-/oauth_authorize` | 2026-04-23 | [Docs](https://developers.asana.com/docs/oauth) |
| DigitalOcean | `DigitalOceanClient` | **Token contract mismatch** | OAuth 2.0 (legacy token parser) | `cloud.digitalocean.com/v1/oauth/authorize` | Not live-verified | [OAuth API](https://docs.digitalocean.com/reference/api/oauth/) |
| ExactOnline | `ExactOnlineClient` | Active | REST API v1 | `start.exactonline.nl/api/oauth2/authorize` | 2026-04-23 | [Docs](https://developers.exactonline.com/) |
| Fitbit | `FitbitClient` | Active | Web API v1 (user profile) | `www.fitbit.com/oauth2/authorize` | 2026-04-23 | [Docs](https://dev.fitbit.com/build/reference/web-api/authorization/) |
| Foursquare | `FoursquareClient` | **Deprecated** | v2 (OAuth deprecated) | `foursquare.com/oauth2/authorize` | 2026-04-23 | [Docs](https://docs.foursquare.com/) |
| LinkedIn | `LinkedInClient` | Active | OAuth v2 (OpenID Connect) | `www.linkedin.com/oauth/v2/authorization` | 2026-04-23 | [Docs](https://learn.microsoft.com/en-us/linkedin/consumer/integrations/self-serve/sign-in-with-linkedin-v2) |
| LoginCidadao | `LoginCidadaoClient` | Unknown | OpenID Connect v2 | `logincidadao.rs.gov.br/openid/connect/authorize` | 2026-04-23 | |
| MailRu | `MailRuClient` | Active | OAuth 2.0 | `connect.mail.ru/oauth/authorize` | 2026-04-23 | [Docs](https://api.mail.ru/docs/guides/oauth/) |
| Odnoklassniki | `OdnoklassnikiClient` | Active | OAuth 2.0 | `www.odnoklassniki.ru/oauth/authorize` | 2026-04-23 | [Docs](https://apiok.ru/en/ext/oauth/) |
| Salesforce | `SalesforceClient` | Active | OAuth 2.0 (Web Server Flow) | `login.salesforce.com/services/oauth2/authorize` | 2026-04-23 | [Docs](https://help.salesforce.com/s/articleView?id=sf.remoteaccess_oauth_web_server_flow.htm) |
| Spotify | `SpotifyClient` | Active | Web API v1 | `accounts.spotify.com/authorize` | 2026-04-23 | [Docs](https://developer.spotify.com/documentation/web-api/tutorials/code-flow) |
| Todoist | `TodoistClient` | Active | REST API v1 | `app.todoist.com/oauth/authorize` | 2026-04-23 | [Docs](https://developer.todoist.com/api/v1/) |
| X (Twitter) | `XClient` | Active | OAuth 1.0a / API v1.1 | `api.twitter.com/oauth/authenticate` | 2026-04-23 | [Docs](https://developer.x.com/en/docs/authentication/oauth-1-0a) |
| Uber | `UberClient` | **Deprecated userinfo endpoint** | OAuth v2 / userinfo v1 | `auth.uber.com/oauth/v2/authorize` | Not live-verified | [Legacy endpoint](https://developer.uber.com/docs/consumer-identity/references/api/v1/me-get) |
| VK (Vkontakte) | `VkClient` | Active | API v5.131 | `oauth.vk.com/authorize` | 2026-04-23 | [Docs](https://dev.vk.com/en/api/access-token/authcode-flow-user) |
| VSTS (Azure DevOps) | `VSTSClient` | **Deprecated (2026)** | Azure DevOps OAuth (deprecated Apr 2025) | `app.vssps.visualstudio.com/oauth2/authorize` | 2026-04-23 | [Docs](https://learn.microsoft.com/en-us/azure/devops/integrate/get-started/authentication/entra-oauth) |
| Windows Live | `WindowsLiveClient` | **Legacy (Working)** | Live SDK v5.0 | `login.live.com/oauth20_authorize.srf` | 2026-04-23 | [Migration Guide](https://learn.microsoft.com/en-us/onedrive/developer/rest-api/concepts/migrating-from-live-sdk) |
| Yahoo | `YahooClient` | **Retired userinfo endpoint** | OAuth 2.0 / Social Directory v1 | `api.login.yahoo.com/oauth2/request_auth` | Not live-verified | [Migration guide](https://developer.yahoo.com/oauth/social-directory-eol/) |
| Yandex | `YandexClient` | Active | OAuth 2.0 (Yandex ID) | `oauth.yandex.ru/authorize` | 2026-04-23 | [Docs](https://yandex.com/dev/id/doc/en/codes/code-url) |

The DigitalOcean, Uber, and Yahoo statuses describe the client implementations, not the availability of the providers' replacement APIs. Their source and documentation were reviewed on 2026-10-06; no authenticated live migration was tested. See the [migration assessment](docs/provider-claims.md#api-support-that-requires-separate-endpoint-work) for identity, permission, and compatibility requirements. These clients have not been upgraded to the replacement response contracts.

> **Removed providers** (Instagram, Xing): These providers' APIs have been retired or shut down. The client classes have been removed.
> - **Instagram**: Basic Display API shut down Dec 4, 2024. There is no consumer OAuth replacement — the remaining Instagram APIs are business/creator-only. [Announcement](https://developers.facebook.com/blog/post/2024/09/04/update-on-instagram-basic-display-api/)
> - **Xing**: OAuth 1.0a REST API discontinued. [dev.xing.com](https://dev.xing.com/) only hosts plugins.
>
> **Renamed providers**: `TwitterClient` → `XClient` (Twitter rebranded to X). The old class name is preserved as an obsolete derived class that produces a compiler error directing users to the new name.
>
> **Legacy (Working)**: `WindowsLiveClient` — Microsoft officially retired the Live SDK in Nov 2018 but the endpoints continue to function in production. Confirmed working in [Exceptionless](https://github.com/exceptionless/Exceptionless). For new integrations, use `MicrosoftClient` (Microsoft Identity Platform v2.0 + Graph). User IDs differ between the two platforms.
>
> **Deprecated providers**:
> - **Foursquare**: v2 consumer OAuth API deprecated. The replacement Places API v3 uses API keys, not OAuth.
> - **VSTS (Azure DevOps)**: Azure DevOps OAuth stopped accepting new app registrations in April 2025 and is scheduled for full retirement in 2026. Use `MicrosoftClient` with [Microsoft Entra ID OAuth](https://learn.microsoft.com/en-us/azure/devops/integrate/get-started/authentication/entra-oauth) instead

## Goals

- Simplicity in usage — even a newcomer can call a couple of methods and receive the expected result
- Well-documented, testable, and tested code
- Flexible, transparent, and easily understandable design
- Support for both fine-grained control and simple plug-and-play usage

## Dependencies

- [RestSharp](https://restsharp.dev)

## Contributors

- Constantin Titarenko (started development, defined library structure, released initial version)
- Blake Niemyjski (helped a lot to maintain the project, currently (since 2015) — top maintainer)
- [Andriy Somak](https://github.com/semack) (helped with improvements on configuration and extending the list of supported services)
- Sascha Kiefer (simplified extending the library with own provider implementations, added GitHub client)
- Krisztián Pócza (added LinkedIn (OAuth 2) client)
- [Jamie Houston](https://github.com/JamieHouston) (added a [Todoist client](OAuth2/Client/Impl/TodoistClient.cs))
- [Sasidhar Kasturi](https://github.com/skasturi) (added Uber, Spotify, Yahoo)
- [Jamie Dalton](https://github.com/daltskin) (added Visual Studio Team Services)

## Acknowledgements

Many thanks to [JetBrains](https://www.jetbrains.com/) company for providing free OSS licenses
for [**ReSharper**](https://www.jetbrains.com/resharper/) and [**dotCover**](https://www.jetbrains.com/dotcover/) -
these tools allow us to work on this project with pleasure!

Also we glad to have opportunity to use free [**Teamcity**](https://www.jetbrains.com/teamcity/) CI server
provided by [Codebetter.com](http://codebetter.com/) and [JetBrains](https://www.jetbrains.com/) -
many thanks for supporting OSS!

OAuth2 optimization would never be so simple without YourKit .NET profiler!
We appreciate kind support of open source projects by YourKit LLC -
the creator of innovative and intelligent tools for profiling .NET [**YourKit .NET Profiler**](https://www.yourkit.com/.net/profiler/index.jsp)
and Java applications [YourKit Java Profiler](https://www.yourkit.com/java/profiler/index.jsp).

## License

The MIT License (MIT)
Copyright (c) 2012-2013 Constantin Titarenko, Andrew Semack and others

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
