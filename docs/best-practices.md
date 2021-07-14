### How to select good environmental variable name?
When selecting for environmental variable name, please check Dotnet cli convention examples.
Please check discussion [here](https://github.com/NuGet/Client.Engineering/issues/882#issuecomment-816118683) for more details, please note this's not ultimate guide just our learning from last experience and hoping to help you, feel free to improve it. 
Since env var name is case sensitive in Linux, so please use all uppercase (separated by underscore) naming convention for best practice.  But value doesn't have to case sensitive, we can convert to [uppercase](https://github.com/NuGet/NuGet.Client/blob/680f9bd4e97db7cd7482584276886764de69d3cb/src/NuGet.Core/NuGet.Packaging/PackageArchiveReader.cs#L530) and compare to validate value.
We selected in DOTNET_NUGET_SIGNATURE_VERIFICATION for 'Add an environment variable to opt-in to the package signing verification on .NET 5+'. Let's dissect part of env var name.

<b>DOTNET_NUGET_SIGNATURE_VERIFICATION</b>

When selecting name first/second parts indicate where this env variable applies. In this case it applies to <b>DOTNET</b> and more specifically for <b>NUGET</b> use cases. 

Next part <b>SIGNATURE</b> what feature (noun) it applies.

Last part <b>VERIFICATION</b>  what operation (verb) it enable or disable.

Please note above is not hard rule, it can change depending on circumstance.

#### Good examples of env var names:

DOTNET_NUGET_SIGNATURE_VERIFICATION

DOTNET_CLI_TELEMETRY_OPTOUT

DOTNET_GENERATE_ASPNET_CERTIFICATE

DOTNET_NOLOGO

DOTNET_SKIP_FIRST_TIME_EXPERIENCE

DOTNET_SDK_TEST_AS_TOOL

#### Bad examples of env var names:

enable_sign_verify : Doesn't say where it applies.

DOTNETSDKTESTASTOOL: Single contiguous word, it need to be separated by '_' between words for better readability.

DOTNET.SDK.TEST.AS.TOOL: Better to use '_' instead of '.'.

dotnet_sdk_test_as_tool: All letters need to be uppercase.

dOTNET_SDK_TEST_AS_TOOL: All letters need to be uppercase.