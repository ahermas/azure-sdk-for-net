# Using the TestFramework
To start using the Test Framework, add a project reference using the alias `AzureCoreTestFramework` into your test `.csproj`:

``` xml
<Project Sdk="Microsoft.NET.Sdk">

...
   <ProjectReference Include="$(AzureCoreTestFramework)" />
...

</Project>

```
As an example, see the [Template](https://github.com/Azure/azure-sdk-for-net/blob/master/sdk/template/Azure.Template/tests/Azure.Template.Tests.csproj#L15) project.

## Sync-async tests

The test framework provides the ability to write tests using async client methods and automatically run them using sync overloads. To write sync-async client tests, inherit from `ClientTestBase` class and use the `InstrumentClient` method to wrap your client into a proxy class that automatically forwards async calls to their sync overloads.

``` C#
public class ConfigurationLiveTests: ClientTestBase
{
    public ConfigurationLiveTests(bool isAsync) : base(isAsync)
    {
    }

    private ConfigurationClient GetClient() =>
        InstrumentClient(
            new ConfigurationClient(
                ..., 
                Recording.InstrumentClientOptions(
                    new ConfigurationClientClientOptions())));
    }

    public async Task DeleteSettingNotFound()
    {
        ConfigurationClient service = GetClient();

        var response = await service.DeleteAsync("Setting");

        Assert.AreEqual(204, response.Status);
        response.Dispose();
    }
}
```

In the test explorer, async tests will display as `TestClassName(true)` and sync tests as `TestClassName(false)`.

When using sync-async tests with recorded tests two sessions files will be generated - the async test session will have `Async.json` suffix.

You can disable the sync-forwarding for an individual test by applying the `[AsyncOnly]` attribute to the test method.


__Limitation__: all method calls/properties that are being used have to be `virtual`.

## Test environment and live test resources

Follow the [live test resources management](../../eng/common/TestResources/README.md) to create a live test resources deployment template and get it deployed. The deployment template should be named `test-resources.json` and will live under your service directory.

To use the environment provided by the `New-TestResources.ps1`, create a class that inherits from `TestEnvironment` and exposes required values as properties:

``` C#
public class AppConfigurationTestEnvironment : TestEnvironment
{
    // Call the base constructor passing the service directory name
    public AppConfigurationTestEnvironment() : base("appconfiguration")
    {
    }

    // Variables retrieved using GetRecordedVariable will be recorded in recorded tests
    // Argument is the output name in the test-resources.json
    public string ConnectionString => GetRecordedVariable("APPCONFIGURATION_CONNECTION_STRING");
    // Variables retrieved using GetVariable will not be recorded but the method will throw if the variable is not set
    public string SystemAssignedVault => GetVariable("IDENTITYTEST_IMDSTEST_SYSTEMASSIGNEDVAULT");
    // Variables retrieved using GetOptionalVariable will not be recorded and the method will return null if variable is not set
    public string TestPassword => GetOptionalVariable("AZURE_IDENTITY_TEST_PASSWORD") ?? "SANITIZED";
}
```

**NOTE:** Make sure that variables containing secret values are not recorded or are sanitized.

You can now retrieve these values in tests:

``` C#
public class ConfigurationLiveTests : RecordedTestBase<AppConfigurationTestEnvironment>
{
    [Test]
    public async Task DeleteSetting()
    {
        var connectionString = TestEnvironment.ConnectionString;
        var password = TestEnvironment.TestPassword;
        //...
    }
}
```

And samples:

``` C#
public partial class ConfigurationSamples: SamplesBase<AppConfigurationTestEnvironment>
{
    [Test]
    public void HelloWorld()
    {
        var connectionString = TestEnvironment.ConnectionString;

        #region Snippet:AzConfigSample1_CreateConfigurationClient
        var client = new ConfigurationClient(connectionString);
        #endregion
    }
}
```

## TokenCredential

If a test or sample uses `TokenCredential` to construct the client use `TestEnvironment.Credential` to retrieve it.

``` C#
    public abstract class KeysTestBase : RecordedTestBase<KeyVaultTestEnvironment>
    {
        internal KeyClient GetClient() =>
            InstrumentClient(
                new KeyClient(
                    new Uri(TestEnvironment.KeyVaultUrl),TestEnvironment.Credential,
                    Recording.InstrumentClientOptions(
                        new KeyClientOptions())));
        }
    }

```

## Recorded tests

The test framework provides an ability to record HTTP requests and responses and replay them for offline test runs. This allows the full suite of tests to be run as part of PR validation without running live tests. In general, live tests are run as part of a separate internal pipeline that runs nightly.

To use recorded test functionality inherit from `RecordedTestBase<T>` class and use `Recording.InstrumentClientOptions` method when creating the client instance. Pass the test environment class as a generic argument to `RecordedTestBase`.


``` C#
public class ConfigurationLiveTests: RecordedTestBase<AppConfigurationTestEnvironment>
{
    public ConfigurationLiveTests(bool isAsync) : base(isAsync)
    {
    }

    private ConfigurationClient GetClient() =>
        InstrumentClient(
            new ConfigurationClient(
                ..., 
                Recording.InstrumentClientOptions(
                    new ConfigurationClientClientOptions())));
    }

    public async Task DeleteSettingNotFound()
    {
        ConfigurationClient service = GetClient();

        var response = await service.DeleteAsync("Setting");

        Assert.AreEqual(204, response.Status);
        response.Dispose();
    }
}
```

By default tests are run in playback mode. To change the mode use the `AZURE_TEST_MODE` environment variable and set it to one of the followind values: `Live`, `Playback`, `Record`.

In development scenarios where it's required to change mode quickly without restarting the Visual Studio use the two-parameter constructor of `RecordedTestBase` to change the mode:

``` C#
public class ConfigurationLiveTests: RecordedTestBase<AppConfigurationTestEnvironment>
{
    public ConfigurationLiveTests(bool isAsync) : base(isAsync, RecordedTestMode.Record)
    {
    }
}
```

## Recording

When tests are run in recording mode, session records are saved to `artifacts/bin/<ProjectName>/<TargetFramework>/SessionRecords` directory. You can copy recordings to the project directory manually or by executing `dotnet msbuild /t:UpdateSessionRecords` in the test project directory.

__NOTE:__ recordings are copied from `netcoreapp2.1` directory by default, make sure you are running the right target framework.

## Sanitizing

Secrets that are part of requests, responses, headers, or connections strings should be sanitized before saving the record.
**Do not check in session records containing secrets.** Common headers like `Authentication` are sanitized automatically, but if custom logic is required and/or if request or response body need to be sanitized, the `RecordedTestSanitizer` should be used as an extension point.

For example:

``` C#
    public class ConfigurationRecordedTestSanitizer : RecordedTestSanitizer
    {
        public override string SanitizeVariable(string variableName, string environmentVariableValue) =>
            variableName switch
            {
                "APPCONFIGURATION_CONNECTION_STRING" => SanitizeConnectionString(environmentVariableValue),
                _ => base.SanitizeVariable(variableName, environmentVariableValue)
            };

        private static string SanitizeConnectionString(string connectionString)
        {
            const string secretKey = "secret";
            var parsed = ConnectionString.Parse(connectionString, allowEmptyValues: true);

            // Configuration client expects secret to be base64 encoded so we can't use the placeholder
            parsed.Replace(secretKey, string.Empty);
            return parsed.ToString();
        }
    }
```
The above example also illustrates a common pattern where you need to sanitize the connection string, but you must ensure that the account key is base64 encoded or else the test will fail your connection string validation when run in Playback mode. In such cases, you need to override the default placeholder that is used to ensure the value is base64.

Another sanitizer property that is available for sanitizing Json payloads is the `JsonPathSanitizers`. This property contains a list of [Json Path](https://www.newtonsoft.com/json/help/html/QueryJsonSelectToken.htm) format strings that will be validated against the body. If a match exists, the value will be sanitized.

```c#
    public class FormRecognizerRecordedTestSanitizer : RecordedTestSanitizer
    {
        private const string SanitizedSasUri = "https://sanitized.blob.core.windows.net";

        public FormRecognizerRecordedTestSanitizer()
            : base()
        {
            JsonPathSanitizers.Add("$..accessToken");
            JsonPathSanitizers.Add("$..source");
        }

        public override void SanitizeHeaders(IDictionary<string, string[]> headers)
        {
            if (headers.ContainsKey(Constants.AuthorizationHeader))
            {
                headers[Constants.AuthorizationHeader] = new[] { SanitizeValue };
            }

            base.SanitizeHeaders(headers);
        }

        public override string SanitizeVariable(string variableName, string environmentVariableValue)
        {
            return variableName switch
            {
                FormRecognizerTestEnvironment.ApiKeyEnvironmentVariableName => SanitizeValue,
                FormRecognizerTestEnvironment.BlobContainerSasUrlEnvironmentVariableName => SanitizedSasUri,
                _ => base.SanitizeVariable(variableName, environmentVariableValue)
            };
        }
    }
```

## Matching

When tests are run in replay mode, HTTP method, Uri and headers are used to match the request to the recordings. Some headers change on every request and are not controlled by the client code and should be ignored during the matching. Common headers like `Date`, `x-ms-date`, `x-ms-client-request-id`, `User-Agent`, `Request-Id` are ignored by default but if more headers need to be ignored, use `RecordMatcher` extensions point.


``` C#
    public class ConfigurationRecordMatcher : RecordMatcher
    {
        public ConfigurationRecordMatcher(RecordedTestSanitizer sanitizer) : base(sanitizer)
        {
            ExcludeHeaders.Add("Sync-Token");
        }
    }

    public class ConfigurationLiveTests: RecordedTestBase
    {
        public ConfigurationLiveTests(bool isAsync) : base(isAsync)
        {
            Sanitizer = new ConfigurationRecordedTestSanitizer();
            Matcher = new ConfigurationRecordMatcher(Sanitizer);
        }
    }
```

## Misc

You can use `Recording.GenerateId()` to generate repeatable random IDs.

You should only use `Recording.Random` for random values (and you MUST make the same number of random calls in the same order every test run)

You can use `Recording.Now` and `Recording.UtcNow` if you need certain values to capture the time the test was recorded.

It's possible to add additional recording variables for advanced scenarios (like custom test configuration, etc.) by using `Recording.SetVariable` or `Recording.GetVariable`.

You can use `if (Mode == RecordingMode.Playback) { ... }` to change behavior for playback only scenarios (in particular to make polling times instantaneous)

You can use `using (Recording.DisableRecording()) { ... }` to disable recording in the code block (useful for polling methods)

# Support multi service version testing

To enable multi-version testing, add the `ClientTestFixture` attribute listing to all the service versions to the test class itself or a base class:

```C#
[ClientTestFixture(
    BlobClientOptions.ServiceVersion.V2019_02_02,
    BlobClientOptions.ServiceVersion.V2019_07_07)]
public abstract class BlobTestBase : StorageTestBase
{
    private readonly BlobClientOptions.ServiceVersion _serviceVersion;

    public BlobTestBase(bool async, BlobClientOptions.ServiceVersion serviceVersion, RecordedTestMode? mode = null)
        : base(async, mode)
    {
        _serviceVersion = serviceVersion;
    }

    // ...
}
```

Add a `ServiceVersion` parameter to the test class constructor and use the provided service version to create the `ClientOptions` instance.

```C#
public BlobClientOptions GetOptions() =>
    new BlobClientOptions(_serviceVersion) { /* ... */ };
```

To control what service versions a test will run against, use the `ServiceVersion` attribute by setting it's `Min` or `Max` properties (inclusive).

```C#
[Test]
[ServiceVersion(Min = BlobClientOptions.ServiceVersion.V2019_02_02)]
public async Task UploadOverwritesExistingBlob()
{
    // ...
}
```

How it looks it the test explorer:

![image](https://user-images.githubusercontent.com/1697911/72942831-52c7ca00-3d29-11ea-9b7e-2e54198d800d.png)

**Note:** If test recordings are enabled, the recordings will be generated against the latests version of the service.

## Support for an additional test parameter

The `ClientTestFixture` attribute also supports specifying an additional array of parameter values to send to the test class. 
Similar to the service versions, this results in the creation of a permutation of each test for each parameter value specified. 
Example usage is shown below:

```c#
// Add a new test suite parameter with no serviceVersions variants
[ClientTestFixture(
    serviceVersions: default,
    additionalParameters: new object[] { TableEndpointType.Storage, TableEndpointType.CosmosTable })]
public class TableServiceLiveTestsBase : RecordedTestBase<TablesTestEnvironment>
{
    protected readonly TableEndpointType _endpointType;

    public TableServiceLiveTestsBase(bool isAsync, TableEndpointType endpointType, RecordedTestMode recordedTestMode) 
        : base(isAsync, recordedTestMode)
    {
        _endpointType = endpointType;
    }
```

```c#
// Both serviceVersions variants and a new test suite parameter
[ClientTestFixture(
    serviceVersions: new object[] { TableClientOptions.ServiceVersion.V2019_02_02, TableClientOptions.ServiceVersion.V2019_07_07 },
    additionalParameters: new object[] { TableEndpointType.Storage, TableEndpointType.CosmosTable })]
public class TableServiceLiveTestsBase : RecordedTestBase<TablesTestEnvironment>
{
    protected readonly TableEndpointType _endpointType;
    TableClientOptions.ServiceVersion _serviceVersion

    public TableServiceLiveTestsBase(bool isAsync, TableClientOptions.ServiceVersion serviceVersion, TableEndpointType endpointType, RecordedTestMode recordedTestMode) 
        : base(isAsync, recordedTestMode)
    {
        _serviceVersion = serviceVersion;
        _endpointType = endpointType;
    }
```

**Note:** Additional parameter options work with test recordings and will create differentiated SessionRecords test class directory names for each additional parameter option. 
For example:

`/SessionRecords/TableClientLiveTests(CosmosTable)/CreatedCustomEntitiesCanBeQueriedWithFiltersAsync.json`
`/SessionRecords/TableClientLiveTests(Storage)/CreatedCustomEntitiesCanBeQueriedWithFiltersAsync.json`

## Management libraries
Testing of management libraries uses the Test Framework and should generally be very similar to tests that you write for data plane libraries. There is an intermediate test class that you will likely want to derive from that lives within the management code base - [ManagementRecordedTestBase](https://github.com/Azure/azure-sdk-for-net/blob/babee31b3151e4512ac5a77a55c426c136335fbb/common/ManagementTestShared/ManagementRecordedTestBase.cs). To see examples of Track 2 Management tests using the Test Framework, take a look at the [Storage tests](https://github.com/Azure/azure-sdk-for-net/tree/master/sdk/storage/Azure.ResourceManager.Storage/tests/Tests).