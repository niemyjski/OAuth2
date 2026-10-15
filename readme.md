# OAuth2

[![Build](https://github.com/titarenko/OAuth2/workflows/Build/badge.svg)](https://github.com/titarenko/OAuth2/actions)
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

## Usage

Create a client with your application's registered credentials and redirect URI:

```csharp
using OAuth2.Client.Impl;
using OAuth2.Configuration;
using OAuth2.Infrastructure;

var client = new GoogleClient(new RequestFactory(), new ClientConfiguration
{
    ClientId = "<client-id>",
    ClientSecret = "<client-secret>",
    RedirectUri = "https://localhost:5001/auth/google/callback",
    Scope = "profile email"
});
```

Call `GetLoginLinkUriAsync(state)` to obtain the login URL. Your application must generate unpredictable, single-use state, bind it to the initiating browser session, and validate the returned value before exchanging the authorization code. After validation, pass the callback parameters as a `NameValueCollection` to `GetUserInfoAsync` to obtain a `UserInfo`.

The library exposes callback state; it does not validate it, manage browser sessions, or validate ID tokens. Use a tested authentication handler or correlation store for those responsibilities. Create a client per authentication flow; do not share an instance concurrently. See [Google's authorization-code flow](https://developers.google.com/identity/protocols/oauth2/web-server).

## Email verification

`UserInfo.EmailVerified` is nullable: `true` and `false` preserve the provider's assertion; `null` means unknown or not mapped. Verification methods differ between providers. The following clients populate it:

| Client | Source |
|--------|--------|
| Google | Boolean `email_verified` in [userinfo](https://developers.google.com/identity/openid-connect/reference). |
| LinkedIn | Optional boolean `email_verified` in [`/v2/userinfo`](https://learn.microsoft.com/en-us/linkedin/consumer/integrations/self-serve/sign-in-with-linkedin-v2). |
| Salesforce | Boolean `email_verified` in the existing [identity URL response](https://developer.salesforce.com/docs/platform/mobile-sdk/guide/oauth-using-identity-urls.html). |
| GitHub | `verified` on the selected [`/user/emails`](https://docs.github.com/en/rest/users/emails#list-email-addresses-for-the-authenticated-user) record. A public profile email alone leaves verification unknown without adding a request. |

Google, LinkedIn, and Salesforce accept only JSON booleans for verification; missing, null, or other types become `null`. GitHub retains its existing typed email parser and primary/verified/first-email selection order. Verification belongs to the selected address, not another address in the response.

Google also maps a string `hd` to `UserInfo.ProviderData["hd"]`. Interpret this key only for `ProviderName == "Google"`. It is not a domain inferred from the email address or a universal tenant identifier. Other clients leave `ProviderData` null; raw responses and tokens are not copied into it.

Identify accounts by `ProviderName` and `UserInfo.Id`. Verification metadata does not authorize account linking; apply an explicit application policy and [Google's email-authority guidance](https://developers.google.com/identity/gsi/web/guides/verify-google-id-token).

Older `UserInfo` JSON deserializes with both new properties set to null. Default `System.Text.Json` serialization adds `EmailVerified` and `ProviderData`, including null values. Check consumers that require an exact JSON schema; naming and null-value settings remain under your application's control.

## Supported Services

| Provider | Client Class | Status | API Version | Auth Endpoint | Last Verified | Docs |
|----------|-------------|--------|-------------|---------------|---------------|------|
| GitHub | `GitHubClient` | Active | REST (default: 2022-11-28) | `github.com/login/oauth/authorize` | 2026-04-23 | [Docs](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps) |
| Google | `GoogleClient` | Active | OAuth2 v2 / UserInfo v3 | `accounts.google.com/o/oauth2/v2/auth` | 2026-04-23 | [Docs](https://developers.google.com/identity/protocols/oauth2/web-server) |
| Facebook | `FacebookClient` | Active | Graph API v25.0 | `www.facebook.com/v25.0/dialog/oauth` | 2026-04-23 | [Docs](https://developers.facebook.com/docs/facebook-login/guides/advanced/manual-flow) |
| Microsoft | `MicrosoftClient` | Active | Identity Platform v2.0 / Graph v1.0 | `login.microsoftonline.com/common/oauth2/v2.0/authorize` | 2026-04-23 | [Docs](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-auth-code-flow) |
| Asana | `AsanaClient` | Active | API v1 | `app.asana.com/-/oauth_authorize` | 2026-04-23 | [Docs](https://developers.asana.com/docs/oauth) |
| DigitalOcean | `DigitalOceanClient` | **Legacy token response** | OAuth 2.0 | `cloud.digitalocean.com/v1/oauth/authorize` | 2026-10-13 (docs) | [OAuth API](https://docs.digitalocean.com/reference/api/oauth/) |
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
| Uber | `UberClient` | **Deprecated userinfo endpoint** | OAuth v2 / userinfo v1 | `auth.uber.com/oauth/v2/authorize` | 2026-10-13 (docs) | [Legacy endpoint](https://developer.uber.com/docs/consumer-identity/references/api/v1/me-get) |
| VK (Vkontakte) | `VkClient` | Active | API v5.131 | `oauth.vk.com/authorize` | 2026-04-23 | [Docs](https://dev.vk.com/en/api/access-token/authcode-flow-user) |
| VSTS (Azure DevOps) | `VSTSClient` | **Deprecated (2026)** | Azure DevOps OAuth (deprecated Apr 2025) | `app.vssps.visualstudio.com/oauth2/authorize` | 2026-04-23 | [Docs](https://learn.microsoft.com/en-us/azure/devops/integrate/get-started/authentication/azure-devops-oauth) |
| Windows Live | `WindowsLiveClient` | **Legacy (Working)** | Live SDK v5.0 | `login.live.com/oauth20_authorize.srf` | 2026-04-23 | [Migration Guide](https://learn.microsoft.com/en-us/onedrive/developer/rest-api/concepts/migrating-from-live-sdk) |
| Yahoo | `YahooClient` | **Retired userinfo endpoint** | OAuth 2.0 / Social Directory v1 | `api.login.yahoo.com/oauth2/request_auth` | 2026-10-13 (docs) | [Migration guide](https://developer.yahoo.com/oauth/social-directory-eol/) |
| Yandex | `YandexClient` | Active | OAuth 2.0 (Yandex ID) | `oauth.yandex.ru/authorize` | 2026-04-23 | [Docs](https://yandex.com/dev/id/doc/en/codes/code-url) |

The three `(docs)` entries record source/documentation checks, not authenticated logins. DigitalOcean's OAuth 2.0 server uses `/v1/oauth`; that is not a protocol downgrade. Its current token example supplies `info.uuid`, while the existing parser expects `uid`. Replacement response contracts for DigitalOcean, Uber, and Yahoo require explicit account-identity and permission compatibility checks.

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
