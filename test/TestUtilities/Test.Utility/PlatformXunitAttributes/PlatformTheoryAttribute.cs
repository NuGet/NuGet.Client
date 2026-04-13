#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGet.Test.Utility
{
    public class PlatformTheoryAttribute
        : TheoryAttribute
    {
        private IEnumerable<string> _platforms = new List<string>();
        private string _platform;
        private IEnumerable<string> _skipPlatforms = new List<string>();
        private string _skipPlatform;
        private bool _onlyOnMono;
        private bool _skipMono;
        private bool _ciOnly;

        public IEnumerable<string> Platforms
        {
            get => _platforms;
            set { _platforms = value; EvaluateSkip(); }
        }

        public string Platform
        {
            get => _platform;
            set { _platform = value; EvaluateSkip(); }
        }

        public IEnumerable<string> SkipPlatforms
        {
            get => _skipPlatforms;
            set { _skipPlatforms = value; EvaluateSkip(); }
        }

        public string SkipPlatform
        {
            get => _skipPlatform;
            set { _skipPlatform = value; EvaluateSkip(); }
        }

        public bool OnlyOnMono
        {
            get => _onlyOnMono;
            set { _onlyOnMono = value; EvaluateSkip(); }
        }

        public bool SkipMono
        {
            get => _skipMono;
            set { _skipMono = value; EvaluateSkip(); }
        }

        public bool CIOnly
        {
            get => _ciOnly;
            set { _ciOnly = value; EvaluateSkip(); }
        }

        /// <summary>
        /// Provide property values to use this attribute.
        /// </summary>
        public PlatformTheoryAttribute()
        {
        }

        /// <summary>
        /// Run only on the given platforms
        /// </summary>
        public PlatformTheoryAttribute(params string[] platforms)
        {
            _platforms = platforms.ToList();
            EvaluateSkip();
        }

        private void EvaluateSkip()
        {
            var skip = XunitAttributeUtility.GetPlatformSkipMessageOrNull(GetAllPlatforms());

            if (string.IsNullOrEmpty(skip))
            {
                skip = XunitAttributeUtility.GetMonoMessage(_onlyOnMono, _skipMono);
            }

            if (string.IsNullOrEmpty(skip))
            {
                if (_ciOnly && !XunitAttributeUtility.IsCI)
                {
                    skip = "This test only runs on the CI. To run it locally set the env var CI=true";
                }
            }

            Skip = skip;
        }

        private string[] GetAllPlatforms()
        {
            var platforms = new HashSet<string>(_platforms ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase)
            {
                _platform
            };

            var skipPlatforms = new HashSet<string>(_skipPlatforms ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase)
            {
                _skipPlatform
            };

            platforms.RemoveWhere(e => string.IsNullOrEmpty(e) || skipPlatforms.Contains(e));

            return platforms.ToArray();
        }
    }
}
