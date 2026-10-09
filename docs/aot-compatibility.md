# AOT and Trimming Compatibility

NuGet has `IsAotCompatible` enabled for all NuGet.Core libraries and the code itself is AOT compatible.
NuGet uses System.Text.Json source-generated deserialization by default.
Newtonsoft.Json remains available as an opt-out compatibility path, but it uses reflection and is not AOT/trim compatible.

Both deserialization paths coexist, gated under a feature switch.
The feature switch defaults to System.Text.Json, and setting it explicitly allows the linker to trim the Newtonsoft.Json code path entirely.

## Using NuGet in a Native AOT Application

System.Text.Json is used by default. If you consume NuGet libraries in a native AOT app, add the following feature switch to your project file so the linker can treat the value as constant:

```xml
<ItemGroup>
  <RuntimeHostConfigurationOption Include="NuGet.UseSystemTextJsonDeserialization"
                                  Value="true"
                                  Trim="true" />
</ItemGroup>
```

This tells the linker that NuGet always uses the AOT-safe System.Text.Json path so it can eliminate the Newtonsoft.Json code path from the binary.

To opt out and use Newtonsoft.Json, set `NuGet.UseSystemTextJsonDeserialization` or the `NUGET_USE_SYSTEM_TEXT_JSON_DESERIALIZATION` environment variable to `false`.
