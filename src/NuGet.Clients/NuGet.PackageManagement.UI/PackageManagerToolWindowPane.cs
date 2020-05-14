// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace NuGet.PackageManagement.UI
{
    [ProvideToolWindow(typeof(PackageManagerToolWindowPane), Style = VsDockStyle.Tabbed, DocumentLikeTool = true, Window = EnvDTE.Constants.vsCATIDDocument)]
    public class PackageManagerToolWindowPane : ToolWindowPane, IVsWindowFrameNotify3
    {
        private PackageManagerControl _content;

        /// <summary>
        /// Initializes a new instance of the EditorPane class.
        /// </summary>
        public PackageManagerToolWindowPane(PackageManagerControl control)
            : base(null)
        {
            _content = control;
        }

        public PackageManagerModel Model
        {
            get { return _content.Model; }
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// IVsWindowPane
        /// </summary>
        /// -----------------------------------------------------------------------------
        public override object Content
        {
            get { return _content; }
        }

        private void CleanUp()
        {
            if (_content != null)
            {
                _content.CleanUp();
                _content = null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    CleanUp();

                    // Because Dispose() will do our cleanup, we can tell the GC not to call the finalizer.

                    // TODO: Mirko - Not sure why this is necessary here but not in PpackageManagerWindowPane. Investigate.

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
                    GC.SuppressFinalize(this);
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        public int OnClose(ref uint pgrfSaveOptions)
        {
            _content.SaveSettings();
            _content.Model.Context.UserSettingsManager.PersistSettings();

            pgrfSaveOptions = (uint)__FRAMECLOSE.FRAMECLOSE_NoSave;
            return VSConstants.S_OK;
        }

        public int OnDockableChange(int fDockable, int x, int y, int w, int h)
        {
            return VSConstants.S_OK;
        }

        public int OnMove(int x, int y, int w, int h)
        {
            return VSConstants.S_OK;
        }

        public int OnShow(int fShow)
        {
            return VSConstants.S_OK;
        }

        public int OnSize(int x, int y, int w, int h)
        {
            return VSConstants.S_OK;
        }
    }
}
