// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Versioning;

if (!AppContext.TryGetSwitch("NuGet.UseSystemTextJsonDeserialization", out bool isEnabled) || !isEnabled)
{
    return 1;
}

if (RunAssetsLockWriterSmoke() != 0)
{
    return 2;
}

Console.WriteLine("Passed.");
return 0;

static int RunAssetsLockWriterSmoke()
{
    var lockFile = new LockFile
    {
        Version = LockFileFormat.Version,
        PackageSpec = new PackageSpec(new[]
        {
            new TargetFrameworkInformation
            {
                FrameworkName = NuGetFramework.Parse("net10.0"),
                TargetAlias = "net10.0"
            }
        })
    };
    var targetLibrary = new LockFileTargetLibrary
    {
        Name = "Package.Direct",
        Version = NuGetVersion.Parse("1.2.3"),
        Type = "package"
    };
    targetLibrary.Dependencies.Add(new PackageDependency(
        "Package.Transitive",
        VersionRange.Parse("[2.0.0, )")));
    targetLibrary.CompileTimeAssemblies.Add(new LockFileItem("lib/net10.0/Package.Direct.dll"));
    lockFile.Targets.Add(new LockFileTarget
    {
        TargetFramework = NuGetFramework.Parse("net10.0"),
        TargetAlias = "net10.0",
        Libraries = new[] { targetLibrary }
    });
    lockFile.Libraries.Add(new LockFileLibrary
    {
        Name = "Package.Direct",
        Version = NuGetVersion.Parse("1.2.3"),
        Type = "package",
        Sha512 = "caf\u00e9<&",
        Files = new[] { "lib/net10.0/Package.Direct.dll" }
    });
    lockFile.ProjectFileDependencyGroups.Add(
        new ProjectFileDependencyGroup("net10.0", new[] { "Package.Direct >= 1.2.3" }));

    var format = new LockFileFormat();
    var stream = new MemoryStream();
    format.Write(stream, lockFile);
    string streamOutput = Encoding.UTF8.GetString(stream.ToArray());

    string outputDirectory = Path.Combine(
        Path.GetTempPath(),
        $"NuGet.AotCompatibility.{Guid.NewGuid():N}");
    string outputPath = Path.Combine(outputDirectory, LockFileFormat.AssetsFileName);
    string fileOutput;
    try
    {
        format.Write(outputPath, lockFile);
        fileOutput = File.ReadAllText(outputPath);
    }
    finally
    {
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    if (!IsValidAssetsLockOutput(streamOutput)
        || !IsValidAssetsLockOutput(fileOutput)
        || stream.CanWrite)
    {
        Console.Error.WriteLine("Assets lock file writer output did not match.");
        return 1;
    }

    return 0;
}

static bool IsValidAssetsLockOutput(string output)
{
    using JsonDocument document = JsonDocument.Parse(output);
    JsonElement root = document.RootElement;

    return root.GetProperty("version").GetInt32() == LockFileFormat.Version
        && root.GetProperty("targets").TryGetProperty(
            "net10.0",
            out JsonElement target)
        && target.TryGetProperty("Package.Direct/1.2.3", out _)
        && root.GetProperty("libraries")
            .GetProperty("Package.Direct/1.2.3")
            .GetProperty("sha512")
            .GetString() == "caf\u00e9<&"
        && root.TryGetProperty("project", out _);
}
