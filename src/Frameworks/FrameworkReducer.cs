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

        /// <summary>
        /// Creates a FrameworkReducer using the default framework mappings.
        /// </summary>
        public FrameworkReducer()
            : this(DefaultFrameworkNameProvider.Instance, DefaultCompatibilityProvider.Instance)
        {

        }

        /// <summary>
        /// Creates a FrameworkReducer using custom framework mappings.
        /// </summary>
        public FrameworkReducer(IFrameworkNameProvider mappings, IFrameworkCompatibilityProvider compat)
        {
            _mappings = mappings;
            _compat = compat;
        }

        /// <summary>
        /// Returns the nearest matching framework that is compatible.
        /// </summary>
        /// <param name="framework">Project target framework</param>
        /// <param name="possibleFrameworks">Possible frameworks to narrow down</param>
        /// <returns>Nearest compatible framework. If no frameworks are compatible null is returned.</returns>
        public NuGetFramework GetNearest(NuGetFramework framework, IEnumerable<NuGetFramework> possibleFrameworks)
        {
            NuGetFramework nearest = null;

            // Unsupported frameworks always lose, throw them out unless it's all we were given
            if (possibleFrameworks.Any(e => e != NuGetFramework.UnsupportedFramework))
            {
                possibleFrameworks = possibleFrameworks.Where(e => e != NuGetFramework.UnsupportedFramework);
            }

            var compatible = possibleFrameworks.Where(f => _compat.IsCompatible(framework, f));
            var reduced = ReduceUpwards(compatible).ToArray();

            if (reduced.Length > 1)
            {
                // if we have a pcl and non-pcl mix, throw out the pcls
                if (reduced.Any(f => f.IsPCL) && reduced.Any(f => !f.IsPCL))
                {
                    reduced = reduced.Where(f => !f.IsPCL).ToArray();
                }

                if (reduced.Length > 1 && reduced.All(f => f.IsPCL))
                {
                    // TODO: find the nearest matching PCL
                    throw new NotImplementedException();
                }

                if (reduced.Length > 1)
                {
                    // just take the first one by rev alphabetical order if we can't narrow it down at all
                    nearest = reduced.OrderByDescending(f => f.Framework, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.GetHashCode()).First();
                }
            }
            else if (reduced.Length == 1)
            {
                nearest = reduced[0];
            }

            return nearest;
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
            // NuGetFramework.AnyFramework is a special case
            if (frameworks.Any(e => e != NuGetFramework.AnyFramework))
            {
                // Remove all instances of Any unless it is the only one in the list
                frameworks = frameworks.Where(e => e != NuGetFramework.AnyFramework);
            }

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
            // NuGetFramework.AnyFramework is a special case
            if (frameworks.Any(e => e == NuGetFramework.AnyFramework))
            {
                // Any is always the lowest
                return new NuGetFramework[] { NuGetFramework.AnyFramework };
            }

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
