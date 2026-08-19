# AOT and Trimming Compatibility

NuGet enables `IsAotCompatible` for all NuGet.Core libraries.
Some NuGet readers and writers still use Newtonsoft.Json, which uses reflection and is not trimming compatible.

NuGet is migrating its JSON readers to System.Text.Json.
During this migration, the Newtonsoft.Json and System.Text.Json readers coexist behind a feature switch.
The switch selects available System.Text.Json readers in NuGet.Protocol, NuGet.Packaging, NuGet.ProjectModel, and the NuGet SDK resolver.
Readers without a System.Text.Json implementation continue to use Newtonsoft.Json.

## Using NuGet in a Native AOT Application

If you consume NuGet libraries in a native AOT app, add the following feature switch to your project file:

```xml
<ItemGroup>
  <RuntimeHostConfigurationOption Include="NuGet.UseSystemTextJsonDeserialization"
                                  Value="true"
                                  Trim="true" />
</ItemGroup>
```

This option selects the AOT-compatible System.Text.Json readers.
The `Trim` value tells the linker that the switch value is constant.
The linker can then remove Newtonsoft.Json read paths that the application does not use.
