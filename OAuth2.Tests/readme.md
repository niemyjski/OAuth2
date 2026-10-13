# Test conventions

Use the existing NUnit, FluentAssertions, and NSubstitute dependencies.

- Name new or changed tests `Method_Scenario_ExpectedResult` and sort methods alphabetically, ignoring case.
- Use separate `// Arrange`, `// Act`, and `// Assert` sections, with that exact capitalization. Fixture setup may perform shared arrangement; identify that when a test needs no additional setup.
- Start provider tests from a documented response example or a sanitized captured fixture. Record the endpoint, source, and any omitted or redacted fields. [ProviderResponses](TestHelpers/ProviderResponses.cs) contains the sourced baselines used by the claim tests; these are not live captures from this repository.
- Label synthetic contract variations and deliberately invalid inputs. Keep one case per distinct behavior or relevant JSON type, not arbitrary spelling or address permutations. Keep expected results explicit.
- Preserve `/* lang=json */` on JSON literals. API field names retain their documented casing; C# members retain theirs, such as `UserInfo.Id`.

## Testable clients

Use a small nested descendant to expose a protected parser or endpoint, matching existing client tests. Inject the request factory and configuration into its constructor. Use the real public client for HTTP-flow tests; subclass it only when testing a protected override.

Keep parser and serialization tests independent of HTTP setup. The two HTTP-flow fixtures reuse [OAuth2ClientTestBase](TestHelpers/OAuth2ClientTestBase.cs) for mock requests and deterministic disposal. Override `SetUp` only for provider setup and call `base.SetUp()` first. The production `BeforeGetUserInfo` hook has an empty base implementation; it does not need a redundant base call.

Serialization fixtures test the library's DTO format, not provider response JSON. Keep coverage for nullable verification, defaults, and nested provider data without adding migration machinery.

## Verification

```sh
dotnet build OAuth2.slnx --configuration Release
dotnet test OAuth2.Tests/OAuth2.Tests.csproj --configuration Release --no-build --no-restore
```

The build covers all library targets; the test project runs on .NET 10. Passing mocks do not verify provider approval, live grants, or existing account identifiers. Never commit credentials or private user data. Report the tested revision and distinguish executed tests from documentation-only verification.
