#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGet.Test.Utility
{
    /// <summary>
    /// Skip the test if a file does not exist.
    /// Fail when running on the CI. This requires the CI to have it.
    /// </summary>
    public class FileExistsFactAttribute
        : FactAttribute
    {
        private IEnumerable<string> _paths = new List<string>();
        private string _path;
        private bool _allowCIToSkip;

        public IEnumerable<string> Paths
        {
            get => _paths;
            set { _paths = value; EvaluateSkip(); }
        }

        public string Path
        {
            get => _path;
            set { _path = value; EvaluateSkip(); }
        }

        /// <summary>
        /// If true the CI will be allowed to skip this test.
        /// </summary>
        public bool AllowCIToSkip
        {
            get => _allowCIToSkip;
            set { _allowCIToSkip = value; EvaluateSkip(); }
        }

        public FileExistsFactAttribute()
        {
        }

        public FileExistsFactAttribute(params string[] paths)
        {
            _paths = paths.ToList();
            EvaluateSkip();
        }

        private void EvaluateSkip()
        {
            Skip = XunitAttributeUtility.GetFileExistsSkipMessageOrNull(_allowCIToSkip, GetPaths());
        }

        private string[] GetPaths()
        {
            var paths = new HashSet<string>(_paths ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase)
            {
                _path
            };

            return paths.Where(e => !string.IsNullOrEmpty(e)).ToArray();
        }
    }
}
