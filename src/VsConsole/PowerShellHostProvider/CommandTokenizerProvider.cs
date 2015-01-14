using System;
using System.ComponentModel.Composition;
using NuGetConsole.Host.PowerShell.Implementation;

namespace NuGetConsole.Host.PowerShellProvider
{
    [Export(typeof(ICommandTokenizerProvider))]
    [HostName(PowerShellHostProvider.HostName)]
    internal class CommandTokenizerProvider : ICommandTokenizerProvider
    {
        private readonly Lazy<CommandTokenizer> _instance = new Lazy<CommandTokenizer>(() => new CommandTokenizer());

        public ICommandTokenizer Create(IHost host)
        {
            return _instance.Value;
        }
    }
}