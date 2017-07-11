// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;

namespace API.Test
{
    public class UwpProjectTestsUtil
    {
        private static readonly Version RS2 = new Version(10, 0, 15063, 0);
        // TODO: Handle creating the right project template for TPV > RS2
        // Returns the path to the solution file
        public static string CreateUwpClassLibrary(string projectName,
            string outputPath,
            string targetPlatformVersion = "10.0.14393.0",
            string targetPlatformMinVersion = "10.0.10586.0")
        {
            if(string.IsNullOrEmpty(targetPlatformVersion))
            {
                targetPlatformVersion = "10.0.14393.0";
            }

            if (string.IsNullOrEmpty(targetPlatformMinVersion))
            {
                targetPlatformMinVersion = "10.0.10586.0";
            }

            var tpv = new Version(targetPlatformVersion);
            Directory.CreateDirectory(Path.Combine(outputPath, projectName, projectName));
            var solutionDir = Path.Combine(outputPath, projectName);

            FormatAndCopyTemplateFiles("project.sln", projectName, solutionDir, targetPlatformVersion, targetPlatformMinVersion, projectName + ".sln");
            FormatAndCopyTemplateFiles("project.rd.xml", projectName, solutionDir, targetPlatformVersion, targetPlatformMinVersion, projectName + ".rd.xml", "Properties");
            FormatAndCopyTemplateFiles("Class1.cs", projectName, solutionDir, targetPlatformVersion, targetPlatformMinVersion, "Class1.cs");
            FormatAndCopyTemplateFiles("AssemblyInfo.cs", projectName, solutionDir, targetPlatformVersion, targetPlatformMinVersion, "AssemblyInfo.cs", "Properties");
            if (tpv >= RS2)
            {
                FormatAndCopyTemplateFiles("packagerefbaseduwpproject.csproj", projectName, solutionDir, targetPlatformVersion, targetPlatformMinVersion, projectName + ".csproj");
            }
            else
            {                
                FormatAndCopyTemplateFiles("project.json", projectName, solutionDir, targetPlatformVersion, targetPlatformMinVersion, "project.json");
                FormatAndCopyTemplateFiles("projectjsonbaseduwpproject.csproj", projectName, solutionDir, targetPlatformVersion, targetPlatformMinVersion, projectName + ".csproj");
                
                
            }

            
            
            return Path.Combine(solutionDir, projectName + ".sln");
        }

        private static void FormatAndCopyTemplateFiles(string fileName,
            string projectName,
            string solutionDir,
            string targetPlatformVersion,
            string targetPlatformMinVersion,
            string destFileName,
            string destinationDir = "")
        {
            var contents = GetResourceAsString($"API.Test.compiler.resources.UWP.{fileName}");
            contents = string.Format(contents, projectName, targetPlatformVersion, targetPlatformMinVersion);

            var finalDestinationDir = Path.Combine(solutionDir, projectName, destinationDir);
            if (Path.GetExtension(destFileName) == ".sln")
            {
                finalDestinationDir = solutionDir;
            }
            if (!Directory.Exists(finalDestinationDir))
            {
                Directory.CreateDirectory(finalDestinationDir);
            }
            File.WriteAllText(Path.Combine(finalDestinationDir, destFileName), contents);
        }
        public static string GetResourceAsString(string name)
        {
            var stream = typeof(UwpProjectTestsUtil).GetTypeInfo().Assembly.GetManifestResourceStream(name);
            return new StreamReader(stream).ReadToEnd();
        }
    }
}
