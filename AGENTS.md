# Repository instructions

Keep changes focused and extend the existing fixture for the provider or model. Do not introduce feature-specific or cross-provider test classes when an appropriate fixture already exists.

Tests use NUnit, FluentAssertions, and NSubstitute. Name new or changed tests `Method_Scenario_ExpectedResult`, order methods alphabetically ignoring case, and use exact `// Arrange`, `// Act`, `// Assert` sections. Reuse `MockHttpMessageHandler` for HTTP tests and dispose owned resources. Use small nested descendants only to expose or override protected members; avoid adding a test inheritance hierarchy.

Base provider fixtures on published examples or sanitized captures, with source and redactions identified. Label deliberate mutations and malformed inputs; test distinct behaviors rather than arbitrary casing/address permutations. Retain `/* lang=json */` on JSON literals. Never commit credentials or private profiles, or describe mocks as live provider verification.

Put public API usage in `readme.md`, not implementation histories. Preserve exact names such as `UserInfo.Id`, `id`, and `sub`. Prefer `is true` for nullable booleans. Do not silently change endpoints, scopes, or stored account identifiers.

Validate with a .NET 10 SDK and the .NET 8/10 runtimes:

```sh
dotnet build OAuth2.slnx --configuration Release
dotnet test OAuth2.Tests/OAuth2.Tests.csproj --configuration Release --no-build --no-restore
```

Report the tested revision and actual results. The standard test project runs on .NET 8 and .NET 10; netstandard2.0 is built, not independently executed by those commands. Do not disable auditing, warning policies, or CI checks to obtain a pass.
