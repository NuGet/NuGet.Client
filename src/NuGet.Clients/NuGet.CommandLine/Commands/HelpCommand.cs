using System;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NuGet.Common;

namespace NuGet.CommandLine
{
    [Export(typeof(HelpCommand))]
    [Command(typeof(NuGetCommand), "help", "HelpCommandDescription", AltName = "?", MaxArgs = 1,
        UsageSummaryResourceName = "HelpCommandUsageSummary", UsageDescriptionResourceName = "HelpCommandUsageDescription",
        UsageExampleResourceName = "HelpCommandUsageExamples")]
    public class HelpCommand : Command
    {
        private readonly string _commandExe;
        private readonly ICommandManager _commandManager;
        private readonly string _helpUrl;
        private readonly string _productName;

        private string CommandName
        {
            get
            {
                if (Arguments != null && Arguments.Count > 0)
                {
                    return Arguments[0];
                }
                return null;
            }
        }

        [Option(typeof(NuGetCommand), "HelpCommandAll")]
        public bool All { get; set; }

        [Option(typeof(NuGetCommand), "HelpCommandMarkdown")]
        public bool Markdown { get; set; }

        [ImportingConstructor]
        public HelpCommand(ICommandManager commandManager)
            : this(commandManager, Assembly.GetExecutingAssembly().GetName().Name, Assembly.GetExecutingAssembly().GetName().Name, CommandLineConstants.NuGetDocsCommandLineReference)
        {
        }

        [SuppressMessage("Microsoft.Design", "CA1054:UriParametersShouldNotBeStrings", MessageId = "3#",
            Justification = "We don't use the Url for anything besides printing, so it's ok to represent it as a string.")]
        public HelpCommand(ICommandManager commandManager, string commandExe, string productName, string helpUrl)
        {
            _commandManager = commandManager;
            _commandExe = commandExe;
            _productName = productName;
            _helpUrl = helpUrl;
        }

        public override void ExecuteCommand()
        {
            if (!String.IsNullOrEmpty(CommandName))
            {
                ViewHelpForCommand(CommandName);
            }
            else if (All && Markdown)
            {
                ViewMarkdownHelp();
            }
            else if (All)
            {
                ViewHelpForAllCommands();
            }
            else
            {
                ViewHelp();
            }
        }

        public void ViewHelp()
        {
            Console.WriteLine("{0} Version: {1}", _productName, this.GetType().Assembly.GetName().Version);
            Console.WriteLine("usage: {0} <command> [args] [options] ", _commandExe);
            Console.WriteLine("Type '{0} help <command>' for help on a specific command.", _commandExe);
            Console.WriteLine();
            Console.WriteLine("Available commands:");
            Console.WriteLine();

            var commands = from c in _commandManager.GetCommands()
                           orderby c.CommandAttribute.CommandName
                           select c.CommandAttribute;

            // Padding for printing
            int maxWidth = commands.Max(c => c.CommandName.Length + GetAltText(c.AltName).Length);

            foreach (var command in commands)
            {
                PrintCommand(maxWidth, command);
                Console.WriteLine();
            }

            if (_helpUrl != null)
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    LocalizedResourceManager.GetString("HelpCommandForMoreInfo"),
                    _helpUrl));
            }
        }

        private void PrintCommand(int maxWidth, CommandAttribute commandAttribute)
        {
            // Write out the command name left justified with the max command's width's padding
            Console.Write(" {0, -" + maxWidth + "}   ", GetCommandText(commandAttribute));
            // Starting index of the description
            int descriptionPadding = maxWidth + 4;
            Console.PrintJustified(descriptionPadding, commandAttribute.Description);
        }

        private static string GetCommandText(CommandAttribute commandAttribute)
        {
            return commandAttribute.CommandName + GetAltText(commandAttribute.AltName);
        }

        public void ViewHelpForCommand(string commandName)
        {
            ICommand command = _commandManager.GetCommand(commandName);
            CommandAttribute attribute = command.CommandAttribute;

            Console.WriteLine("usage: {0} {1} {2}", _commandExe, attribute.CommandName, attribute.UsageSummary);
            Console.WriteLine();

            if (!String.IsNullOrEmpty(attribute.AltName))
            {
                Console.WriteLine("alias: {0}", attribute.AltName);
                Console.WriteLine();
            }

            Console.WriteLine(attribute.Description);
            Console.WriteLine();

            if (attribute.UsageDescription != null)
            {
                const int padding = 5;
                Console.PrintJustified(padding, attribute.UsageDescription);
                Console.WriteLine();
            }

            var options = _commandManager.GetCommandOptions(command);

            if (options.Count > 0)
            {
                Console.WriteLine("options:");
                Console.WriteLine();

                // Get the max option width. +2 for showing + against multivalued properties
                int maxOptionWidth = options.Max(o => o.Value.Name.Length) + 2;
                // Get the max altname option width
                int maxAltOptionWidth = options.Max(o => (o.Key.AltName ?? String.Empty).Length);

                foreach (var o in options)
                {
                    Console.Write(" -{0, -" + (maxOptionWidth + 2) + "}", o.Value.Name +
                        (TypeHelper.IsMultiValuedProperty(o.Value) ? " +" : String.Empty));
                    Console.Write(" {0, -" + (maxAltOptionWidth + 4) + "}", GetAltText(o.Key.AltName));

                    Console.PrintJustified((10 + maxAltOptionWidth + maxOptionWidth), o.Key.Description);

                }

                if (_helpUrl != null)
                {
                    Console.WriteLine();
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        LocalizedResourceManager.GetString("HelpCommandForMoreInfo"),
                        _helpUrl));
                }

                Console.WriteLine();
            }
        }

        private void ViewHelpForAllCommands()
        {
            var commands = from c in _commandManager.GetCommands()
                           orderby c.CommandAttribute.CommandName
                           select c.CommandAttribute;
            TextInfo info = CultureInfo.CurrentCulture.TextInfo;

            foreach (var command in commands)
            {
                Console.WriteLine(info.ToTitleCase(command.CommandName) + " Command");
                ViewHelpForCommand(command.CommandName);
            }
        }

        /// <summary>
        /// Prints help for all commands in markdown format.
        /// </summary>
        private void ViewMarkdownHelp()
        {
            var commands = from c in _commandManager.GetCommands()
                           orderby c.CommandAttribute.CommandName
                           select c;
            foreach (var command in commands)
            {
                var template = new HelpCommandMarkdownTemplate
                {
                    CommandAttribute = command.CommandAttribute,
                    Options = from item in _commandManager.GetCommandOptions(command)
                              select new { Name = item.Value.Name, Description = item.Key.Description }
                };
                Console.WriteLine(template.TransformText());
            }
        }

        private static string GetAltText(string altNameText)
        {
            if (String.IsNullOrEmpty(altNameText))
            {
                return String.Empty;
            }
            return String.Format(CultureInfo.CurrentCulture, " ({0})", altNameText);
        }

    }
}
