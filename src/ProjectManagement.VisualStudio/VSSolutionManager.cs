using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.ProjectManagement.VisualStudio
{
    [PartCreationPolicy(CreationPolicy.Shared)]
    [Export(typeof(ISolutionManager))]
    public class VSSolutionManager : ISolutionManager, IVsSelectionEvents
    {
        private readonly DTE _dte;
        private readonly SolutionEvents _solutionEvents;
        private readonly IVsMonitorSelection _vsMonitorSelection;
        private readonly uint _solutionLoadedUICookie;
        private readonly IVsSolution _vsSolution;
        private bool _initNeeded;
        private IDictionary<string, EnvDTEProject> _projectCache = null;

        public VSSolutionManager()
            : this(
                ServiceLocator.GetInstance<DTE>(),
                ServiceLocator.GetGlobalService<SVsSolution, IVsSolution>(),
                ServiceLocator.GetGlobalService<SVsShellMonitorSelection, IVsMonitorSelection>())
        {
        }

        internal VSSolutionManager(DTE dte, IVsSolution vsSolution, IVsMonitorSelection vsMonitorSelection)
        {
            if (dte == null)
            {
                throw new ArgumentNullException("dte");
            }

            _initNeeded = true;
            _dte = dte;
            _vsSolution = vsSolution;
            _vsMonitorSelection = vsMonitorSelection;

            // Keep a reference to SolutionEvents so that it doesn't get GC'ed. Otherwise, we won't receive events.
            _solutionEvents = _dte.Events.SolutionEvents;

            // can be null in unit tests
            if (vsMonitorSelection != null)
            {
                Guid solutionLoadedGuid = VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_guid;
                _vsMonitorSelection.GetCmdUIContextCookie(ref solutionLoadedGuid, out _solutionLoadedUICookie);

                uint cookie;
                int hr = _vsMonitorSelection.AdviseSelectionEvents(this, out cookie);
                ErrorHandler.ThrowOnFailure(hr);
            }
            
            //_solutionEvents.BeforeClosing += OnBeforeClosing;
            //_solutionEvents.AfterClosing += OnAfterClosing;
            //_solutionEvents.ProjectAdded += OnProjectAdded;
            //_solutionEvents.ProjectRemoved += OnProjectRemoved;
            //_solutionEvents.ProjectRenamed += OnProjectRenamed;

            // Run the init on another thread to avoid an endless loop of SolutionManager -> Project System -> VSPackageManager -> SolutionManager
            ThreadPool.QueueUserWorkItem(new WaitCallback(Init));
        }

        public NuGetProject DefaultNuGetProject
        {
            get;
            private set;
        }

        public string DefaultNuGetProjectName
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public NuGetProject GetNuGetProject(string nuGetProjectSafeName)
        {
            throw new NotImplementedException();
        }

        public string GetNuGetProjectSafeName(NuGetProject nuGetProject)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<NuGetProject> GetProjects()
        {
            throw new NotImplementedException();
        }

        public bool IsSolutionOpen
        {
            get { throw new NotImplementedException(); }
        }

        public event EventHandler<NuGetProjectEventArgs> NuGetProjectAdded;

        public event EventHandler SolutionClosed;

        public event EventHandler SolutionClosing;

        public string SolutionDirectory
        {
            get { throw new NotImplementedException(); }
        }

        public event EventHandler SolutionOpened;

        /// <summary>
        /// Checks whether the current solution is saved to disk, as opposed to be in memory.
        /// </summary>
        private bool IsSolutionSavedAsRequired()
        {
            // Check if user is doing File - New File without saving the solution.
            object value;
            _vsSolution.GetProperty((int)(__VSPROPID.VSPROPID_IsSolutionSaveAsRequired), out value);
            if ((bool)value)
            {
                return true;
            }

            // Check if user unchecks the "Tools - Options - Project & Soltuions - Save new projects when created" option
            _vsSolution.GetProperty((int)(__VSPROPID2.VSPROPID_DeferredSaveSolution), out value);
            return (bool)value;
        }

        /// <summary>
        /// Invokes the action on the UI thread if one exists.
        /// </summary>
        private void InvokeOnUIThread(Action action)
        {
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(action);
            }
            else
            {
                action();
            }
        }

        private void OnSolutionOpened()
        {
            // we can skip the init if this has already been called
            _initNeeded = false;

            // although the SolutionOpened event fires, the solution may be only in memory (e.g. when
            // doing File - New File). In that case, we don't want to act on the event.
            if (!IsSolutionOpen)
            {
                return;
            }

            EnsureProjectCache();
            SetDefaultProject();
            if (SolutionOpened != null)
            {
                SolutionOpened(this, EventArgs.Empty);
            }
        }

        private void SetDefaultProject()
        {
            // when a new solution opens, we set its startup project as the default project in NuGet Console
            var solutionBuild = (SolutionBuild2)_dte.Solution.SolutionBuild;
            if (solutionBuild.StartupProjects != null)
            {
                IEnumerable<object> startupProjects = (IEnumerable<object>)solutionBuild.StartupProjects;
                string startupProjectName = startupProjects.Cast<string>().FirstOrDefault();
                if (!String.IsNullOrEmpty(startupProjectName))
                {
                    EnvDTEProject startupProject;
                    if (_projectCache.TryGetValue(startupProjectName, out startupProject))
                    {
                        var vsNuGetProjectFactory = new VSNuGetProjectFactory();
                        DefaultNuGetProject = vsNuGetProjectFactory.GetNuGetProject(startupProject, new ToBeDeletedNuGetProjectContext());
                    }
                }
            }
        }

        private void EnsureProjectCache()
        {
            if (IsSolutionOpen && _projectCache == null)
            {
                _projectCache = new Dictionary<string, EnvDTEProject>();

                var allEnvDTEProjects = EnvDTESolutionUtility.GetAllEnvDTEProjects(_dte.Solution);
                foreach (EnvDTEProject envDTEProject in allEnvDTEProjects)
                {
                    var uniqueName = EnvDTEProjectUtility.GetUniqueName(envDTEProject);
                    _projectCache.Add(uniqueName, envDTEProject);
                }
            }
        }

        private void Init(object state)
        {
            try
            {
                if (_initNeeded && _dte.Solution.IsOpen)
                {
                    InvokeOnUIThread(() => OnSolutionOpened());
                }
            }
            catch (Exception)
            {
            }
        }

        #region IVsSelectionEvents implementation

        public int OnCmdUIContextChanged(uint dwCmdUICookie, int fActive)
        {
            //if (dwCmdUICookie == _solutionLoadedUICookie && fActive == 1)
            //{
            //    OnSolutionOpened();
            //    // We must call DeleteMarkedPackageDirectories outside of OnSolutionOpened, because OnSolutionOpened might be called in the constructor
            //    // and DeleteOnRestartManager requires VsFileSystemProvider and RepositorySetings which both have dependencies on SolutionManager.
            //    // In practice, this code gets executed even when a solution is opened directly during Visual Studio startup.
            //    DeleteOnRestartManager.Value.DeleteMarkedPackageDirectories();
            //}

            return VSConstants.S_OK;
        }

        public int OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
        {
            return VSConstants.S_OK;
        }

        public int OnSelectionChanged(IVsHierarchy pHierOld, uint itemidOld, IVsMultiItemSelect pMISOld, ISelectionContainer pSCOld, IVsHierarchy pHierNew, uint itemidNew, IVsMultiItemSelect pMISNew, ISelectionContainer pSCNew)
        {
            return VSConstants.S_OK;
        }

        #endregion 
    }

    internal class ToBeDeletedNuGetProjectContext : INuGetProjectContext
    {

        public void Log(MessageLevel level, string message, params object[] args)
        {
            
        }

        public FileConflictAction ResolveFileConflict(string message)
        {
            return FileConflictAction.IgnoreAll;
        }
    }
}
