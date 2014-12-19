using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace NuGet.Client
{
    public interface IInstallationTarget
    {
        /// <summary>
        /// Gets the name of this installation target (solution name, project name, etc.)
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets a boolean indicating if this installation target is available to be installed into.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Gets a boolean indicating if this installation target is a solution
        /// </summary>
        bool IsSolution { get; }

        /// <summary>
        /// Gets a list of packages installed into the target.
        /// </summary>
        InstalledPackagesList InstalledPackages { get; }

        /// <summary>
        /// Retrieves an instance of the requested feature, throwing a <see cref="RequiredFeatureNotSupportedException"/>
        /// if the feature is not supported by this host.
        /// </summary>
        /// <typeparam name="T">The type defining the feature to retrieve</typeparam>
        /// <returns>An instance of <typeparamref name="T"/>.</returns>
        /// <exception cref="RequiredFeatureNotSupportedException">The target does not support this feature.</exception>
        T GetRequiredFeature<T>();

        /// <summary>
        /// Retrieves an instance of the requested feature, throwing a <see cref="RequiredFeatureNotSupportedException"/>
        /// if the feature is not supported by this host.
        /// </summary>
        /// <param name="featureType">The type defining the feature to retrieve</param>
        /// <returns>An instance of <paramref name="featureType"/>.</returns>
        /// <exception cref="RequiredFeatureNotSupportedException">The host does not support this feature.</exception>
        object GetRequiredFeature(Type featureType);

        /// <summary>
        /// Retrieves an instance of the requested feature, if one exists in this host.
        /// </summary>
        /// <typeparam name="T">The type defining the feature to retrieve</typeparam>
        /// <returns>An instance of <typeparamref name="T"/>, or null if no such feature exists.</returns>
        T TryGetFeature<T>();

        /// <summary>
        /// Retrieves an instance of the requested feature, if one exists in this host.
        /// </summary>
        /// <param name="featureType">The type defining the feature to retrieve</param>
        /// <returns>An instance of <paramref name="featureType"/>, or null if no such feature exists.</returns>
        object TryGetFeature(Type featureType);

        /// <summary>
        /// Gets the list of frameworks supported by this target.
        /// </summary>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "This method may require computation")]
        IEnumerable<FrameworkName> GetSupportedFrameworks();

        void AddMetricsMetadata(JObject metricsRecord);

    }
}
