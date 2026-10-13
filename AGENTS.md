# Repository instructions

Before changing tests, read `OAuth2.Tests/readme.md` and the existing fixture for that provider. Follow its three-part names, alphabetical ordering, and exact `// Arrange`, `// Act`, `// Assert` markers for new or changed tests.

Keep changes focused. Preserve provider endpoints, scopes, and account identifiers unless a separately verified migration requires a change. Use sourced response fixtures; distinguish published examples, sanitized captures, and synthetic negative cases. Do not claim authenticated provider verification based on mocks.

Keep user documentation in `readme.md`; avoid duplicating implementation narratives. Use exact C# member names such as `UserInfo.Id` and exact API field names such as `id` or `sub`. Prefer `is true` for nullable boolean checks. Reuse existing XML documentation conventions for overrides.

Build and test using the commands in `OAuth2.Tests/readme.md`. Report the tested commit and any failures or unverified integration behavior. Do not weaken auditing, warning policies, or CI checks to make validation pass.
