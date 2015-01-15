using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace NuGetConsole.Implementation.PowerConsole
{

    [Export(typeof(IClassifierProvider))]
    [ContentType(PowerConsoleWindow.ContentType)]
    class ClassifierProvider : IClassifierProvider
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [Import]
        public IWpfConsoleService WpfConsoleService { get; set; }

        public IClassifier GetClassifier(ITextBuffer textBuffer)
        {
            return WpfConsoleService.GetClassifier(textBuffer) as IClassifier;
        }
    }
}
