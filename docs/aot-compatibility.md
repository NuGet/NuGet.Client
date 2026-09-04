# AOT and Trimming Compatibility

NuGet enables `IsAotCompatible` for all NuGet.Core libraries.
Some NuGet readers and writers still use Newtonsoft.Json, which uses reflection and is not trimming compatible.

NuGet is migrating its JSON readers and selected writers to System.Text.Json.
During this migration, some Newtonsoft.Json and System.Text.Json implementations coexist behind a feature switch.
The switch selects available System.Text.Json readers in NuGet.Protocol, NuGet.Packaging, and NuGet.ProjectModel.
Readers without a System.Text.Json implementation continue to use Newtonsoft.Json.
The `global.json` and runtime graph readers, and the `packages.lock.json` file, stream, and string APIs use System.Text.Json directly.
The obsolete `packages.lock.json` `TextWriter` API continues to use Newtonsoft.Json for compatibility.

## Using NuGet in a Native AOT Application

If you consume NuGet libraries in a native AOT app, add the following feature switch to your project file:

```xml
<ItemGroup>
  <RuntimeHostConfigurationOption Include="NuGet.UseSystemTextJsonDeserialization"
                                  Value="true"
                                  Trim="true" />
</ItemGroup>
```

This option selects the AOT-compatible System.Text.Json readers that remain behind the feature switch.
The `Trim` value tells the linker that the switch value is constant.
The linker can then remove Newtonsoft.Json paths that the application does not use.
