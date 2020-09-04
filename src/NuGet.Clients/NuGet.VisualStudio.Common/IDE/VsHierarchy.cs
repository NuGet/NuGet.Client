// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace NuGet.VisualStudio
{
    /// <summary> Represent a particular tree node in the Solution Explorer window. </summary>
    public sealed class VsHierarchy : IEquatable<VsHierarchy>
    {
        private static readonly string[] UnsupportedProjectCapabilities = new string[]
        {
            "SharedAssetsProject", // This is true for shared projects in universal apps
        };

        private readonly uint _vsitemid;
        private readonly IVsHierarchy _hierarchy;

        public delegate int ProcessItemDelegate(VsHierarchy item, object callerObject, out object newCallerObject);

        private VsHierarchy(IVsHierarchy hierarchy, uint id)
        {
            _vsitemid = id;
            _hierarchy = hierarchy;
        }

        private VsHierarchy(IVsHierarchy hierarchy)
            : this(hierarchy, VSConstants.VSITEMID_ROOT)
        {
        }

        /// <summary> Create a new instance of <see cref="VisualStudio.VsHierarchy"/> from a DTE project object. </summary>
        /// <param name="project"> A DTE project. </param>
        /// <returns> Instance of <see cref="VisualStudio.VsHierarchy"/> wrapping <paramref name="project"/>. </returns>
        public static VsHierarchy FromDteProject(EnvDTE.Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Assumes.Present(project);
            return new VsHierarchy(ToVsHierarchy(project));
        }

        /// <summary> Create a new instance of <see cref="VisualStudio.VsHierarchy"/> from a <see cref="IVsHierarchy"/> object. </summary>
        /// <param name="project"> A <see cref="IVsHierarchy"/> project. </param>
        /// <returns> Instance of <see cref="VisualStudio.VsHierarchy"/> wrapping <paramref name="project"/>. </returns>
        public static VsHierarchy FromVsHierarchy(IVsHierarchy project)
        {
            Assumes.Present(project);
            return new VsHierarchy(project);
        }

        /// <summary> Underlying <see cref="IVsHierarchy"/> object. </summary>
        public IVsHierarchy Ptr => _hierarchy;

        /// <summary> Try getting a project ID. </summary>
        /// <param name="projectId"> Found Project ID. </param>
        /// <returns> True if project ID is found. </returns>
        public bool TryGetProjectId(out Guid projectId)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var result = _hierarchy.GetGuidProperty(
                VSConstants.VSITEMID_ROOT,
                (int)__VSHPROPID.VSHPROPID_ProjectIDGuid,
                out projectId);

            return result == VSConstants.S_OK;
        }


        /// <summary> Find whether project type is supported. </summary>
        /// <param name="projectTypeGuid"> Project type ID. </param>
        /// <returns> True if project type is supported. </returns>
        public bool IsSupported(string projectTypeGuid)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (IsProjectCapabilityCompliant())
            {
                return true;
            }

            return !string.IsNullOrEmpty(projectTypeGuid) && SupportedProjectTypes.IsSupported(projectTypeGuid) && !HasUnsupportedProjectCapability();
        }

        /// <summary> Finds whether project capability matching. </summary>
        /// <returns> True if project matches required capabilities. </returns>
        public bool IsProjectCapabilityCompliant()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return _hierarchy.IsCapabilityMatch("AssemblyReferences + DeclaredSourceItems + UserSourceItems");
        }

        /// <summary> Finds whether project has unsupported project capability. </summary>
        /// <returns> True if project has unsupported project capability. </returns>
        public bool HasUnsupportedProjectCapability()
        {
            return UnsupportedProjectCapabilities.Any(c => _hierarchy.IsCapabilityMatch(c));
        }

        /// <summary> Get project type GUIDs. </summary>
        /// <param name="defaultType"> Default project type. </param>
        /// <returns> Array of project type GUIDs. </returns>
        public string[] GetProjectTypeGuids(string defaultType = "")
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var aggregatableProject = _hierarchy as IVsAggregatableProject;
            if (aggregatableProject != null)
            {
                string projectTypeGuids;
                var hr = aggregatableProject.GetAggregateProjectTypeGuids(out projectTypeGuids);
                ErrorHandler.ThrowOnFailure(hr);

                return projectTypeGuids.Split(';');
            }

            if (!string.IsNullOrEmpty(defaultType))
            {
                return new[] { defaultType };
            }

            return Array.Empty<string>();
        }

        /// <summary> Check for CPS capability in IVsHierarchy. </summary>
        /// <returns> True if project is CPS-based. </returns>
        /// <remarks> All CPS projects will have CPS capability except VisualC projects. So checking for VisualC explicitly with a OR flag. </remarks>
        public bool IsCPSCapabilityComplaint()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return _hierarchy.IsCapabilityMatch("CPS | VisualC");
        }

        /// <summary> Gets <see cref="EnvDTE.Project"/> instance for this hierarchy. </summary>
        /// <returns> A <see cref="EnvDTE.Project"/> instance. </returns>
        public EnvDTE.Project GetDteProject()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Set it to null to avoid unassigned local variable warning
            EnvDTE.Project project = null;
            object projectObject;

            if (_hierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_ExtObject, out projectObject) >= 0)
            {
                project = (EnvDTE.Project)projectObject;
            }

            return project;
        }

        /// <summary> Walk the solution node tree, depth first. </summary>
        /// <param name="fVisible"> Is node visible. </param>
        /// <param name="processCallback"> Processing callback. </param>
        /// <param name="callerObject"> Caller object. </param>
        /// <returns> Return code. </returns>
        public int WalkDepthFirst(bool fVisible, ProcessItemDelegate processCallback, object callerObject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            //
            // TODO Need to see what to do if this is a sub project
            //
            // Get the node type
            // Guid nodeGuid;
            // if (hier.GetGuidProperty(_vsitemid, (int)__VSHPROPID.VSHPROPID_TypeGuid, out nodeGuid) != 0)

            if (processCallback == null)
            {
                return 0;
            }

            object newCallerObject;
            int processReturn = processCallback(this, callerObject, out newCallerObject);
            if (processReturn != 0)
            {
                // Callback says to skip (1) or stop (-1)
                return processReturn;
            }

            // The process callback can change the caller object. If not we just use the one originally
            // passed in.
            if (newCallerObject == null)
            {
                newCallerObject = callerObject;
            }

            // Walk children if there are any
            if (IsExpandable())
            {
                VsHierarchy child = GetFirstChild(fVisible);
                while (child != null)
                {
                    object isNonMemberItemValue = child.GetProperty(__VSHPROPID.VSHPROPID_IsNonMemberItem);
                    // Some project systems (e.g. F#) don't support querying for the VSHPROPID_IsNonMemberItem property.
                    // In that case, we treat this child as belonging to the project
                    bool isMemberOfProject = isNonMemberItemValue == null || (bool)isNonMemberItemValue == false;
                    if (isMemberOfProject)
                    {
                        int returnVal = child.WalkDepthFirst(fVisible, processCallback, newCallerObject);
                        if (returnVal == -1)
                        {
                            return returnVal;
                        }
                    }
                    child = child.GetNextSibling(fVisible);
                }
            }
            return 0;
        }

        /// <summary> Checks if hierarchy item is expanded. </summary>
        /// <param name="uiWindow"> UI window. </param>
        /// <returns> True if item is expanded. </returns>
        public bool IsVsHierarchyItemExpanded(IVsUIHierarchyWindow uiWindow)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!IsExpandable())
            {
                return false;
            }

            const uint expandedStateMask = (uint)__VSHIERARCHYITEMSTATE.HIS_Expanded;
            uint itemState;

            uiWindow.GetItemState(AsVsUIHierarchy(), _vsitemid, expandedStateMask, out itemState);
            return ((__VSHIERARCHYITEMSTATE)itemState == __VSHIERARCHYITEMSTATE.HIS_Expanded);
        }

        /// <summary> Collapse the solution node item. </summary>
        /// <param name="vsHierarchyWindow"> UI window. </param>
        public void Collapse(IVsUIHierarchyWindow vsHierarchyWindow)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (vsHierarchyWindow == null)
            {
                return;
            }

            vsHierarchyWindow.ExpandItem(AsVsUIHierarchy(), _vsitemid, EXPANDFLAGS.EXPF_CollapseFolder);
        }

        /// <summary> Compares two hierarchy items. </summary>
        /// <param name="other"> Other hierarchy item. </param>
        /// <returns> True if they point to same node. </returns>
        public bool Equals(VsHierarchy other)
        {
            return _vsitemid == other._vsitemid;
        }

        /// <summary> Compares it to another object. </summary>
        /// <param name="other"> Other object. </param>
        /// <returns> True if other object is a <see cref="VisualStudio.VsHierarchy"/> and they point to same node. </returns>
        public override bool Equals(object obj)
        {
            var other = obj as VsHierarchy;
            return other != null && Equals(other);
        }

        /// <summary> Gets hash code. </summary>
        /// <returns> Hash code of hierarchy item ID. </returns>
        public override int GetHashCode()
        {
            return _vsitemid.GetHashCode();
        }

        private static IVsHierarchy ToVsHierarchy(EnvDTE.Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            IVsHierarchy hierarchy;

            // Get the vs solution
            var solution = ServiceLocator.GetInstance<IVsSolution>();
            var hr = solution.GetProjectOfUniqueName(EnvDTEProjectInfoUtility.GetUniqueName(project), out hierarchy);

            if (hr != VSConstants.S_OK)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            return hierarchy;
        }

        private bool IsExpandable()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var o = GetProperty(__VSHPROPID.VSHPROPID_Expandable);
            if (o is bool)
            {
                return (bool)o;
            }
            return (o is int) && (int)o != 0;
        }

        private object GetProperty(__VSHPROPID propid)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            object value = null;
            if (TryGetProperty((int)propid, out value))
            {
                return value;
            }

            return null;
        }

        private bool TryGetProperty(int propid, out object value)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            value = null;
            try
            {
                if (_hierarchy != null)
                {
                    _hierarchy.GetProperty(_vsitemid, propid, out value);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private VsHierarchy GetNextSibling(bool fVisible)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            uint childId = GetNextSiblingId(fVisible);
            if (childId != VSConstants.VSITEMID_NIL)
            {
                return new VsHierarchy(_hierarchy, childId);
            }
            return null;
        }

        private uint GetNextSiblingId(bool fVisible)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            object o = GetProperty(fVisible ? __VSHPROPID.VSHPROPID_NextVisibleSibling : __VSHPROPID.VSHPROPID_NextSibling);

            if (o is int)
            {
                return unchecked((uint)((int)o));
            }
            if (o is uint)
            {
                return (uint)o;
            }
            return VSConstants.VSITEMID_NIL;
        }

        private VsHierarchy GetFirstChild(bool fVisible)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            uint childId = GetFirstChildId(fVisible);
            if (childId != VSConstants.VSITEMID_NIL)
            {
                return new VsHierarchy(_hierarchy, childId);
            }
            return null;
        }

        private uint GetFirstChildId(bool fVisible)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            object o = GetProperty(fVisible ? __VSHPROPID.VSHPROPID_FirstVisibleChild : __VSHPROPID.VSHPROPID_FirstChild);

            if (o is int)
            {
                return unchecked((uint)((int)o));
            }
            if (o is uint)
            {
                return (uint)o;
            }
            return VSConstants.VSITEMID_NIL;
        }

        private IVsUIHierarchy AsVsUIHierarchy()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return _hierarchy as IVsUIHierarchy;
        }
    }
}
