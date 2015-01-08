using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NuGet.PackageManagement.UI
{
    public class PreviewWindowModel
    {
        private IEnumerable<PreviewResult> _previewResults;

        public IEnumerable<PreviewResult> PreviewResults
        {
            get
            {
                return _previewResults;
            }
        }

        public string Title
        {
            get;
            private set;
        }

        public PreviewWindowModel(IEnumerable<PreviewResult> results)
        {
            _previewResults = results;
            Title = Resources.WindowTitle_Preview;
        }
    }
}