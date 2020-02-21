    

---
title: dotnet nuget add source command
description: The `dotnet nuget add source` command will add a new source to your NuGet configuration files. This will enable finding packages at this additional source. 
author: nugetClient
ms.date: 2/21/2020
---
# dotnet nuget add source

**This article applies to:** ✔️ .NET Core 3.1.200 SDK and later versions

## Name

`dotnet nuget add source` - Add a NuGet source.

## Synopsis

```dotnetcli
`dotnet nuget add source PACKAGESOURCEPATH [--name] [--username] [--password] [--store-password-in-clear-text] [--valid-authentication-types] [--configfile]`
`dotnet nuget add source [-h|--help]`
```

## Description

The `dotnet nuget add source` command will add a new source to your NuGet configuration files. This will enable finding packages at this additional source. 

## Arguments
- **`PACKAGESOURCEPATH`**

  Path to the package(s) source.

## Options
- **`-n|--name`**

  Name of the source.

- **`-u|--username`**

  UserName to be used when connecting to an authenticated source.

- **`-p|--password`**

  Password to be used when connecting to an authenticated source.

- **`--store-Password-In-Clear-Text`**

  Enables storing portable package source credentials by disabling password encryption.

- **`--valid-Authentication-Types`**

  Comma-separated list of valid authentication types for this source. By default, all authentication types are valid. Example: basic,negotiate

- **`--configfile`**

  The NuGet configuration file. If specified, only the settings from this file will be used. If not specified, the hierarchy of configuration files from the current directory will be used. To learn more about NuGet configuration go to https://docs.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior.

## Examples

- Add `nuget.org` as a source:

  ```dotnetcli
  dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org
  ```

- Add `c:\packages` as a local source:

  ```dotnetcli
  dotnet nuget add source c:\packages
  ```

- Add a source that needs authentication:

  ```dotnetcli
  dotnet nuget add source https://someServer/myTeam -n myTeam -u myUserName -p myPassword --store-password-in-clear-text
  ```

- Add a source that needs authentication (then go install credential provider):

  ```dotnetcli
  dotnet nuget add source https://azureartifacts.microsoft.com/myTeam -n myTeam
  ```
## See also

- [Package source sections in NuGet.config files](/nuget/reference/nuget-config-file#package-source-sections)


- [sources command (nuget.exe)](/nuget/reference/cli-reference/cli-ref-sources)

    

---
title: dotnet nuget disable source command
description: The `dotnet nuget disable source` command will disable an existing source in your NuGet configuration files. 
author: nugetClient
ms.date: 2/21/2020
---
# dotnet nuget disable source

**This article applies to:** ✔️ .NET Core 3.1.200 SDK and later versions

## Name

`dotnet nuget disable source` - Disable a NuGet source.

## Synopsis

```dotnetcli
`dotnet nuget disable source NAME [--configfile]`
`dotnet nuget disable source [-h|--help]`
```

## Description

The `dotnet nuget disable source` command will disable an existing source in your NuGet configuration files. 

## Arguments
- **`NAME`**

  Name of the source.

## Options
- **`--configfile`**

  The NuGet configuration file. If specified, only the settings from this file will be used. If not specified, the hierarchy of configuration files from the current directory will be used. To learn more about NuGet configuration go to https://docs.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior.

## Examples

- Disable a source with name of `mySource`:

  ```dotnetcli
  dotnet nuget disable source mySource
  ```
## See also

- [Package source sections in NuGet.config files](/nuget/reference/nuget-config-file#package-source-sections)


- [sources command (nuget.exe)](/nuget/reference/cli-reference/cli-ref-sources)

    

---
title: dotnet nuget enable source command
description: The `dotnet nuget enable source` command will enable an existing source in your NuGet configuration files. 
author: nugetClient
ms.date: 2/21/2020
---
# dotnet nuget enable source

**This article applies to:** ✔️ .NET Core 3.1.200 SDK and later versions

## Name

`dotnet nuget enable source` - Enable a NuGet source.

## Synopsis

```dotnetcli
`dotnet nuget enable source NAME [--configfile]`
`dotnet nuget enable source [-h|--help]`
```

## Description

The `dotnet nuget enable source` command will enable an existing source in your NuGet configuration files. 

## Arguments
- **`NAME`**

  Name of the source.

## Options
- **`--configfile`**

  The NuGet configuration file. If specified, only the settings from this file will be used. If not specified, the hierarchy of configuration files from the current directory will be used. To learn more about NuGet configuration go to https://docs.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior.

## Examples

- Enable a source with name of `mySource`:

  ```dotnetcli
  dotnet nuget enable source mySource
  ```
## See also

- [Package source sections in NuGet.config files](/nuget/reference/nuget-config-file#package-source-sections)


- [sources command (nuget.exe)](/nuget/reference/cli-reference/cli-ref-sources)

    

---
title: dotnet nuget list source command
description: The `dotnet nuget list source` command will list all existing sources from your NuGet configuration files. 
author: nugetClient
ms.date: 2/21/2020
---
# dotnet nuget list source

**This article applies to:** ✔️ .NET Core 3.1.200 SDK and later versions

## Name

`dotnet nuget list source` - Lists all configured NuGet sources.

## Synopsis

```dotnetcli
`dotnet nuget list source [--format] [--configfile]`
`dotnet nuget list source [-h|--help]`
```

## Description

The `dotnet nuget list source` command will list all existing sources from your NuGet configuration files. 

## Options
- **`--format`**

  Applies to the list action. Accepts two values: Detailed (the default) and Short.

- **`--configfile`**

  The NuGet configuration file. If specified, only the settings from this file will be used. If not specified, the hierarchy of configuration files from the current directory will be used. To learn more about NuGet configuration go to https://docs.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior.

## Examples

- List configured sources from the current directory:

  ```dotnetcli
  dotnet nuget list source
  ```
## See also

- [Package source sections in NuGet.config files](/nuget/reference/nuget-config-file#package-source-sections)


- [sources command (nuget.exe)](/nuget/reference/cli-reference/cli-ref-sources)

    

---
title: dotnet nuget remove source command
description: The `dotnet nuget remove source` command will remove an existing source from your NuGet configuration files. 
author: nugetClient
ms.date: 2/21/2020
---
# dotnet nuget remove source

**This article applies to:** ✔️ .NET Core 3.1.200 SDK and later versions

## Name

`dotnet nuget remove source` - Remove a NuGet source.

## Synopsis

```dotnetcli
`dotnet nuget remove source NAME [--configfile]`
`dotnet nuget remove source [-h|--help]`
```

## Description

The `dotnet nuget remove source` command will remove an existing source from your NuGet configuration files. 

## Arguments
- **`NAME`**

  Name of the source.

## Options
- **`--configfile`**

  The NuGet configuration file. If specified, only the settings from this file will be used. If not specified, the hierarchy of configuration files from the current directory will be used. To learn more about NuGet configuration go to https://docs.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior.

## Examples

- Remove a source with name of `mySource`:

  ```dotnetcli
  dotnet nuget remove source mySource
  ```
## See also

- [Package source sections in NuGet.config files](/nuget/reference/nuget-config-file#package-source-sections)


- [sources command (nuget.exe)](/nuget/reference/cli-reference/cli-ref-sources)

    

---
title: dotnet nuget update source command
description: The `dotnet nuget update source` command will update an existing source in your NuGet configuration files. 
author: nugetClient
ms.date: 2/21/2020
---
# dotnet nuget update source

**This article applies to:** ✔️ .NET Core 3.1.200 SDK and later versions

## Name

`dotnet nuget update source` - Update a NuGet source.

## Synopsis

```dotnetcli
`dotnet nuget update source NAME [--source] [--username] [--password] [--store-password-in-clear-text] [--valid-authentication-types] [--configfile]`
`dotnet nuget update source [-h|--help]`
```

## Description

The `dotnet nuget update source` command will update an existing source in your NuGet configuration files. 

## Arguments
- **`NAME`**

  Name of the source.

## Options
- **`-s|--source`**

  Path to the package(s) source.

- **`-u|--username`**

  UserName to be used when connecting to an authenticated source.

- **`-p|--password`**

  Password to be used when connecting to an authenticated source.

- **`--store-Password-In-Clear-Text`**

  Enables storing portable package source credentials by disabling password encryption.

- **`--valid-Authentication-Types`**

  Comma-separated list of valid authentication types for this source. By default, all authentication types are valid. Example: basic,negotiate

- **`--configfile`**

  The NuGet configuration file. If specified, only the settings from this file will be used. If not specified, the hierarchy of configuration files from the current directory will be used. To learn more about NuGet configuration go to https://docs.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior.

## Examples

- Enable a source with name of `mySource`:

  ```dotnetcli
  dotnet nuget remove source mySource
  ```
## See also

- [Package source sections in NuGet.config files](/nuget/reference/nuget-config-file#package-source-sections)


- [sources command (nuget.exe)](/nuget/reference/cli-reference/cli-ref-sources)

}
