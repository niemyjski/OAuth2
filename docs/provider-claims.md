# Email-verification and provider-data contracts

## Shared model

`UserInfo.EmailVerified` stores the provider's verification claim about the returned email address. The library does not verify the address or decide whether to link a local account. A `null` value means unknown, absent, or not mapped. Do not treat `null` as `true` or `false`.

The client can preserve a verification claim when the response omits `Email`. If your application needs an email, require a usable address. Apply your application's verification policy to that address.

[OpenID Connect defines `email_verified`](https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims). Each provider chooses its verification methods within its trust framework. The nullable property supports providers that omit this claim. It does not imply that all providers verify addresses in the same way.

`UserInfo.ProviderData` is an optional dictionary of explicitly mapped strings. Interpret each key together with `ProviderName`. Built-in clients exclude arbitrary response fields, nested JSON, credentials, and access tokens. Only Google adds data: its `hd` claim. Other clients leave the dictionary null.

A Salesforce organization ID, Microsoft tenant ID, business account ID, and Google hosted domain describe different things. Do not combine them in one shared `HostedDomain` field.

The dictionary keeps provider metadata on the existing `UserInfo` returned by the public API. Serialization does not require a provider-specific subclass or more top-level properties. The dictionary accepts strings, not raw JSON.

The DTO remains mutable. Trust a provider claim only when it comes from a trusted provider flow. A caller can change DTO values or supply arbitrary serialized data.

## Implemented mappings

| Provider | Response used by this client | Mapping and evidence |
|----------|------------------------------|----------------------|
| Google | `/oauth2/v3/userinfo` | JSON boolean `email_verified` to `EmailVerified`; string `hd` to `ProviderData["hd"]`. [Google reference](https://developers.google.com/identity/openid-connect/reference#userinfo). |
| LinkedIn | `/v2/userinfo` | Optional JSON boolean `email_verified` to `EmailVerified`. [Sign In with LinkedIn](https://learn.microsoft.com/en-us/linkedin/consumer/integrations/self-serve/sign-in-with-linkedin-v2). |
| Salesforce | Identity URL returned in the token response's `id` | JSON boolean `email_verified` to `EmailVerified`. The existing identity URL, identifier, and photo mapping remain intact. [Identity URL response](https://developer.salesforce.com/docs/platform/mobile-sdk/guide/oauth-using-identity-urls.html). |
| GitHub | `/user`, with the existing `/user/emails` fallback only when the profile has no email | The selected email record's `verified` value to `EmailVerified`. [Email API](https://docs.github.com/en/rest/users/emails#list-email-addresses-for-the-authenticated-user). |

### Parsing and request behavior

Google, LinkedIn, and Salesforce accept only literal JSON booleans for `email_verified`. Property names must match exactly. Missing, null, string, numeric, array, or object values produce null. The parser does not convert `"true"` or `1` to a boolean. These clients use Bearer authorization for user-information requests. The shared base client's authentication behavior is unchanged.

Google's client preserves `hd` only when it is a JSON string. It retains case, whitespace, empty strings, and escaped characters without normalization or approval. Each response has its own dictionary. The client does not derive `hd` from the email suffix. Missing `given_name` or `family_name` does not discard the remaining identity information.

GitHub keeps its existing email selection order:

1. Select the first nonempty primary address.
2. If none exists, select the first nonempty verified address.
3. If neither exists, select the first nonempty address.

Verification comes from the selected record. A verified secondary address does not make an unverified primary address verified. Missing `verified` remains unknown. The protected `UserEmails.Verified` member remains `bool`, including for custom `ParseEmailAddresses` overrides. Internal nullable state distinguishes an omitted value from an explicitly assigned false.

GitHub keeps its typed, case-insensitive email deserialization. Non-boolean `verified` values still cause deserialization to fail. They do not produce null as the optional OIDC claims do.

When `/user` supplies a public email, `EmailVerified` remains null. The client makes no extra request and needs no extra `user:email` permission to obtain verification. Request counts and required scopes stay unchanged. This change does not add pagination or change which fallback response records the client can select.

## API support that requires separate endpoint work

The following providers document email verification. Their current clients do not read the response that contains it. The missing mapping is a client limitation, not a claim that the provider lacks verification. This PR does not implement these migrations.

| Provider | Documented capability | Why this change leaves it unmapped |
|----------|-----------------------|------------------------------------|
| DigitalOcean | `/v2/account` returns `account.email_verified`; the reference specifies `account:read`. [Account API](https://docs.digitalocean.com/reference/api/reference/account/). | The client parses the token response's `uid` and `info`, without an account API request. The [current token example](https://docs.digitalocean.com/reference/api/oauth/#response) instead shows `info.uuid` and omits `uid`; their equivalence is not established here. |
| Uber | `/v3/me` documents boolean `email_verified`, subject to endpoint access approval and profile permissions. [Current user endpoint](https://developer.uber.com/docs/consumer-identity/references/api/v3/me-get). | The client calls deprecated `/v1/me`. Approval for the replacement and identity/profile compatibility must be verified. [Legacy endpoint](https://developer.uber.com/docs/riders/references/api/v1/me-get). |
| Yahoo | `/openid/v1/userinfo` documents `email_verified`. [Sign In With Yahoo](https://developer.yahoo.com/sign-in-with-yahoo/). | Yahoo documents GUID-to-`sub` compatibility and continued legacy-scope support, but also a changed profile response. The existing client still parses Social Directory data. [Migration guide](https://developer.yahoo.com/oauth/social-directory-eol/). |

This PR adds no endpoint migration, permission, or network request for these clients. Mocked tests do not prove live endpoint access, granted permissions, or compatibility with stored account identifiers.

### DigitalOcean: resolve identity and permission contracts first

The [account schema](https://docs.digitalocean.com/reference/api/reference/account/) distinguishes the user's `account.uuid` from `account.team.uuid`. Only the user's verification value could describe the returned user email. The team identifier is neither a hosted domain nor an alternative user identifier.

The current parser always calls `GetProperty("uid")`. The documented OAuth token example omits `uid`, so that response would fail this lookup. This existing contract mismatch does not prove that every live grant omits the old field. This review found no documented conversion from legacy `uid` to the newer UUID.

Before migration, establish how the new identifier relates to stored IDs. Verify the granted account-read permission. Define how the extra account request handles permission denial, rate limiting, cancellation, and service failure. Do not silently replace an existing ID. Do not attach verification to a different email record.

### Uber: approval and response changes prevent a blanket upgrade

The [v3 reference](https://developer.uber.com/docs/consumer-identity/references/api/v3/me-get) requires Uber approval. Its response uses `given_name`, `family_name`, and an encrypted `sub`. The current parser reads `first_name` and `last_name`. It does not set `UserInfo.Id`. Changing only the request URL would lose names and leave the new identity contract unimplemented.

The reference associates `email_verified` with `profile`, but its profile-only example omits the value. Treat a missing claim as unknown. Do not infer email verification from an address, phone verification, or verification timestamps. Before enabling a migration, confirm approval and granted permissions for the application.

### Yahoo: documented migration candidate, not a transparent replacement

Yahoo's [migration guide](https://developer.yahoo.com/oauth/social-directory-eol/) gives June 30, 2020 as the old service's end-of-life date. It maps `guid` to `sub` and confirms support for legacy scopes. It replaces the email array with one preferred email. Basic and extended legacy scopes return different fields. The guide resolves the documented identity mapping, but application compatibility still needs testing.

The [discovery document](https://api.login.yahoo.com/.well-known/openid-configuration) confirms the userinfo URL, public subject type, and `openid`/`profile`/`email` scopes. It also lists `client_secret_basic` and `client_secret_post`. The [sign-in guide](https://developer.yahoo.com/sign-in-with-yahoo/) deprecates `xoauth_yahoo_guid`. Do not require that field in a new implementation. Do not use an unvalidated ID token as a substitute.

A migration needs a flat-profile parser, strict nullable email verification, and mappings for optional fields and avatars. Test compatibility with the public `UserProfileGUID` property and protected overrides.

Compare stored GUIDs with authenticated userinfo subjects. Reject contradictory supplied identities. Do not link those accounts by email. Document required grants and changes to returned data. Do not silently add broader scopes.

### Acceptance criteria for a separate migration

If existing behavior must change, provide an explicitly selected client or a versioned migration with compatibility notes. Complete these checks before release:

1. Verify authenticated responses with an application that has the required provider approval and grants. Compare subjects with stored identities or historical records. Synthetic examples alone are insufficient. Do not expect Yahoo's retired endpoint to provide a live baseline.
2. Test minimum permissions and supported legacy grants. Test absent email or verification, false verification, malformed claims, and contradictory identities. Include personal and team contexts where relevant.
3. Test exact request origins, paths, Bearer headers, request counts, and scope preservation. Test cancellation and error behavior. Keep tokens out of provider metadata, URLs, and diagnostic exceptions.
4. Test the public DTO, protected extension points, serialization, and state isolation between calls. Build every target. Run the relevant test matrix. Use alphabetically ordered three-part test names. Separate each test into Arrange, Act, and Assert sections.

These release checks remain open for the separate migrations. This assessment used no live authorized provider sessions or application account databases. None of the reviewed replacement contracts defines a common hosted-domain claim for `UserInfo`.

## Values deliberately not treated as email verification

Mail.ru documents `is_verified` as confirmation of the user's **phone**. It is not mapped to `EmailVerified`. See the [users.getInfo response](https://api.mail.ru/docs/reference/rest/users-getinfo/).

Todoist's `verification_status` describes an account state. Its documented `verified` category includes social sign-up. Other values include `unverified`, `blocked`, and `legacy`. That contract does not establish verification of the returned email address, so the client leaves email verification unknown. See [Todoist user properties](https://developer.todoist.com/api/v1/).

Microsoft Graph's `mail`, `userPrincipalName`, and tenant identifiers do not replace an email-verification claim or Google's `hd`. See the [Graph user resource](https://learn.microsoft.com/en-us/graph/api/resources/user?view=graph-rest-1.0). Facebook account-verification flags and a Google-looking email suffix also do not establish these claims. The negative tests cover these mappings. They do not describe every API these providers offer.

Login Cidadao uses `/api/v2/person` in this library, not a standard OIDC userinfo response. Its OpenID Connect authorization endpoints do not establish OIDC claim semantics for that response.

This change adds no mappings for Asana, ExactOnline, Fitbit, Foursquare, Odnoklassniki, Spotify, VK, VSTS, Windows Live, X, or Yandex. Their other APIs can have capabilities outside this implementation's scope. Before adding a mapping, verify the exact endpoint, relevant scopes, and current provider contract. Confirm that verification belongs to the selected email.

The legacy X `verify_credentials` reference redirects to the general documentation overview. That redirect does not confirm the legacy contract. The client does not infer verification from a nonempty email.

## Security and compatibility

Use the provider's stable subject identifier together with the provider name. Do not automatically link accounts because their emails match. Google's [email-authority guidance](https://developers.google.com/identity/gsi/web/guides/verify-google-id-token) distinguishes Gmail, Google Workspace, and third-party email accounts. A historical verification flag does not always prove current control of an external mailbox.

The application's authentication flow remains responsible for callback-state validation, PKCE, ID-token validation, domain authorization, and account-linking policy. The new fields do not provide these protections.

Generate an unpredictable, single-use state value. Bind it to the initiating user-agent session. Validate callback state before calling `GetTokenAsync` or `GetUserInfoAsync`.

Client instances retain mutable state and tokens. Use a separate instance for each flow. Never share an instance concurrently. Tests use synthetic provider responses and mocked HTTP, not live accounts.

Older `UserInfo` JSON defaults the added fields to null. Default `System.Text.Json` options write `EmailVerified` and `ProviderData`, including null values. The output bytes can therefore change. Callers still control null/default suppression and naming policies.

Nullable false survives default-value suppression. Metadata remains nested. Dictionary keys remain exact under the default key policy. An application's custom dictionary-key policy can change its stored keys.
