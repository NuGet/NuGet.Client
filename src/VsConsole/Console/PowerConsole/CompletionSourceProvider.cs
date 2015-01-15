using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace NuGetConsole.Implementation.PowerConsole
{

    [Export(typeof(ICompletionSourceProvider))]
    [ContentType(PowerConsoleWindow.ContentType)]
    [Name("PowerConsoleCompletion")]
    class CompletionSourceProvider : ICompletionSourceProvider
    {

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [Import]
        public IWpfConsoleService WpfConsoleService { get; set; }

        public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
        {
            return WpfConsoleService.TryCreateCompletionSource(textBuffer) as ICompletionSource;
        }
    }
}
