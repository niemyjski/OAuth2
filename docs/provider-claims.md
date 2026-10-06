# Email-verification and provider-data contracts

## Shared model

`UserInfo.EmailVerified` is a nullable provider assertion about the returned email address. It is not a library-generated assurance level or an instruction to link a local account. `null` includes unknown, absent, and not mapped; it must not be interpreted as either `true` or `false`. A claim can be preserved even if the response omits `Email`, so consumers that need an email must require both a usable address and their chosen verification policy.

[OpenID Connect defines `email_verified`](https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims) but explicitly leaves verification methods dependent on the provider and its trust framework. This makes a nullable shared property appropriate without assuming that every provider implements it or verifies addresses equally.

`UserInfo.ProviderData` is an optional dictionary of explicitly mapped string values. Keys are interpreted in the context of `ProviderName`. The built-in clients do not copy arbitrary response fields, nested JSON, credentials, or access tokens into it. Currently only Google populates it, with `hd`; other clients leave it null. A Salesforce organization ID, Microsoft tenant ID, business account ID, and Google hosted domain are different concepts and must not be renamed into one shared `HostedDomain` field.

This additive dictionary keeps provider metadata on the existing `UserInfo` returned by the public API, without requiring consumers to recover a provider-specific subclass when serializing or deserializing that base type. It also avoids an ever-growing set of provider-specific top-level properties. Its string-only scope is intentional, not a generic raw-JSON container. The DTO remains mutable; only values obtained through a trusted provider flow are provider assertions, not values supplied by a caller or arbitrary serialized input.

## Implemented mappings

| Provider | Response used by this client | Mapping and evidence |
|----------|------------------------------|----------------------|
| Google | `/oauth2/v3/userinfo` | JSON boolean `email_verified` to `EmailVerified`; string `hd` to `ProviderData["hd"]`. [Google reference](https://developers.google.com/identity/openid-connect/reference#userinfo). |
| LinkedIn | `/v2/userinfo` | Optional JSON boolean `email_verified` to `EmailVerified`. [Sign In with LinkedIn](https://learn.microsoft.com/en-us/linkedin/consumer/integrations/self-serve/sign-in-with-linkedin-v2). |
| Salesforce | Identity URL returned in the token response's `id` | JSON boolean `email_verified` to `EmailVerified`. The existing identity URL, identifier, and photo mapping remain intact. [Identity URL response](https://developer.salesforce.com/docs/platform/mobile-sdk/guide/oauth-using-identity-urls.html). |
| GitHub | `/user`, with the existing `/user/emails` fallback only when the profile has no email | The selected email record's `verified` value to `EmailVerified`. [Email API](https://docs.github.com/en/rest/users/emails#list-email-addresses-for-the-authenticated-user). |

### Parsing and request behavior

Google, LinkedIn, and Salesforce accept only literal JSON booleans for `email_verified`, using exact property names. Missing, null, string, numeric, array, or object values produce null. `"true"` and `1` are not coerced. These clients explicitly use Bearer authorization for their user-information requests; the shared base client's authentication behavior is unchanged.

Google's `hd` is preserved only when it is a JSON string. Case, whitespace, empty strings, and escaped characters are preserved, not approved or normalized. Each response receives its own dictionary. The email suffix does not supply the claim. Missing `given_name` or `family_name` is also allowed without losing the remaining identity information.

GitHub preserves its existing selection order: first nonempty primary address, otherwise first nonempty verified address, otherwise first nonempty address. Verification comes from that exact record. An unverified primary address is not marked verified merely because a different secondary address is verified. Missing `verified` remains unknown. The protected `UserEmails.Verified` member remains `bool`, including for custom `ParseEmailAddresses` overrides; internal nullable state distinguishes omission from an explicitly assigned false.

GitHub's existing typed, case-insensitive email deserialization remains in place. Invalid non-boolean `verified` values still fail deserialization rather than acquiring the tolerant-null behavior used for the optional OIDC claims. When `/user` already supplies a public email, `EmailVerified` remains null and the client does not add a request or require the `user:email` permission just to obtain verification. This preserves request counts and existing scope requirements. This change does not add pagination or otherwise alter the existing fallback's response selection boundary.

## API support that requires separate endpoint work

The following providers document email verification, but their currently implemented client paths do not consume that response. They are not classified as providers that lack email verification.

| Provider | Documented capability | Why this change leaves it unmapped |
|----------|-----------------------|------------------------------------|
| DigitalOcean | `/v2/account` returns `account.email_verified`; the reference specifies `account:read`. [Account API](https://docs.digitalocean.com/reference/api/reference/account/). | The client currently parses the token response's `uid` and `info`, without an account API request. Adding a request must address permission availability, user/team identity association, and backward compatibility. [Current OAuth response](https://docs.digitalocean.com/reference/api/oauth/). |
| Uber | `/v3/me` documents boolean `email_verified`, subject to endpoint access approval and profile permissions. [Current user endpoint](https://developer.uber.com/docs/consumer-identity/references/api/v3/me-get). | The client calls `/v1/me`, whose `mobile_verified` describes a phone, not an email. A migration must cover endpoint access and changed identity/profile fields, not just add a property assignment. [Legacy endpoint](https://developer.uber.com/docs/riders/references/api/v1/me-get). |
| Yahoo | `/openid/v1/userinfo` documents `email_verified`. [Sign In With Yahoo](https://developer.yahoo.com/sign-in-with-yahoo/). | The client uses the Social Directory profile response and `xoauth_yahoo_guid`. Migration must verify scopes and identifier continuity. Yahoo documents the [Social Directory API shutdown](https://developer.yahoo.com/oauth/social-directory-eol/); parsing a new field does not repair that integration. |

No endpoint migration, additional permission, or new network request is introduced for these clients here.

## Values deliberately not treated as email verification

Mail.ru documents `is_verified` as confirmation of the user's **phone**. It is not mapped to `EmailVerified`. See the [users.getInfo response](https://api.mail.ru/docs/reference/rest/users-getinfo/).

Todoist's `verification_status` is an account state. Its documented `verified` category includes social sign-up, while other values include `unverified`, `blocked`, and `legacy`. Without a contract equating that state to verification of the returned email address, this client leaves email verification unknown. See [Todoist user properties](https://developer.todoist.com/api/v1/).

For Microsoft Graph, `mail`, `userPrincipalName`, and tenant-related identifiers are not substituted for an email-verification assertion or Google's `hd`. See the [Graph user resource](https://learn.microsoft.com/en-us/graph/api/resources/user?view=graph-rest-1.0). Facebook account-verification flags and a Google-looking email suffix are likewise not automatically promoted. The negative tests exercise these boundaries without asserting that every possible API offered by those companies has identical behavior.

Login Cidadao uses `/api/v2/person` in this library, not a standard OIDC userinfo response. Its implementation is not assigned OIDC claim semantics merely because its authorization endpoints use OpenID Connect.

No new mappings are added for Asana, ExactOnline, Fitbit, Foursquare, Odnoklassniki, Spotify, VK, VSTS, Windows Live, X, or Yandex. This records the implementation scope, not a guarantee that their entire API surface lacks verification capabilities. Before adding one, verify the exact endpoint, relevant scopes, association with the selected email, and current provider contract. For example, the legacy X `verify_credentials` reference currently redirects to the general documentation overview; this change does not infer verification from a nonempty email based on an unconfirmed legacy contract.

## Security and compatibility

Use the provider's stable subject identifier in its provider context. Do not automatically link accounts merely because their emails match. Google's [email-authority guidance](https://developers.google.com/identity/gsi/web/guides/verify-google-id-token) distinguishes Gmail, Google Workspace, and third-party email accounts: a historical verification flag does not always prove current control of an external mailbox.

The new fields do not implement callback-state validation, ID-token validation, domain authorization, or an account-linking policy. Those remain responsibilities of the consuming authentication flow. Tests use synthetic provider responses and mocked HTTP, not live accounts.

Older `UserInfo` JSON defaults the added fields to null. With default `System.Text.Json` options, the new fields are serialized as `EmailVerified` and `ProviderData`, including null values; exact output bytes therefore can change. Null/default suppression and naming policies remain caller-controlled. Nullable false is preserved under default-value suppression, metadata remains nested, and dictionary keys remain exact under the default key policy. A consumer's custom dictionary-key transformation can change its own persisted keys.
