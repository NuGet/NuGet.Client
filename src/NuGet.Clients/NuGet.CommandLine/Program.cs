using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using NuGet.Common;

namespace NuGet.CommandLine
{
    public class Program
    {
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
#if DEBUG
            if (args.Contains("--debug", StringComparer.OrdinalIgnoreCase))
            {
                args = args.Where(arg => !string.Equals(arg, "--debug", StringComparison.OrdinalIgnoreCase)).ToArray();
                System.Diagnostics.Debugger.Launch();
            }
#endif

            return MainCore(Directory.GetCurrentDirectory(), args);
        }

        public static int MainCore(string workingDirectory, string[] args)
        {
            // This is to avoid applying weak event pattern usage, which breaks under Mono or restricted environments, e.g. Windows Azure Web Sites.
            EnvironmentUtility.SetRunningFromCommandLine();

            // set output encoding to UTF8 if -utf8 is specified
            var oldOutputEncoding = System.Console.OutputEncoding;
            if (args.Any(arg => String.Equals(arg, "-utf8", StringComparison.OrdinalIgnoreCase)))
            {
                args = args.Where(arg => !String.Equals(arg, "-utf8", StringComparison.OrdinalIgnoreCase)).ToArray();
                SetConsoleOutputEncoding(System.Text.Encoding.UTF8);
            }

            // Increase the maximum number of connections per server.
            if (!EnvironmentUtility.IsMonoRuntime)
            {
                ServicePointManager.DefaultConnectionLimit = 64;
            }
            else
            {
                // Keep mono limited to a single download to avoid issues.
                ServicePointManager.DefaultConnectionLimit = 1;
            }

            var console = new Common.Console();
            var fileSystem = new PhysicalFileSystem(workingDirectory);

            Func<Exception, string> getErrorMessage = ExceptionUtilities.DisplayMessage;

            try
            {
                // Remove NuGet.exe.old
                RemoveOldFile(fileSystem);

                // Import Dependencies
                var p = new Program();
                p.Initialize(fileSystem, console);

                // Add commands to the manager
                foreach (ICommand cmd in p.Commands)
                {
                    p.Manager.RegisterCommand(cmd);
                }

                CommandLineParser parser = new CommandLineParser(p.Manager);

                // Parse the command
                ICommand command = parser.ParseCommandLine(args) ?? p.HelpCommand;
                command.CurrentDirectory = workingDirectory;

                // Fallback on the help command if we failed to parse a valid command
                if (!ArgumentCountValid(command))
                {
                    // Get the command name and add it to the argument list of the help command
                    string commandName = command.CommandAttribute.CommandName;

                    // Print invalid command then show help
                    console.WriteLine(LocalizedResourceManager.GetString("InvalidArguments"), commandName);

                    p.HelpCommand.ViewHelpForCommand(commandName);
                }
                else
                {
                    SetConsoleInteractivity(console, command as Command);

                    // When we're detailed, get the whole exception including the stack
                    // This is useful for debugging errors.
                    if (console.Verbosity == Verbosity.Detailed)
                    {
                        getErrorMessage = e => e.ToString();
                    }

                    command.Execute();
                }
            }
            catch (AggregateException exception)
            {
                Exception unwrappedEx = ExceptionUtility.Unwrap(exception);
                if (unwrappedEx is ExitCodeException)
                {
                    // Return the exit code without writing out the exception type
                    var exitCodeEx = unwrappedEx as ExitCodeException;
                    return exitCodeEx.ExitCode;
                }
                
                console.WriteError(getErrorMessage(exception));
                return 1;
            }
            catch (Exception exception)
            {
                console.WriteError(getErrorMessage(exception));
                return 1;
            }
            finally
            {
                OptimizedZipPackage.PurgeCache();
                SetConsoleOutputEncoding(oldOutputEncoding);
            }

            return 0;
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

        private void Initialize(IFileSystem fileSystem, IConsole console)
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
                        container.ComposeExportedValue<IPackageRepositoryFactory>(new CommandLineRepositoryFactory(console));
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
        internal static void RemoveOldFile(IFileSystem fileSystem)
        {
            string oldFile = typeof(Program).Assembly.Location + ".old";
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
            CommandAttribute attribute = command.CommandAttribute;
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
                        String.Format(LocalizedResourceManager.GetString(nameof(NuGetResources.FailedToLoadExtension)),
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
            // Global environment variable to prevent the exe for prompting for credentials
            string globalSwitch = Environment.GetEnvironmentVariable("NUGET_EXE_NO_PROMPT");

            // When running from inside VS, no input is available to our executable locking up VS.
            // VS sets up a couple of environment variables one of which is named VisualStudioVersion.
            // Every time this is setup, we will just fail.
            // TODO: Remove this in next iteration. This is meant for short-term backwards compat.
            string vsSwitch = Environment.GetEnvironmentVariable("VisualStudioVersion");

            console.IsNonInteractive = !String.IsNullOrEmpty(globalSwitch) ||
                                       !String.IsNullOrEmpty(vsSwitch) ||
                                       (command != null && command.NonInteractive);

            string forceInteractive = Environment.GetEnvironmentVariable("FORCE_NUGET_EXE_INTERACTIVE");
            if (!String.IsNullOrEmpty(forceInteractive))
            {
                console.IsNonInteractive = false;
            }

            if (command != null)
            {
                console.Verbosity = command.Verbosity;
            }
        }
    }
}
