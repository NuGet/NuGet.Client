// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Globalization;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Common;
using NuGet.Packaging.Signing;

namespace NuGet.CommandLine.XPlat
{
    internal static class AddPackageReferenceCommand
    {
        internal static Func<IPackageReferenceCommandRunner> GetCommandRunner;

        internal static CliCommand Register(CliCommand app, Func<ILogger> getLogger, Func<IPackageReferenceCommandRunner> getCommandRunner)
        {
            var AddPackageCmd = new CliCommand(name: "add", description: Strings.Delete_Description);

            // Options directly under the verb 'add'

            // Options under sub-command: add
            RegisterOptionsForCommandAdd(AddPackageCmd, getLogger);
            GetCommandRunner = getCommandRunner;

            app.Subcommands.Add(AddPackageCmd);

            return AddPackageCmd;
        }

        private static void RegisterOptionsForCommandAdd(CliCommand cmd, Func<ILogger> getLogger)
        {
            var package_Option = new CliOption<string>(name: "--package")
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.AddPkg_PackageIdDescription,
            };
            cmd.Add(package_Option);
            var version_Option = new CliOption<string>(name: "--version")
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.AddPkg_PackageVersionDescription,
            };
            cmd.Add(version_Option);
            var dgFilePath_Option = new CliOption<string>(name: "--dg-file", aliases: new[] { "-d" })
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.AddPkg_DgFileDescription,
            };
            cmd.Add(dgFilePath_Option);
            var projectPath_Option = new CliOption<string>(name: "--project", aliases: new[] { "-p" })
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.AddPkg_ProjectPathDescription,
            };
            cmd.Add(projectPath_Option);
            var framework_Option = new CliOption<string[]>(name: "--framework", aliases: new[] { "-f" })
            {
                Arity = ArgumentArity.ZeroOrMore,
                Description = Strings.AddPkg_FrameworksDescription,
            };
            cmd.Add(framework_Option);
            var noRestore_Option = new CliOption<bool>(name: "--no-restore", aliases: new[] { "-n" })
            {
                Arity = ArgumentArity.Zero,
                Description = Strings.AddPkg_NoRestoreDescription,
            };
            cmd.Add(noRestore_Option);
            var source_Option = new CliOption<string[]>(name: "--source", aliases: new[] { "-s" })
            {
                Arity = ArgumentArity.ZeroOrMore,
                Description = Strings.AddPkg_SourcesDescription,
            };
            cmd.Add(source_Option);
            var packageDirectory_Option = new CliOption<string>(name: "--package-directory")
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.AddPkg_PackageDirectoryDescription,
            };
            cmd.Add(packageDirectory_Option);
            var interactive_Option = new CliOption<bool>(name: "--interactive")
            {
                Arity = ArgumentArity.Zero,
                Description = Strings.NuGetXplatCommand_Interactive,
            };
            cmd.Add(interactive_Option);
            var prerelease_Option = new CliOption<bool>(name: "--interactive")
            {
                Arity = ArgumentArity.Zero,
                Description = Strings.AddPkg_PackagePrerelease,
            };
            cmd.Add(prerelease_Option);
            var forceEnglishOutput_Option = new CliOption<bool>(name: CommandConstants.ForceEnglishOutputOption)
            {
                Arity = ArgumentArity.Zero,
                Description = Strings.ForceEnglishOutput_Description,
            };
            cmd.Add(forceEnglishOutput_Option);
            // Create handler delegate handler for cmd
            cmd.SetAction((parseResult) =>
            {
                string id = parseResult.GetValue(package_Option);
                string version = parseResult.GetValue(version_Option);
                string dgFilePath = parseResult.GetValue(dgFilePath_Option);
                string projectPath = parseResult.GetValue(projectPath_Option);
                string[] frameworks = parseResult.GetValue(framework_Option);
                bool noRestore = parseResult.GetValue(noRestore_Option);
                string[] sources = parseResult.GetValue(source_Option);
                string packageDirectory = parseResult.GetValue(packageDirectory_Option);
                bool interactive = parseResult.GetValue(interactive_Option);
                bool prerelease = parseResult.GetValue(prerelease_Option);

                ValidateArgument(id, cmd.Name);
                ValidateArgument(projectPath, cmd.Name);
                ValidateProjectPath(projectPath, cmd.Name);
                if (!noRestore)
                {
                    ValidateArgument(dgFilePath, cmd.Name);
                }
                var logger = getLogger();
                var noVersion = !(version is not null);
                var packageVersion = version is not null ? version : null;
                ValidatePrerelease(prerelease, noVersion, cmd.Name);
                var packageRefArgs = new PackageReferenceArgs(projectPath, logger)
                {
                    Frameworks = CommandLineUtility.SplitAndJoinAcrossMultipleValues(frameworks),
                    Sources = CommandLineUtility.SplitAndJoinAcrossMultipleValues(sources),
                    PackageDirectory = packageDirectory,
                    NoRestore = noRestore,
                    NoVersion = noVersion,
                    DgFilePath = dgFilePath,
                    Interactive = interactive,
                    Prerelease = prerelease,
                    PackageVersion = packageVersion,
                    PackageId = id
                };
                var msBuild = new MSBuildAPIUtility(logger);

                X509TrustStore.InitializeForDotNetSdk(logger);

                var addPackageRefCommandRunner = GetCommandRunner();
                return addPackageRefCommandRunner.ExecuteCommand(packageRefArgs, msBuild).Result;
            });
        }

        public static void Register(CommandLineApplication app, Func<ILogger> getLogger,
            Func<IPackageReferenceCommandRunner> getCommandRunner)
        {
            app.Command("add", addpkg =>
            {
                addpkg.Description = Strings.AddPkg_Description;
                addpkg.HelpOption(XPlatUtility.HelpOption);

                addpkg.Option(
                    CommandConstants.ForceEnglishOutputOption,
                    Strings.ForceEnglishOutput_Description,
                    CommandOptionType.NoValue);

                var id = addpkg.Option(
                    "--package",
                    Strings.AddPkg_PackageIdDescription,
                    CommandOptionType.SingleValue);

                var version = addpkg.Option(
                    "--version",
                    Strings.AddPkg_PackageVersionDescription,
                    CommandOptionType.SingleValue);

                var dgFilePath = addpkg.Option(
                    "-d|--dg-file",
                    Strings.AddPkg_DgFileDescription,
                    CommandOptionType.SingleValue);

                var projectPath = addpkg.Option(
                    "-p|--project",
                    Strings.AddPkg_ProjectPathDescription,
                    CommandOptionType.SingleValue);

                var frameworks = addpkg.Option(
                    "-f|--framework",
                    Strings.AddPkg_FrameworksDescription,
                    CommandOptionType.MultipleValue);

                var noRestore = addpkg.Option(
                    "-n|--no-restore",
                    Strings.AddPkg_NoRestoreDescription,
                    CommandOptionType.NoValue);

                var sources = addpkg.Option(
                    "-s|--source",
                    Strings.AddPkg_SourcesDescription,
                    CommandOptionType.MultipleValue);

                var packageDirectory = addpkg.Option(
                    "--package-directory",
                    Strings.AddPkg_PackageDirectoryDescription,
                    CommandOptionType.SingleValue);

                var interactive = addpkg.Option(
                    "--interactive",
                    Strings.AddPkg_InteractiveDescription,
                    CommandOptionType.NoValue);

                var prerelease = addpkg.Option(
                    "--prerelease",
                    Strings.AddPkg_PackagePrerelease,
                    CommandOptionType.NoValue);

                addpkg.OnExecute(() =>
                {
                    ValidateArgument(id, addpkg.Name);
                    ValidateArgument(projectPath, addpkg.Name);
                    ValidateProjectPath(projectPath, addpkg.Name);
                    if (!noRestore.HasValue())
                    {
                        ValidateArgument(dgFilePath, addpkg.Name);
                    }
                    var logger = getLogger();
                    var noVersion = !version.HasValue();
                    var packageVersion = version.HasValue() ? version.Value() : null;
                    ValidatePrerelease(prerelease.HasValue(), noVersion, addpkg.Name);
                    var packageRefArgs = new PackageReferenceArgs(projectPath.Value(), logger)
                    {
                        Frameworks = CommandLineUtility.SplitAndJoinAcrossMultipleValues(frameworks.Values),
                        Sources = CommandLineUtility.SplitAndJoinAcrossMultipleValues(sources.Values),
                        PackageDirectory = packageDirectory.Value(),
                        NoRestore = noRestore.HasValue(),
                        NoVersion = noVersion,
                        DgFilePath = dgFilePath.Value(),
                        Interactive = interactive.HasValue(),
                        Prerelease = prerelease.HasValue(),
                        PackageVersion = packageVersion,
                        PackageId = id.Values[0]
                    };
                    var msBuild = new MSBuildAPIUtility(logger);

                    X509TrustStore.InitializeForDotNetSdk(logger);

                    var addPackageRefCommandRunner = getCommandRunner();
                    return addPackageRefCommandRunner.ExecuteCommand(packageRefArgs, msBuild);
                });
            });
        }

        private static void ValidatePrerelease(bool prerelease, bool noVersion, string commandName)
        {
            if (prerelease && !noVersion)
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_PrereleaseWhenVersionSpecified,
                    commandName));
            }
        }

        private static void ValidateArgument(CommandOption arg, string commandName)
        {
            if (arg.Values.Count < 1)
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_PkgMissingArgument,
                    commandName,
                    arg.Template));
            }
        }

        private static void ValidateProjectPath(CommandOption projectPath, string commandName)
        {
            if (!File.Exists(projectPath.Value()) || !projectPath.Value().EndsWith("proj", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                    Strings.Error_PkgMissingOrInvalidProjectFile,
                    commandName,
                    projectPath.Value()));
            }
        }

        private static void ValidateArgument(string arg, string commandName)
        {
            if (!string.IsNullOrEmpty(arg))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_PkgMissingArgument,
                    commandName,
                    "--package"));
            }
        }

        private static void ValidateProjectPath(string projectPath, string commandName)
        {
            if (!string.IsNullOrEmpty(projectPath) && (!File.Exists(projectPath) || !projectPath.EndsWith("proj", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                    Strings.Error_PkgMissingOrInvalidProjectFile,
                    commandName,
                    projectPath));
            }
        }
    }
}
