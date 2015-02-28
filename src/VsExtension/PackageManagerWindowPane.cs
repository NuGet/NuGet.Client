using System;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement.UI;

namespace NuGetVSExtension
{
    public class PackageManagerWindowPane : WindowPane, IVsWindowFrameNotify3
    {
        private PackageManagerControl _content;

        /// <summary>
        /// Initializes a new instance of the EditorPane class.
        /// </summary>
        public PackageManagerWindowPane(PackageManagerControl control)
            : base(null)
        {
            _content = control;
        }

        public PackageManagerModel Model
        {
            get
            {
                return _content.Model;
            }
        }

        ///-----------------------------------------------------------------------------
        /// <summary>
        /// IVsWindowPane
        /// </summary>
        ///-----------------------------------------------------------------------------
        public override object Content
        {
            get
            {
                return _content;
            }
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
                    GC.SuppressFinalize(this);
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
            _content.Model.Context.PersistSettings();

            pgrfSaveOptions = (uint)__FRAMECLOSE.FRAMECLOSE_NoSave;
            return Microsoft.VisualStudio.VSConstants.S_OK;
        }

        public int OnDockableChange(int fDockable, int x, int y, int w, int h)
        {
            return Microsoft.VisualStudio.VSConstants.S_OK;
        }

        public int OnMove(int x, int y, int w, int h)
        {
            return Microsoft.VisualStudio.VSConstants.S_OK;
        }

        public int OnShow(int fShow)
        {
            return Microsoft.VisualStudio.VSConstants.S_OK;
        }

        public int OnSize(int x, int y, int w, int h)
        {
            return Microsoft.VisualStudio.VSConstants.S_OK;
        }
    }
}