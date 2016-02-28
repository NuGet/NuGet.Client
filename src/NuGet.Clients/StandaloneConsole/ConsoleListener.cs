using System;
using System.Threading.Tasks;
using NuGetConsole;

namespace StandaloneConsole
{
    internal class ConsoleListener
    {
        private readonly IHost _host;
        private readonly ICommandExpansion _commandExpansion;

        public bool ShouldExit { get; set; }
        public int ExitCode { get; set; }

        public ConsoleListener(IHost host, ICommandExpansion commandExpansion)
        {
            _host = host;
            _commandExpansion = commandExpansion;
        }

        public async Task RunAsync(IConsole console)
        {
            // Set up the control-C handler.
            Console.CancelKeyPress += new ConsoleCancelEventHandler(HandleControlC);
            Console.TreatControlCAsInput = false;

            var consoleReadLine = new PSLineEditor(_commandExpansion);

            while (!ShouldExit)
            {
                var fc = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write(_host.Prompt);
                Console.ForegroundColor = fc;

                string cmd = consoleReadLine.Read();
                await Task.Run(() => _host.Execute(console, cmd, null)).ConfigureAwait(false);
            }

            // Exit with the desired exit code that was set by the exit command.
            // The exit code is set in the host by the MyHost.SetShouldExit() method.
            Environment.Exit(ExitCode);
        }

        private void HandleControlC(object sender, ConsoleCancelEventArgs e)
        {
            try
            {
                _host.Abort();
                e.Cancel = true;
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.ToString());
            }
        }
    }
}
