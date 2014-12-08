using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    /// <summary>
    /// Reduces a list of frameworks into the smallest set of frameworks required.
    /// </summary>
    public class FrameworkReducer
    {
        private readonly IFrameworkNameProvider _mappings;
        private readonly IFrameworkCompatibilityProvider _compat;

        public FrameworkReducer()
            : this(DefaultFrameworkNameProvider.Instance, DefaultCompatibilityProvider.Instance)
        {

        }

        public FrameworkReducer(IFrameworkNameProvider mappings, IFrameworkCompatibilityProvider compat)
        {
            _mappings = mappings;
            _compat = compat;
        }

        /// <summary>
        /// Remove duplicates found in the equivalence mappings.
        /// </summary>
        public IEnumerable<NuGetFramework> Reduce(IEnumerable<NuGetFramework> frameworks)
        {
            // order first so we get consistent results for equivalent frameworks
            NuGetFramework[] input = frameworks.OrderBy(f => f.DotNetFrameworkName, StringComparer.OrdinalIgnoreCase).ToArray();

            var comparer = new NuGetFrameworkFullComparer();

            for (int i = 0; i < input.Length; i++)
            {
                bool dupe = false;

                IEnumerable<NuGetFramework> eqFrameworks = null;
                if (!_mappings.TryGetEquivalentFrameworks(input[i], out eqFrameworks))
                {
                    eqFrameworks = new List<NuGetFramework>() { input[i] };
                }

                for (int j = i + 1; !dupe && j < input.Length; j++)
                {
                    dupe = eqFrameworks.Contains(input[j], comparer);
                }

                if (!dupe)
                {
                    yield return input[i];
                }
            }

            yield break;
        }

        /// <summary>
        /// Reduce to the highest framework
        /// Ex: net45, net403, net40 -> net45
        /// </summary>
        public IEnumerable<NuGetFramework> ReduceUpwards(IEnumerable<NuGetFramework> frameworks)
        {
            // x: net40 j: net45 -> remove net40
            // x: wp8 j: win8 -> keep wp8
            return ReduceCore(frameworks, (x, y) => _compat.IsCompatible(y, x)).ToArray();
        }

        /// <summary>
        /// Reduce to the lowest framework
        /// Ex: net45, net403, net40 -> net40
        /// </summary>
        public IEnumerable<NuGetFramework> ReduceDownwards(IEnumerable<NuGetFramework> frameworks)
        {
            return ReduceCore(frameworks, (x, y) => _compat.IsCompatible(x, y)).ToArray();
        }

        private IEnumerable<NuGetFramework> ReduceCore(IEnumerable<NuGetFramework> frameworks, Func<NuGetFramework, NuGetFramework, bool> isCompat)
        {
            // order first so we get consistent results for equivalent frameworks
            NuGetFramework[] input = frameworks.OrderBy(f => f.DotNetFrameworkName, StringComparer.OrdinalIgnoreCase).ToArray();

            for (int i = 0; i < input.Length; i++)
            {
                bool dupe = false;

                NuGetFramework x = input[i];

                for (int j = 0; !dupe && j < input.Length; j++)
                {
                    if (j != i)
                    {
                        NuGetFramework y = input[j];
                        dupe = isCompat(x, y);
                    }
                }

                if (!dupe)
                {
                    yield return input[i];
                }
            }

            yield break;
        }
    }
}
