// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using NuGet.RuntimeModel;

if (!AppContext.TryGetSwitch("NuGet.UseSystemTextJsonDeserialization", out bool isEnabled) || !isEnabled)
{
    Console.Error.WriteLine("NuGet.UseSystemTextJsonDeserialization is not enabled.");
    return 1;
}

const string runtimeGraphJson = """
    {
      "runtimes": {
        "win-x64": {
          "#import": [ "win" ]
        }
      },
      "supports": {
        "desktop": {
          "net10.0": "win-x64"
        }
      }
    }
    """;

using var stream = new MemoryStream(Encoding.UTF8.GetBytes(runtimeGraphJson));
RuntimeGraph graph = JsonRuntimeFormat.ReadRuntimeGraph(stream);

if (!graph.Runtimes.TryGetValue("win-x64", out RuntimeDescription? runtime)
    || runtime.InheritedRuntimes.Count != 1
    || runtime.InheritedRuntimes[0] != "win")
{
    Console.Error.WriteLine("Runtime graph did not contain the expected win-x64 inheritance.");
    return 1;
}

if (!graph.Supports.TryGetValue("desktop", out CompatibilityProfile? profile)
    || profile.RestoreContexts.Count != 1
    || profile.RestoreContexts[0].RuntimeIdentifier != "win-x64")
{
    Console.Error.WriteLine("Runtime graph did not contain the expected desktop compatibility profile.");
    return 1;
}

Console.WriteLine("Passed.");
return 0;
