using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using NuGet.Common;

namespace NuGet.CommandLine
{
    public class Program
    {
        private const string NuGetExtensionsKey = "NUGET_EXTENSIONS_PATH";
        private static readonly string ExtensionsDirectoryRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NuGet", "Commands");

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
            if (args.Contains("--debug"))
            {
                args = args.Skip(1).ToArray();
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

            var console = new Common.Console();
            var fileSystem = new PhysicalFileSystem(workingDirectory);

            Func<Exception, string> getErrorMessage = e => e.Message;

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
                string message;
                Exception unwrappedEx = ExceptionUtility.Unwrap(exception);
                if (unwrappedEx == exception)
                {
                    // If the AggregateException contains more than one InnerException, it cannot be unwrapped. In which case, simply print out individual error messages
                    message = String.Join(Environment.NewLine, exception.InnerExceptions.Select(getErrorMessage).Distinct(StringComparer.CurrentCulture));
                }
                else if (unwrappedEx is System.Reflection.TargetInvocationException)
                {
                    message = getErrorMessage(unwrappedEx.InnerException);
                }
                else
                {
                    message = getErrorMessage(unwrappedEx);
                }
                console.WriteError(message);
                return 1;
            }
            catch (Exception e)
            {
                console.WriteError(getErrorMessage(ExceptionUtility.Unwrap(e)));
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
            using (var catalog = new AggregateCatalog(new AssemblyCatalog(GetType().Assembly)))
            {
                if (!IgnoreExtensions)
                {
                    AddExtensionsToCatalog(catalog, console);
                }
                using (var container = new CompositionContainer(catalog))
                {
                    container.ComposeExportedValue<IConsole>(console);
                    container.ComposeExportedValue<IPackageRepositoryFactory>(new NuGet.Common.CommandLineRepositoryFactory(console));
                    container.ComposeExportedValue<IFileSystem>(fileSystem);
                    container.ComposeParts(this);
                }
            }
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
            IEnumerable<string> directories = new[] { ExtensionsDirectoryRoot };

            var customExtensions = Environment.GetEnvironmentVariable(NuGetExtensionsKey);
            if (!String.IsNullOrEmpty(customExtensions))
            {
                // Add all directories from the environment variable if available.
                directories = directories.Concat(customExtensions.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            }

            IEnumerable<string> files;
            foreach (var directory in directories)
            {
                if (Directory.Exists(directory))
                {
                    files = Directory.EnumerateFiles(directory, "*.dll", SearchOption.AllDirectories);
                    RegisterExtensions(catalog, files, console);
                }
            }
        }

        private static void RegisterExtensions(AggregateCatalog catalog, IEnumerable<string> enumerateFiles, IConsole console)
        {
            foreach (var item in enumerateFiles)
            {
                try
                {
                    catalog.Catalogs.Add(new AssemblyCatalog(item));
                }
                catch (BadImageFormatException ex)
                {
                    // Ignore if the dll wasn't a valid assembly
                    console.WriteWarning(ex.Message);
                }
                catch (FileLoadException ex)
                {
                    // Ignore if we couldn't load the assembly.
                    console.WriteWarning(ex.Message);
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
