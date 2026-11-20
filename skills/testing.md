# Testing

How to test this plugin, from a single method to a page in a browser. A change is done when the tests for every layer it touches pass.

## The Rule

Every source file has a test file, and every test proves that given inputs produce the expected output. A bug fix starts with a test that fails without the fix.

## Layers

Test each behavior at the lowest layer that can prove it. Go up a layer only for what the lower one cannot reach.

| Layer | Proves | Runs | Time |
| :--- | :--- | :--- | :--- |
| Unit | One class or function gives the right output for each input | `dotnet test` | Seconds |
| Stateful service | A service reads and writes the plugin configuration and the invite status file correctly | `dotnet test` | Seconds |
| Controller | A route's status codes, guards, and response shape | `dotnet test` | Seconds |
| Flow | A whole invite redemption, from the request to the created user in its group | `dotnet test` | Seconds |
| Page script | A config page script parses and its Base imports exist | `node --check` | Seconds |
| Page in a browser | A config page draws, loads, and saves the right config | A test server or a browser harness | Minutes |

| A change to | Needs |
| :--- | :--- |
| A utility, model, or validator | Unit |
| `GroupService`, `InviteService`, `InviteStatusStore`, or a scheduled task | Stateful service |
| A controller or route attribute | Controller |
| Invite redemption, password rules at signup, or group assignment of a new user | Flow |
| A config page script | Page script and page in a browser |
| A config page's markup or CSS | Page in a browser |

## Layout

The test project is flat. Name each test file after its subject.

| Source | Test |
| :--- | :--- |
| `Jellyfin.Plugin.UserManagement/Utilities/PasswordValidator.cs` | `Jellyfin.Plugin.UserManagement.Tests/PasswordValidatorTests.cs` |
| `Jellyfin.Plugin.UserManagement/Services/InviteAttemptLimiter.cs` | `Jellyfin.Plugin.UserManagement.Tests/InviteAttemptLimiterTests.cs` |
| `Jellyfin.Plugin.UserManagement/Api/InviteController.cs` | `Jellyfin.Plugin.UserManagement.Tests/InviteControllerValidationTests.cs` |

A subject with several kinds of test takes a file per kind, such as `InviteServiceTests.cs` and `InviteServiceStatefulTests.cs`.

## C# Tests

Tests use xUnit with plain `Assert`, and NSubstitute for Jellyfin interfaces.

```csharp
/// <summary>
/// A password shorter than the group's minimum is refused with one error.
/// True: a member cannot set a password the group's rules forbid.
/// False: a short password would pass and the rules would mean nothing.
/// </summary>
[Fact]
public void Validate_TooShort_ReturnsError()
{
    var policy = new PasswordPolicy { Enabled = true, MinLength = 8 };

    var errors = PasswordValidator.Validate("abc", policy);

    Assert.Single(errors);
}
```

### Naming and Comments

* Name the class `<Subject>Tests` and each method `Method_Scenario_Expectation`.
* Give the class a one sentence summary of what its subject must guarantee.
* Give each new test the three line summary. What it proves, what passing means, and what failing would cause.

### Writing a Test

* Arrange, act, and assert, separated by blank lines.
* Assert one behavior per test. Several asserts are fine when they check one outcome.
* Use `[Theory]` with `[InlineData]` for one rule over several inputs.
* Test the edges. Null, empty, an administrator, an expired group, a locked invite, and a wrong PIN.
* Never sleep. Pass a clock in, and control it from the test.
* A test that fails once in many runs has found a race. Fix the source or the test, and never run it again until it passes.
* A test never reaches the network or a real Jellyfin.

### Shared Support

| Support | Stands in for |
| :--- | :--- |
| `TestSupport` | A fresh plugin instance with an in memory configuration, and the services built over it |
| `FakeUserServer` | An `IUserManager` that keeps real per user state and raises the user created event the way Jellyfin does |

* Keep shared support in its own file, with a header comment like any other class.
* Never copy a fake into a test file. Extend the shared one.
* A fake records what it was asked so the test can assert on it.

```bash
dotnet test
```

## Page Scripts

There are no JavaScript tests in this repository yet. Every page script must at least parse, and every name it imports from Base must exist.

```bash
for f in Jellyfin.Plugin.UserManagement/Web/*.js; do node --check "$f"; done
```

```bash
grep -o "export function create[A-Za-z]*" ~/.nuget/packages/jpkribs.jellyfin.base/<version>/assets/jpkribs_shared.d.ts
```

Check each name in the import list of `usermanagement_shared.js` against the typings of the Base version the csproj names.

## Pages in a Browser

Look at every changed page before calling it done.

* **On a test server.** Build with `./build.sh`, unzip the zip from `dist/` into a test Jellyfin's `plugins` folder, restart it, and open each tab. Never use a server real users depend on, since saving a group applies it to its members.
* **In a browser harness.** When no test server is available, open the Groups, Invites or Resets tab, or the signup page, with the screenshot kit in `tools/screenshots/`, as its [README](../tools/screenshots/README.md) describes. For another page, serve it the same way. Map `configurationpage?name=<file>` to the plugin's files and `usermanagement_jpkribs_shared.*` to Base's `assets/jpkribs_shared.*`. Stub `ApiClient`, `Dashboard`, and `LibraryMenu` with sample config, load the page's HTML into the document, call the module's default export with the page element, and dispatch `viewshow` on it.

In either, check that the page loads with no console error, that each changed control saves the config it should, and that the page works at 375 px wide. Say which of the two you used in the report.

## Checklist

* [ ] Every changed source file has tests.
* [ ] A bug fix has a test that fails without it.
* [ ] Edges are covered, including null, empty, administrators, and expired or locked records.
* [ ] Shared support comes from `TestSupport` and `FakeUserServer`.
* [ ] `dotnet test` passes.
* [ ] Every page script passes `node --check`, and its Base imports exist.
* [ ] Every changed page was opened, on a test server or in a browser harness.
