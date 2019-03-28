// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

extern alias CoreV2;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.Win32;
using NuGet.Common;
using NuGet.PackageManagement;

namespace NuGet.CommandLine
{
    public class Program
    {
        private const string Utf8Option = "-utf8";
        private const string ForceEnglishOutputOption = "-forceEnglishOutput";
        private const string DebugOption = "--debug";
        private const string OSVersionRegistryKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
        private const string FilesystemRegistryKey = @"SYSTEM\CurrentControlSet\Control\FileSystem";
        private const string DotNetSetupRegistryKey = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\";
        private const int Net462ReleasedVersion = 394802;


        private static readonly string ThisExecutableName = typeof(Program).Assembly.GetName().Name;

        [Import]
        public HelpCommand HelpCommand { get; set; }

        [ImportMany]
        public IEnumerable<ICommand> Commands { get; set; }

        [Import]
        public ICommandManager Manager { get; set; }

        /// <summary>
        /// Flag meant for unit tests that prevents command line extensions from being loaded.
        /// </summary>
        public static bool IgnoreExtensions { get; set; }

        public static int Main(string[] args)
        {
            AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false);
            AppContext.SetSwitch("Switch.System.IO.BlockLongPaths", false);

#if DEBUG
            if (args.Contains(DebugOption, StringComparer.OrdinalIgnoreCase))
            {
                args = args.Where(arg => !string.Equals(arg, DebugOption, StringComparison.OrdinalIgnoreCase)).ToArray();
                System.Diagnostics.Debugger.Launch();
            }
#endif

#if IS_DESKTOP
            // Find any response files and resolve the args
            if (!RuntimeEnvironmentHelper.IsMono)
            {
                args = CommandLineResponseFile.ParseArgsResponseFiles(args);
            }
#endif
            return MainCore(Directory.GetCurrentDirectory(), args);
        }

        public static int MainCore(string workingDirectory, string[] args)
        {
            // First, optionally disable localization in resources.
            if (args.Any(arg => string.Equals(arg, ForceEnglishOutputOption, StringComparison.OrdinalIgnoreCase)))
            {
                CultureUtility.DisableLocalization();
            }

            // set output encoding to UTF8 if -utf8 is specified
            var oldOutputEncoding = System.Console.OutputEncoding;
            if (args.Any(arg => string.Equals(arg, Utf8Option, StringComparison.OrdinalIgnoreCase)))
            {
                args = args.Where(arg => !string.Equals(arg, Utf8Option, StringComparison.OrdinalIgnoreCase)).ToArray();
                SetConsoleOutputEncoding(Encoding.UTF8);
            }

            // Increase the maximum number of connections per server.
            if (!RuntimeEnvironmentHelper.IsMono)
            {
                ServicePointManager.DefaultConnectionLimit = 64;
            }
            else
            {
                // Keep mono limited to a single download to avoid issues.
                ServicePointManager.DefaultConnectionLimit = 1;
            }

            NetworkProtocolUtility.ConfigureSupportedSslProtocols();

            var console = new Console();
            var fileSystem = new CoreV2.NuGet.PhysicalFileSystem(workingDirectory);
            var logStackAsError = console.Verbosity == Verbosity.Detailed;

            try
            {
                // Remove NuGet.exe.old
                RemoveOldFile(fileSystem);

                // Import Dependencies
                var p = new Program();
                p.Initialize(fileSystem, console);

                // Add commands to the manager
                foreach (var cmd in p.Commands)
                {
                    p.Manager.RegisterCommand(cmd);
                }

                var parser = new CommandLineParser(p.Manager);

                // Parse the command
                var command = parser.ParseCommandLine(args) ?? p.HelpCommand;
                command.CurrentDirectory = workingDirectory;
                
                // Fallback on the help command if we failed to parse a valid command
                if (!ArgumentCountValid(command))
                {
                    // Get the command name and add it to the argument list of the help command
                    var commandName = command.CommandAttribute.CommandName;

                    // Print invalid arguments command error message in stderr
                    console.WriteError(LocalizedResourceManager.GetString("InvalidArguments"), commandName);

                    // then show help
                    p.HelpCommand.ViewHelpForCommand(commandName);

                    return 1;
                }
                else
                {
                    SetConsoleInteractivity(console, command as Command);

                    try
                    {
                        command.Execute();
                    }
                    catch (AggregateException e)
                    {
                        var unwrappedEx = ExceptionUtility.Unwrap(e);

                        if (unwrappedEx is CommandLineArgumentCombinationException)
                        {
                            var commandName = command.CommandAttribute.CommandName;

                            console.WriteLine($"{string.Format(CultureInfo.CurrentCulture, LocalizedResourceManager.GetString("InvalidArguments"), commandName)} {unwrappedEx.Message}");

                            p.HelpCommand.ViewHelpForCommand(commandName);

                            return 1;
                        }
                        else
                        {
                            throw;
                        }
                    }
                    catch (CommandLineArgumentCombinationException e)
                    {
                        var commandName = command.CommandAttribute.CommandName;

                        console.WriteLine($"{string.Format(CultureInfo.CurrentCulture, LocalizedResourceManager.GetString("InvalidArguments"), commandName)} {e.Message}");

                        p.HelpCommand.ViewHelpForCommand(commandName);

                        return 1;
                    }
                }
            }
            catch (AggregateException exception)
            {
                var unwrappedEx = ExceptionUtility.Unwrap(exception);
                var rootException = ExceptionUtility.GetRootException(exception);

                if (unwrappedEx is ExitCodeException)
                {
                    // Return the exit code without writing out the exception type
                    var exitCodeEx = unwrappedEx as ExitCodeException;
                    return exitCodeEx.ExitCode;
                }
                if (rootException is PathTooLongException)
                {
                    LogHelperMessageForPathTooLongException(console);
                }

                // Log the exception and stack trace.
                ExceptionUtilities.LogException(unwrappedEx, console, logStackAsError);
                return 1;
            }
            catch (ExitCodeException e)
            {
                return e.ExitCode;
            }
            catch (PathTooLongException e)
            {
                // Log the exception and stack trace.
                ExceptionUtilities.LogException(e, console, logStackAsError);
                LogHelperMessageForPathTooLongException(console);
                return 1;
            }
            catch (Exception exception)
            {
                ExceptionUtilities.LogException(exception, console, logStackAsError);
                return 1;
            }
            finally
            {
                CoreV2.NuGet.OptimizedZipPackage.PurgeCache();
                SetConsoleOutputEncoding(oldOutputEncoding);
            }

            return 0;
        }

        private void Initialize(CoreV2.NuGet.IFileSystem fileSystem, IConsole console)
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            using (var catalog = new AggregateCatalog(new AssemblyCatalog(GetType().Assembly)))
            {
                if (!IgnoreExtensions)
                {
                    AddExtensionsToCatalog(catalog, console);
                }

                try
                {
                    using (var container = new CompositionContainer(catalog))
                    {
                        container.ComposeExportedValue(console);
                        container.ComposeExportedValue<CoreV2.NuGet.IPackageRepositoryFactory>(new CommandLineRepositoryFactory(console));
                        container.ComposeExportedValue(fileSystem);
                        container.ComposeParts(this);
                    }
                }
                catch (ReflectionTypeLoadException ex) when (ex?.LoaderExceptions.Length > 0)
                {
                    throw new AggregateException(ex.LoaderExceptions);
                }
            }
        }

        // This method acts as a binding redirect
        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name);

            if (string.Equals(name.Name, ThisExecutableName, StringComparison.OrdinalIgnoreCase))
            {
                return typeof(Program).Assembly;
            }

            return null;
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We don't want to block the exe from usage if anything failed")]
        internal static void RemoveOldFile(CoreV2.NuGet.IFileSystem fileSystem)
        {
            var oldFile = typeof(Program).Assembly.Location + ".old";
            try
            {
                if (fileSystem.FileExists(oldFile))
                {
                    fileSystem.DeleteFile(oldFile);
                }
            }
            catch
            {
                // We don't want to block the exe from usage if anything failed
            }
        }

        public static bool ArgumentCountValid(ICommand command)
        {
            var attribute = command.CommandAttribute;
            return command.Arguments.Count >= attribute.MinArgs &&
                   command.Arguments.Count <= attribute.MaxArgs;
        }

        private static void AddExtensionsToCatalog(AggregateCatalog catalog, IConsole console)
        {
            var extensionLocator = new ExtensionLocator();
            var files = extensionLocator.FindExtensions();
            RegisterExtensions(catalog, files, console);
        }

        private static void RegisterExtensions(AggregateCatalog catalog, IEnumerable<string> enumerateFiles, IConsole console)
        {
            foreach (var item in enumerateFiles)
            {
                AssemblyCatalog assemblyCatalog = null;
                try
                {
                    assemblyCatalog = new AssemblyCatalog(item);

                    // get the parts - throw if something went wrong
                    var parts = assemblyCatalog.Parts;

                    // load all the types - throw if assembly cannot load (missing dependencies is a good example)
                    var assembly = Assembly.LoadFile(item);
                    assembly.GetTypes();

                    catalog.Catalogs.Add(assemblyCatalog);
                }
                catch (BadImageFormatException ex)
                {
                    if (assemblyCatalog != null)
                    {
                        assemblyCatalog.Dispose();
                    }

                    // Ignore if the dll wasn't a valid assembly
                    console.WriteWarning(ex.Message);
                }
                catch (FileLoadException ex)
                {
                    // Ignore if we couldn't load the assembly.

                    if (assemblyCatalog != null)
                    {
                        assemblyCatalog.Dispose();
                    }

                    var message =
                        string.Format(LocalizedResourceManager.GetString(nameof(NuGetResources.FailedToLoadExtension)),
                                      item);

                    console.WriteWarning(message);
                    console.WriteWarning(ex.Message);
                }
                catch (ReflectionTypeLoadException rex)
                {
                    // ignore if the assembly is missing dependencies

                    var resource =
                        LocalizedResourceManager.GetString(nameof(NuGetResources.FailedToLoadExtensionDuringMefComposition));

                    var perAssemblyError = string.Empty;

                    if (rex?.LoaderExceptions.Length > 0)
                    {
                        var builder = new StringBuilder();

                        builder.AppendLine(string.Empty);

                        var errors = rex.LoaderExceptions.Select(e => e.Message).Distinct(StringComparer.Ordinal);

                        foreach (var error in errors)
                        {
                            builder.AppendLine(error);
                        }

                        perAssemblyError = builder.ToString();
                    }

                    var warning = string.Format(resource, item, perAssemblyError);

                    console.WriteWarning(warning);
                }
            }
        }

        private static void SetConsoleInteractivity(IConsole console, Command command)
        {
            // Apply command setting
            console.IsNonInteractive = command.NonInteractive;

            // Global environment variable to prevent the exe for prompting for credentials
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NUGET_EXE_NO_PROMPT")))
            {
                console.IsNonInteractive = true;
            }

            // Disable non-interactive if force is set.
            var forceInteractive = Environment.GetEnvironmentVariable("FORCE_NUGET_EXE_INTERACTIVE");
            if (!string.IsNullOrEmpty(forceInteractive))
            {
                console.IsNonInteractive = false;
            }

            if (command != null)
            {
                console.Verbosity = command.Verbosity;
            }
        }

        private static void SetConsoleOutputEncoding(System.Text.Encoding encoding)
        {
            try
            {
                System.Console.OutputEncoding = encoding;
            }
            catch (IOException)
            {
            }
        }

        private static void LogHelperMessageForPathTooLongException(Console logger)
        {
            if (!IsWindows10(logger))
            {
                logger.WriteWarning(LocalizedResourceManager.GetString(nameof(NuGetResources.Warning_LongPath_UnsupportedOS)));
            }
            else if (!IsSupportLongPathEnabled(logger))
            {
                logger.WriteWarning(LocalizedResourceManager.GetString(nameof(NuGetResources.Warning_LongPath_DisabledPolicy)));
            }
            else if (!IsRuntimeGreaterThanNet462(logger))
            {
                logger.WriteWarning(LocalizedResourceManager.GetString(nameof(NuGetResources.Warning_LongPath_UnsupportedNetFramework)));
            }
        }

        private static bool IsWindows10(ILogger logger)
        {
            var productName = (string)RegistryKeyUtility.GetValueFromRegistryKey("ProductName", OSVersionRegistryKey, Registry.LocalMachine, logger);

            return productName != null && productName.StartsWith("Windows 10");
        }

        private static bool IsSupportLongPathEnabled(ILogger logger)
        {
            var longPathsEnabled = RegistryKeyUtility.GetValueFromRegistryKey("LongPathsEnabled", FilesystemRegistryKey, Registry.LocalMachine, logger);

            return longPathsEnabled != null && (int)longPathsEnabled > 0;
        }

        private static bool IsRuntimeGreaterThanNet462(ILogger logger)
        {
            var release = RegistryKeyUtility.GetValueFromRegistryKey("Release", DotNetSetupRegistryKey, Registry.LocalMachine, logger);

            return release != null && (int)release >= Net462ReleasedVersion;
        }
    }
}
