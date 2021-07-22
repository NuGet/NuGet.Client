### How to select good environmental variable name?
When selecting for environmental variable name, please check Dotnet cli [convention examples](https://github.com/dotnet/sdk/blob/56a6239ba8959df4e7d851923e5022a0fce78805/src/Cli/dotnet/Program.cs#L158-L165).

Please note below is not ultimate guide, just our learning from last experience and hoping to help you and it can change depending on circumstance so feel free to improve it.

* Use capitalized snake case for the environment variable name (ex. CAPITALIZED_SNAKE_CASE). Because env var name is case sensitive on Linux.
* Separate words by underscore(`_`) to for readibility and clarity.
* Value doesn't have to case sensitive, we can convert to [uppercase](https://github.com/NuGet/NuGet.Client/blob/680f9bd4e97db7cd7482584276886764de69d3cb/src/NuGet.Core/NuGet.Packaging/PackageArchiveReader.cs#L530) and compare to validate value.
* Avoid using too generic or too short name which leads to ambiguity.

General format rule to follow for env var is: `PRODUCT_FEATURE_OPERATION` or `PRODUCT_FEATURE_PROPERTY`..

We selected in DOTNET_NUGET_SIGNATURE_VERIFICATION for 'Add an environment variable to opt-in to the package signing verification on .NET 5+'. Let's dissect part of env var name.

<b>DOTNET_NUGET_SIGNATURE_VERIFICATION</b>

* When selecting name first/second part is `PRODUCT` name (indicate where this env variable applies). In this case it applies to <b>DOTNET</b> and more specifically for <b>NUGET</b> use case specifically.

* Next part <b>SIGNATURE</b> what `FEATURE` (noun) it applies.

* Last part <b>VERIFICATION</b>  what `OPERATION` (verb) it enable or disable.

#### Good examples of env var names:

DOTNET_NUGET_SIGNATURE_VERIFICATION

DOTNET_CLI_TELEMETRY_OPTOUT

DOTNET_GENERATE_ASPNET_CERTIFICATE

DOTNET_NOLOGO

DOTNET_SKIP_FIRST_TIME_EXPERIENCE

DOTNET_SDK_TEST_AS_TOOL

#### Bad examples of env var names:

ENABLE: Too generic.

enable_sign_verify : Doesn't say where it applies.

DOTNETSDKTESTASTOOL: Single contiguous word, it need to be separated by '_' between words for better readability.

DOTNET.SDK.TEST.AS.TOOL: Better to use '_' instead of '.'.

dotnet_sdk_test_as_tool: All letters need to be uppercase.

dOTNET_SDK_TEST_AS_TOOL: All letters need to be uppercase.
