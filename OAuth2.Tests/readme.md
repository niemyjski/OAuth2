# Test conventions

Use NUnit, FluentAssertions, and NSubstitute, as the existing tests do.

- Name tests `Method_Scenario_ExpectedResult`.
- Sort test methods alphabetically within each fixture, ignoring case.
- Separate each test with `// arrange`, `// act`, and `// assert` comments.
- Use test cases for variations of the same behavior. Keep the expected result explicit.
- Assert observable results and request contracts. Do not copy production logic into expected-value calculations.
- Keep pure parsing and serialization fixtures independent of HTTP setup.

## HTTP-flow tests

Inherit from [OAuth2ClientTestBase](TestHelpers/OAuth2ClientTestBase.cs) when a fixture tests mocked HTTP flows. The base creates fresh HTTP resources, a request factory, and client configuration for each test. It disposes those resources after the test.

Call `ConfigureEndpoints` with the expected base URIs. Add mock responses through `Handler.EnqueueResponse`. Assert request counts, URLs, and authorization headers where the behavior requires them. Keep provider-specific setup and assertions in the fixture.

If you override `SetUp`, call `base.SetUp()` first. Do not add separate cleanup for resources owned by the base. See [ProviderUserInfoTests](Client/Impl/ProviderUserInfoTests.cs) and [GitHubEmailVerificationTests](Client/Impl/GitHubEmailVerificationTests.cs).

## Run tests

Run these commands from the repository root:

```sh
dotnet build OAuth2.slnx --configuration Release
dotnet test OAuth2.Tests/OAuth2.Tests.csproj --configuration Release --no-build
```

The build covers all library targets. The test project runs on .NET 10. Mocked tests do not verify authenticated provider access.
