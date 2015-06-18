// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.PackageManagement.UI;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.VisualStudio;
using Test.Utility;

namespace StandaloneUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [Import]
        public INuGetUIFactory _uiServiceFactory;

        [Import]
        public ISettings _settings;

        public ISourceControlManagerProvider _sourceControlManagerProvider;

        public ICommonOperations _commonOperations;

        private CompositionContainer _container;
        private PackageManagerControl _packageManagerControl;

        public MainWindow()
        {
            Brushes.Initialize();
            InitializeComponent();
            _commonOperations = new StandAloneUICommonOperations();
            CreatePackageManagerControl();
        }

        private void CreatePackageManagerControl()
        {
            _container = Initialize();

            // This method is called from MainWindow's constructor. Current thread is the main thread
            var mainThread = Thread.CurrentThread;
            var synchronizationContext = SynchronizationContext.Current;

            NuGetUIThreadHelper.SetCustomJoinableTaskFactory(mainThread, synchronizationContext);

            Title = "NuGet Standalone UI";
            Height = 800;
            Width = 1000;

            var repositoryProvider = new SourceRepositoryProvider(_settings, Repository.Provider.GetVisualStudio());
            var settings = new DefaultSettings();

            var testSolutionManager = new TestSolutionManager(@"c:\temp\test");

            var projectA = testSolutionManager.AddNewMSBuildProject("projectA");
            //var projectB = testSolutionManager.AddNewMSBuildProject("projectB");
            //var projectC = testSolutionManager.AddProjectKProject("projectK");
            var projectBuildIntegrated = testSolutionManager.AddBuildIntegratedProject("BuildIntProj", NuGetFramework.Parse("net46"));

            var projects = new[] { projectBuildIntegrated };

            var packageRestoreManager = new PackageRestoreManager(
                repositoryProvider,
                settings,
                testSolutionManager);

            var contextFactory = new StandaloneUIContextFactory(
                repositoryProvider,
                testSolutionManager,
                settings,
                packageRestoreManager: packageRestoreManager,
                optionsPage: null);
            var context = contextFactory.Create(@"c:\temp\test\settings.txt", projects);
            var uiController = _uiServiceFactory.Create(
                context,
                new NuGetUIProjectContext(new StandaloneUILogger(_textBox, _scrollViewer), _sourceControlManagerProvider, _commonOperations));

            var model = new PackageManagerModel(uiController, context, isSolution: false);
            model.SolutionName = "test solution";
            _packageManagerControl =
                new PackageManagerControl(model, _settings, new SimpleSearchBoxFactory(), vsShell: null);
            layoutGrid.Children.Add(_packageManagerControl);
        }

        private CompositionContainer Initialize()
        {
            var assemblyName = Assembly.GetEntryAssembly().FullName;

            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            using (var catalog = new AggregateCatalog(
                new AssemblyCatalog(Assembly.Load(assemblyName)),
                new DirectoryCatalog(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "*.dll")))
            {
                var container = new CompositionContainer(catalog);

                try
                {
                    container.ComposeParts(this);
                    return container;
                }
                catch (Exception ex)
                {
                    Debug.Fail("MEF: " + ex);

                    throw;
                }
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _packageManagerControl.SaveSettings();
            _packageManagerControl.Model.Context.PersistSettings();
        }
    }

    internal class V3OnlyPackageSourceProvider : IPackageSourceProvider
    {
        public void DisablePackageSource(PackageSource source)
        {
            throw new NotImplementedException();
        }

        public bool IsPackageSourceEnabled(PackageSource source)
        {
            return true;
        }

        public IEnumerable<PackageSource> LoadPackageSources()
        {
            return new List<PackageSource> { new PackageSource("https://api.nuget.org/v3/index.json", "nuget.org v3") };
        }

#pragma warning disable 0067

        public event EventHandler PackageSourcesChanged;

#pragma warning restore 0067

        public void SavePackageSources(IEnumerable<PackageSource> sources)
        {
            throw new NotImplementedException();
        }

        public string ActivePackageSourceName
        {
            get { throw new NotImplementedException(); }
        }

        public void SaveActivePackageSource(PackageSource source)
        {
            throw new NotImplementedException();
        }
    }

    internal class StandAloneUICommonOperations : ICommonOperations
    {
        public Task OpenFile(string fullPath)
        {
            try
            {
                Process.Start(@"C:\windows\system32\notepad.exe", fullPath);
            }
            catch (Exception)
            {
            }
            return Task.FromResult(0);
        }

        public Task SaveSolutionExplorerNodeStates(ISolutionManager solutionManager)
        {
            return Task.FromResult(0);
        }

        public Task CollapseAllNodes(ISolutionManager solutionManager)
        {
            return Task.FromResult(0);
        }
    }
}
