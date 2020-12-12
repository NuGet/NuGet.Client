// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

#pragma warning disable CA1062 // Validate arguments of public methods

namespace NuGet.VisualStudio
{
    public static class VsMonitorSelectionExtensions
    {
        public static EnvDTE.Project GetActiveProject(this IVsMonitorSelection vsMonitorSelection)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

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
                    uint numberOfSelectedItems;
                    int isSingleHierarchyInt;
                    if (ErrorHandler.Succeeded(ppMIS.GetSelectionInfo(out numberOfSelectedItems, out isSingleHierarchyInt)))
                    {
                        bool isSingleHierarchy = (isSingleHierarchyInt != 0);

                        VSITEMSELECTION[] vsItemSelections = new VSITEMSELECTION[numberOfSelectedItems];
                        uint flags = 0; // No flags, which will give us back a hierarchy for each item
                        ErrorHandler.ThrowOnFailure(ppMIS.GetSelectedItems(flags, numberOfSelectedItems, vsItemSelections));

                        if (isSingleHierarchy)
                        {
                            EnvDTE.Project lastProject = null;
                            foreach (VSITEMSELECTION sel in vsItemSelections)
                            {
                                if (sel.pHier != null)
                                {
                                    IVsHierarchy selHierarchy = Marshal.GetTypedObjectForIUnknown(ppHier, typeof(IVsHierarchy)) as IVsHierarchy;
                                    if (selHierarchy != null)
                                    {
                                        object project;
                                        if (selHierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_ExtObject, out project) >= 0)
                                        {
                                            var thisProject = project as EnvDTE.Project;
                                            if (lastProject == null)
                                            {
                                                lastProject = thisProject;
                                            }

                                            if (thisProject != lastProject)
                                            {
                                                return null;
                                            }
                                        }
                                    }
                                }
                            }

                            return lastProject;
                        }
                    }
                }

                IVsHierarchy hierarchy = Marshal.GetTypedObjectForIUnknown(ppHier, typeof(IVsHierarchy)) as IVsHierarchy;
                if (hierarchy != null)
                {
                    object project;
                    if (hierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_ExtObject, out project) >= 0)
                    {
                        return project as EnvDTE.Project;
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
    }
}
