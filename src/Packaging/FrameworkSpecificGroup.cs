using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging
{
    public class FrameworkSpecificGroup
    {
        private readonly string _targetFramework;
        private readonly IEnumerable<string> _items;

        public FrameworkSpecificGroup(string targetFramework, IEnumerable<string> items)
        {
            if (String.IsNullOrEmpty(targetFramework))
            {
                _targetFramework = PackagingConstants.AnyFramework;
            }
            else
            {
                _targetFramework = targetFramework;
            }

            _items = items;
        }

        public string TargetFramework
        {
            get
            {
                return _targetFramework;
            }
        }

        public IEnumerable<string> Items
        {
            get
            {
                return _items;
            }
        }
    }
}
