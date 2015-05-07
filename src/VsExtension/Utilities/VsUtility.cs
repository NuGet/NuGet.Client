// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace NuGetVSExtension.Utilities
{
    public static class VsUtility
    {
        public static Project GetActiveProject(IVsMonitorSelection vsMonitorSelection)
        {
            IntPtr ppHier = IntPtr.Zero;
            uint pitemid;
            IVsMultiItemSelect ppMIS;
            IntPtr ppSC = IntPtr.Zero;

            try
            {
                vsMonitorSelection.GetCurrentSelection(out ppHier, out pitemid, out ppMIS, out ppSC);

                if (ppHier == IntPtr.Zero)
                {
                    return null;
                }

                // multiple items are selected.
                if (pitemid == (uint)VSConstants.VSITEMID.Selection)
                {
                    return null;
                }

                IVsHierarchy hierarchy = Marshal.GetTypedObjectForIUnknown(ppHier, typeof(IVsHierarchy)) as IVsHierarchy;
                if (hierarchy != null)
                {
                    object project;
                    if (hierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_ExtObject, out project) >= 0)
                    {
                        return project as Project;
                    }
                }

                return null;
            }
            finally
            {
                if (ppHier != IntPtr.Zero)
                {
                    Marshal.Release(ppHier);
                }
                if (ppSC != IntPtr.Zero)
                {
                    Marshal.Release(ppSC);
                }
            }
        }

        public static bool IsUnloaded(Project project)
        {
            return false; // ***  NuGetVSConstants.UnloadedProjectTypeGuid.Equals(project.Kind, StringComparison.OrdinalIgnoreCase);
        }
    }
}
