using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Versioning;

namespace NuGet.PackageManagement_TestVSExtension
{
    /// <summary>
    /// Interaction logic for MyControl.xaml
    /// </summary>
    public partial class MyControl : UserControl
    {
        private MyControlNuGetProjectContext MyControlNuGetProjectContext { get; set; }
        private ISolutionManager SolutionManager { get; set; }
        private bool _initNeeded = true;
        public MyControl()
        {
            InitializeComponent();
        }

        private void Init()
        {            
            if (_initNeeded)
            {
                _initNeeded = false;
                //SolutionManager = new VSSolutionManager();
                SolutionManager = ServiceLocator.GetInstance<ISolutionManager>();
                MyControlNuGetProjectContext = new MyControlNuGetProjectContext(this);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions")]
        private async void AddPackage(object sender, RoutedEventArgs e)
        {            
            Init();
            try
            {
                var nuGetProject = SolutionManager.DefaultNuGetProject;
                if (nuGetProject != null)
                {
                    MessageBox.Show("You want to add package " + AddPackageId.Text + "." + AddPackageVersion.Text);
                    var packageIdentity = new PackageIdentity(AddPackageId.Text, new NuGetVersion(AddPackageVersion.Text));

                    var packagePath = GetPackagePathFromMachineCache(packageIdentity);
                    if(File.Exists(packagePath))
                    {
                        using(var packageStream = File.OpenRead(packagePath))
                        {
                            await nuGetProject.InstallPackageAsync(packageIdentity, packageStream, MyControlNuGetProjectContext, CancellationToken.None);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Package could not be found in machine cache");
                    }
                }
                else
                {
                    MessageBox.Show("No default NuGetProject available");
                }
            }
            catch (Exception ex)
            {
                MyControlNuGetProjectContext.Log(MessageLevel.Error, ex.ToString());
            }
        }

        private async void RemovePackage(object sender, RoutedEventArgs e)
        {
            Init();
            try
            {
                var nuGetProject = SolutionManager.DefaultNuGetProject;
                if (nuGetProject != null)
                {
                    var addedPackageReference = (await nuGetProject.GetInstalledPackagesAsync(CancellationToken.None))
                        .Where(i => RemovePackageId.Text.Equals(i.PackageIdentity.Id, StringComparison.OrdinalIgnoreCase))
                        .FirstOrDefault();

                    if(addedPackageReference != null)
                    {
                        MessageBox.Show("You want to remove package " + addedPackageReference.PackageIdentity);
                        await nuGetProject.UninstallPackageAsync(addedPackageReference.PackageIdentity,
                            MyControlNuGetProjectContext, CancellationToken.None);
                    }
                    else
                    {
                        MessageBox.Show("No version of package " + RemovePackageId.Text + " is found in the project");
                    }
                }
                else
                {
                    MessageBox.Show("No default NuGetProject available");
                }
            }
            catch (Exception ex)
            {
                MyControlNuGetProjectContext.Log(MessageLevel.Error, ex.ToString());
            }
        }

        private string GetPackagePathFromMachineCache(PackageIdentity packageIdentity)
        {
            const string MachineCachePath = @"C:\Users\daravind\AppData\Local\NuGet\Cache";
            return System.IO.Path.Combine(MachineCachePath, packageIdentity.Id + "." + packageIdentity.Version + ".nupkg");
        }

        private void ClearAll(object sender, RoutedEventArgs e)
        {
            Logger.Text = String.Empty;
            AddPackageId.Text = String.Empty;
            AddPackageVersion.Text = String.Empty;
            RemovePackageId.Text = String.Empty;
        }
    }

    internal class MyControlNuGetProjectContext : INuGetProjectContext
    {
        private MyControl MyControl { get; set; }
        public MyControlNuGetProjectContext(MyControl myControl)
        {
            MyControl = myControl;
        }

        public void Log(MessageLevel level, string message, params object[] args)
        {
            MyControl.Logger.Text += Environment.NewLine;
            MyControl.Logger.Text += String.Format(message, args);
        }

        public FileConflictAction ResolveFileConflict(string message)
        {
            return FileConflictAction.IgnoreAll;
        }


        public PackageExtractionContext PackageExtractionContext
        {
            get;
            set;
        }


        public ISourceControlManagerProvider SourceControlManagerProvider
        {
            get { return null; }
        }

        public ProjectManagement.ExecutionContext ExecutionContext
        {
            get { return null; }
        }

        public void ReportError(string message)
        {
            // no-op
        }
    }
}