using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

namespace NuGet.Client.Models
{
    /// <summary>
    /// Represents the description of a single NuGet service.
    /// </summary>
    public class ServiceDescription : IEquatable<ServiceDescription>
    {
        /// <summary>
        /// Gets the name of the service.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the URL to the root endpoint for the service.
        /// </summary>
        public Uri RootUrl { get; private set; }

        /// <summary>
        /// Creates a <see cref="NuGet.Client.Models.ServiceDescription"/>.
        /// </summary>
        /// <param name="name">The name of the service represented by this object.</param>
        /// <param name="rootUrl">The URL to the root endpoint for this service.</param>
        public ServiceDescription(string name, Uri rootUrl)
        {
            Guard.NotNullOrEmpty(name, "name");
            Guard.NotNull(rootUrl, "rootUrl");
            
            Name = name;
            RootUrl = rootUrl;
        }

        /// <summary>
        /// Determines whether the specified object object is equal to the current <see cref="NuGet.Client.Models.ServiceDescription"/> object.
        /// </summary>
        /// <param name="obj">The object object to compare with the current <see cref="NuGet.Client.Models.ServiceDescription"/> object.</param>
        /// <returns>true if the specified object is a <see cref="NuGet.Client.Models.ServiceDescription"/> and it contains the same service name and root URL as the current <see cref="NuGet.Client.Models.ServiceDescription"/>; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            return Equals(obj as ServiceDescription);
        }

        /// <summary>
        /// Determines whether the specified <see cref="NuGet.Client.Models.ServiceDescription"/> object is equal to the current <see cref="NuGet.Client.Models.ServiceDescription"/> object.
        /// </summary>
        /// <param name="other">The <see cref="NuGet.Client.Models.ServiceDescription"/> object to compare with the current <see cref="NuGet.Client.Models.ServiceDescription"/> object.</param>
        /// <returns>true if the current <see cref="NuGet.Client.Models.ServiceDescription"/> object represents the same service name and root URL as the specified <see cref="NuGet.Client.Models.ServiceDescription"/>; otherwise, false.</returns>
        public bool Equals(ServiceDescription other)
        {
            return other != null &&
                String.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) &&
                Equals(RootUrl, other.RootUrl);
        }

        /// <summary>
        /// Returns a hash code for the current <see cref="NuGet.Client.Models.ServiceDescription"/> object.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode()
        {
            return HashCodeCombiner.Start()
                .Add(Name)
                .Add(RootUrl)
                .CombinedHash;
        }
    }
}
