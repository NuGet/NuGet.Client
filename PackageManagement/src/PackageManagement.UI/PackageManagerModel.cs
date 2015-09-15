// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Encapsulates the document model behind the Package Manager document window
    /// </summary>
    /// <remarks>
    /// This class just proxies all calls through to the PackageManagerSession and implements IVsPersistDocData to
    /// fit into the VS model. It's basically an adaptor that turns PackageManagerSession into an IVsPersistDocData
    /// so VS is happy.
    /// </remarks>
    public class PackageManagerModel : IVsPersistDocData, INotifyPropertyChanged
    {
        internal const string EditorFactoryGuidString = "EC269AD5-3EA8-4A13-AAF8-76741843B3CD";
        public static readonly Guid EditorFactoryGuid = new Guid(EditorFactoryGuidString);

        public PackageManagerModel(INuGetUI uiController, INuGetUIContext context, bool isSolution)
        {
            Context = context;
            UIController = uiController;
            IsSolution = isSolution;
        }

        public INuGetUIContext Context { get; }

        // When the model is used for a solution, this property is not null nor empty.
        public string SolutionName { get; set; }

        // Indicates whether the model is used for a solution, or for a project.
        public bool IsSolution { get; private set; }

        public INuGetUI UIController { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #region IVsPersistDocData

        public int Close()
        {
            return VSConstants.S_OK;
        }

        public int GetGuidEditorType(out Guid pClassID)
        {
            pClassID = EditorFactoryGuid;
            return VSConstants.S_OK;
        }

        public int IsDocDataDirty(out int pfDirty)
        {
            pfDirty = 0;
            return VSConstants.S_OK;
        }

        public int IsDocDataReloadable(out int pfReloadable)
        {
            // Reload doesn't make sense
            pfReloadable = 0;
            return VSConstants.S_OK;
        }

        public int LoadDocData(string pszMkDocument)
        {
            return VSConstants.S_OK;
        }

        public int OnRegisterDocData(uint docCookie, IVsHierarchy pHierNew, uint itemidNew)
        {
            return VSConstants.S_OK;
        }

        public int ReloadDocData(uint grfFlags)
        {
            return VSConstants.S_OK;
        }

        public int RenameDocData(uint grfAttribs, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew)
        {
            return VSConstants.E_NOTIMPL;
        }

        public int SaveDocData(VSSAVEFLAGS dwSave, out string pbstrMkDocumentNew, out int pfSaveCanceled)
        {
            // We don't support save as so we don't need to the two out parameters.
            pbstrMkDocumentNew = null;
            pfSaveCanceled = 0;

            return VSConstants.S_OK;
        }

        public int SetUntitledDocPath(string pszDocDataPath)
        {
            return VSConstants.E_NOTIMPL;
        }

        #endregion IVsPersistDocData
    }
}