using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.UI
{
    public class PackageManagerWindowPane : WindowPane
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
    }
}
