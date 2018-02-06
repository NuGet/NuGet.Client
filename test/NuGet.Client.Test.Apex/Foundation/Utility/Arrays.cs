using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetClient.Test.Foundation.Utility
{
    // <summary>
    /// Array helpers
    /// </summary>
    public static class Arrays
    {
        /// <summary>
        /// Perform an action for all indicies in an n-dimensional array.
        /// </summary>
        public static void ForEachIndex(this Array array, Action<object> action)
        {
            Action<object, int[]> indexAction = (object value, int[] indices) =>
            {
                action(value);
            };

            array.ForEachIndex(indexAction);
        }

        /// <summary>
        /// Perform an action for all indicies in an n-dimensional array.
        /// </summary>
        /// <param name="action">Action that wants to explicitly know the current index.</param>
        public static void ForEachIndex(this Array array, Action<object, int[]> action)
        {
            int arrayDimensions = array.GetType().GetArrayRank();
            Arrays.WalkArray(array, 1, arrayDimensions, new int[arrayDimensions], action);
        }

        private static void WalkArray(Array array, int currentDimension, int totalDimensions, int[] indices, Action<object, int[]> action)
        {
            int dimensionIndex = currentDimension - 1;
            int dimensionLength = array.GetLength(dimensionIndex);

            if (currentDimension == totalDimensions)
            {
                // At the final dimension of the array, we've got a full set of indices and can perform an action
                for (int i = 0; i < dimensionLength; i++)
                {
                    indices[dimensionIndex] = i;
                    action(array.GetValue(indices: indices), indices);
                }
            }
            else
            {
                // Not at the bottom dimension of the array, recurse
                for (int i = 0; i < dimensionLength; i++)
                {
                    indices[dimensionIndex] = i;

                    Arrays.WalkArray(array, currentDimension + 1, totalDimensions, indices, action);
                }
            }
        }
    }
}
