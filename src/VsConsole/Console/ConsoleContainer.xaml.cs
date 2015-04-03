using Microsoft.VisualStudio.Shell;
using NuGet.PackageManagement;
using NuGet.PackageManagement.UI;
using System.Windows;
using System.Windows.Controls;

namespace NuGetConsole
{
    /// <summary>
    /// Interaction logic for ConsoleContainer.xaml
    /// </summary>
    public partial class ConsoleContainer : UserControl
    {
        public ConsoleContainer(ISolutionManager solutionManager, IProductUpdateService productUpdateService, IPackageRestoreManager packageRestoreManager)
        {
            InitializeComponent();

            RootLayout.Children.Add(new ProductUpdateBar(productUpdateService));
            RootLayout.Children.Add(new PackageRestoreBar(solutionManager, packageRestoreManager));

            // Set DynamicResource binding in code 
            // The reason we can't set it in XAML is that the VsBrushes class come from either 
            // Microsoft.VisualStudio.Shell.10 or Microsoft.VisualStudio.Shell.11 assembly, 
            // depending on whether NuGet runs inside VS10 or VS11.
            InitializeText.SetResourceReference(TextBlock.ForegroundProperty, VsBrushes.WindowTextKey);
        }

        public void AddConsoleEditor(UIElement content)
        {
            Grid.SetRow(content, 1);
            RootLayout.Children.Add(content);
        }

        public void NotifyInitializationCompleted()
        {
            RootLayout.Children.Remove(InitializeText);
        }
    }
}