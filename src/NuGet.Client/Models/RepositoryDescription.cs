using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Client.Diagnostics;

namespace NuGet.Client.Models
{
    /// <summary>
    /// Describes the version and available services for a NuGet Repository.
    /// </summary>
    public class RepositoryDescription : IEquatable<RepositoryDescription>
    {
        /// <summary>
        /// Gets the version of the NuGet Service Platform in use.
        /// </summary>
        public Version Version { get; private set; }

        /// <summary>
        /// Gets a list of mirror endpoints that provide similar (but not necessarily identical) services over the same data set.
        /// </summary>
        public IList<Uri> Mirrors { get; private set; }

        /// <summary>
        /// Gets a list of services provided by this Repository.
        /// </summary>
        public IList<ServiceDescription> Services { get; private set; }

        /// <summary>
        /// Creates a <see cref="NuGet.Client.Models.RepositoryDescription"/>.
        /// </summary>
        /// <param name="version">The version of the NuGet Service Platform in use.</param>
        /// <param name="mirrors">A list of mirror endpoints.</param>
        /// <param name="services">A list of services provided by this Repository.</param>
        public RepositoryDescription(Version version, IEnumerable<Uri> mirrors, IEnumerable<ServiceDescription> services)
        {
            Guard.NotNull(version, "version");
            Guard.NotNull(mirrors, "mirrors");
            Guard.NotNull(services, "services");

            Version = version;
            Mirrors = mirrors.ToList();
            Services = services.ToList();
        }

        /// <summary>
        /// Determines whether the specified object object is equal to the current <see cref="NuGet.Client.Models.RepositoryDescription"/> object.
        /// </summary>
        /// <param name="obj">The object object to compare with the current <see cref="NuGet.Client.Models.RepositoryDescription"/> object.</param>
        /// <returns>true if the specified object is a <see cref="NuGet.Client.Models.RepositoryDescription"/> and it represents the same version and contains all the same mirrors and services as the current <see cref="NuGet.Client.Models.RepositoryDescription"/>; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            return Equals(obj as RepositoryDescription);
        }

        /// <summary>
        /// Determines whether the specified <see cref="NuGet.Client.Models.RepositoryDescription"/> object is equal to the current <see cref="NuGet.Client.Models.RepositoryDescription"/> object.
        /// </summary>
        /// <param name="other">The <see cref="NuGet.Client.Models.RepositoryDescription"/> object to compare with the current <see cref="NuGet.Client.Models.RepositoryDescription"/> object.</param>
        /// <returns>true if the current <see cref="NuGet.Client.Models.RepositoryDescription"/> object represents the same version and contains all the same mirrors and services as the specified <see cref="NuGet.Client.Models.RepositoryDescription"/>; otherwise, false.</returns>
        public bool Equals(RepositoryDescription other)
        {
            return other != null &&
                Equals(Version, other.Version) &&
                Enumerable.SequenceEqual(Mirrors, other.Mirrors) &&
                Enumerable.SequenceEqual(Services, other.Services);
        }

        /// <summary>
        /// Returns a hash code for the current <see cref="NuGet.Client.Models.RepositoryDescription"/> object.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode()
        {
            return HashCodeCombiner.Start()
                .Add(Version)
                .Add(Mirrors)
                .Add(Services)
                .CombinedHash;
        }

        /// <summary>
        /// Converts the value of the current <see cref="RepositoryDescription"/> object to its equivalent <see cref="String"/> representation.
        /// </summary>
        /// <returns>The <see cref="String"/> representation of the values of the <see cref="RepositoryDescription"/> object</returns>
        public override string ToString()
        {
            return "{version:" + Version.Major + ",services:{" + String.Join(",", Services.Select(s => "\"" + s.Name + "\": \"" + s.RootUrl.ToString() + "\"")) + "},mirrors:[" + String.Join(",", Mirrors.Select(u => "\"" + u.ToString() + "\"")) + "]}";
        }

        internal static RepositoryDescription FromJson(JObject json, Tracer trace, Uri documentRoot)
        {
            Guard.NotNull(json, "json");
            Guard.NotNull(trace, "trace");

            using (trace.EnterExit())
            {
                // Read the version field
                int majorVersion = 0;
                var majorVersionToken = json["version"];
                if (majorVersionToken != null && majorVersionToken.Type != JTokenType.Null)
                {
                    if (majorVersionToken.Type != JTokenType.Integer)
                    {
                        trace.JsonParseWarning(
                            majorVersionToken,
                            String.Format(CultureInfo.CurrentCulture, Strings.RepositoryDescription_InvalidVersion, majorVersionToken.ToDisplayString()));
                    }
                    else
                    {
                        majorVersion = majorVersionToken.Value<int>();

                        if (majorVersion < 0)
                        {
                            trace.JsonParseWarning(
                                majorVersionToken,
                                String.Format(CultureInfo.CurrentCulture, Strings.RepositoryDescription_InvalidVersion, majorVersion));
                            majorVersion = 0;
                        }
                    }
                }
                else
                {
                    trace.JsonParseWarning(
                        json,
                        String.Format(CultureInfo.CurrentCulture, Strings.RepositoryDescription_MissingExpectedProperty, "version"));
                }
                var version = new Version(majorVersion, 0);

                // Read mirrors/alternates
                // TODO: Remove old "alternates" name
                var mirrorsToken = json["mirrors"] ?? json["alternates"];
                IEnumerable<Uri> mirrors;
                if (mirrorsToken != null && mirrorsToken.Type != JTokenType.Null)
                {
                    if (mirrorsToken.Type != JTokenType.Array)
                    {
                        trace.JsonParseWarning(
                            mirrorsToken,
                            String.Format(CultureInfo.CurrentCulture, Strings.RepositoryDescription_InvalidMirrors, mirrorsToken.ToDisplayString()));
                        mirrors = Enumerable.Empty<Uri>();
                    }
                    else
                    {
                        mirrors = JsonParsing.ParseUrlArray((JArray)mirrorsToken, trace, documentRoot, Strings.RepositoryDescription_InvalidMirrorUrl);
                    }
                }
                else
                {
                    trace.JsonParseWarning(
                        json,
                        String.Format(CultureInfo.CurrentCulture, Strings.RepositoryDescription_MissingExpectedProperty, "mirrors"));
                    mirrors = Enumerable.Empty<Uri>();
                }

                // Read services
                var servicesToken = json["services"];
                IEnumerable<ServiceDescription> services;
                if (servicesToken != null && servicesToken.Type != JTokenType.Null)
                {
                    if (servicesToken.Type != JTokenType.Object)
                    {
                        trace.JsonParseWarning(
                            mirrorsToken,
                            String.Format(CultureInfo.CurrentCulture, Strings.RepositoryDescription_InvalidServices, servicesToken.ToDisplayString()));
                        services = Enumerable.Empty<ServiceDescription>();
                    }
                    else
                    {
                        services = JsonParsing.ParseUrlDictionary(json.Value<JObject>("services"), trace, documentRoot, Strings.RepositoryDescription_InvalidServiceUrl).Select(pair => new ServiceDescription(pair.Key, pair.Value));
                    }
                }
                else
                {
                    trace.JsonParseWarning(
                        json,
                        String.Format(CultureInfo.CurrentCulture, Strings.RepositoryDescription_MissingExpectedProperty, "services"));
                    services = Enumerable.Empty<ServiceDescription>();
                }

                // Create the object!
                return new RepositoryDescription(version, mirrors, services);
            }
        }
    }
}
