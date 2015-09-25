// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NuGet.Protocol.Core.v3.Data
{
    public static class Utility
    {
        /// <summary>
        /// True if this object has @context
        /// </summary>
        /// <param name="compacted"></param>
        /// <returns></returns>
        public static bool IsValidJsonLd(JObject compacted)
        {
            return compacted != null && GetContext(compacted) != null;
        }

        public static readonly string[] IdNames = new string[] { Constants.UrlIdName, Constants.IdName };

        public static Uri GetUriWithoutHash(Uri uri)
        {
            if (uri != null)
            {
                var s = uri.AbsoluteUri;
                var hash = s.IndexOf('#');

                if (hash > -1)
                {
                    s = s.Substring(0, hash);
                    return new Uri(s);
                }
            }

            return uri;
        }

        ///// <summary>
        ///// Applies a package's id and version to a URI template
        ///// </summary>
        ///// <param name="template">The URI Template</param>
        ///// <param name="id">The id of the package with natural casing</param>
        ///// <param name="version">The version of the package with natural casing</param>
        ///// <returns>The URI with the template fields applied</returns>
        //public static Uri ApplyPackageIdVersionToUriTemplate(Uri template, string id, Versioning.NuGetVersion version)
        //{
        //    var packageUri = ApplyPackageIdToUriTemplate(template, id);

        //    if (packageUri != null)
        //    {
        //        var versionUrl = packageUri.ToString()
        //            .Replace("{version}", version.ToNormalizedString())
        //            .Replace("{version-lower}", version.ToNormalizedString().ToLowerInvariant());

        //        Uri versionUri = null;
        //        if (Uri.TryCreate(versionUrl, UriKind.Absolute, out versionUri))
        //        {
        //            return versionUri;
        //        }
        //    }

        //    return null;
        //}

        ///// <summary>
        ///// Applies a package's id and version to a list of URI templates
        ///// </summary>
        ///// <param name="templates">The list of URI templates</param>
        ///// <param name="id">The id of the package with natural casing</param>
        ///// <param name="version">The version of the package with natural casing</param>
        ///// <returns>The list of URIs with the template fields applied</returns>
        //public static IEnumerable<Uri> ApplyPackageIdVersionToUriTemplate(IEnumerable<Uri> templates, string id, Versioning.NuGetVersion version)
        //{
        //    foreach (var template in templates)
        //    {
        //        yield return ApplyPackageIdVersionToUriTemplate(template, id, version);
        //    }
        //}

        ///// <summary>
        ///// Applies a package's id to a URI template
        ///// </summary>
        ///// <param name="template">The URI template</param>
        ///// <param name="id">The package's id with natural casing</param>
        ///// <returns>The URI with the template fields applied</returns>
        //public static Uri ApplyPackageIdToUriTemplate(Uri template, string id)
        //{
        //    var packageUrl = template.ToString()
        //        .Replace("{id}", id)
        //        .Replace("{id-lower}", id.ToLowerInvariant());

        //    Uri packageUri = null;
        //    if (Uri.TryCreate(packageUrl, UriKind.Absolute, out packageUri))
        //    {
        //        return packageUri;
        //    }

        //    return null;
        //}

        ///// <summary>
        ///// Applies a package's id to a list of URI templates
        ///// </summary>
        ///// <param name="templates">The list of URI templates</param>
        ///// <param name="id">The package's id with natural casing</param>
        ///// <returns>The list of URIs with the template fields applied</returns>
        //public static IEnumerable<Uri> ApplyPackageIdToUriTemplate(IEnumerable<Uri> templates, string id)
        //{
        //    foreach (var template in templates)
        //    {
        //        yield return ApplyPackageIdToUriTemplate(template, id);
        //    }
        //}

        /// <summary>
        /// Check if the entity url matches the root url
        /// </summary>
        /// <param name="token">entity token</param>
        /// <param name="entityUri">Optional field, if this is given the method will not try to parse it out again.</param>
        /// <returns>true if the root uri is the base of the entity uri</returns>
        public static bool? IsEntityFromPage(JToken token, Uri entityUri = null)
        {
            bool? result = null;
            var uri = entityUri == null ? GetEntityUri(token) : entityUri;

            if (uri != null)
            {
                var rootUri = GetEntityUri(GetRoot(token));

                result = CompareRootUris(uri, rootUri);
            }

            return result;
        }

        /// <summary>
        /// True if the uri does not have a #
        /// </summary>
        public static bool IsRootUri(Uri uri)
        {
            return (uri.AbsoluteUri.IndexOf('#') == -1);
        }

        /// <summary>
        /// Checks if the uris match or differ only in the # part.
        /// If either are null this returns false.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool CompareRootUris(Uri a, Uri b)
        {
            if (a == null
                || b == null)
            {
                return false;
            }

            var x = GetUriWithoutHash(a);
            var y = GetUriWithoutHash(b);

            return x.Equals(y);
        }

        public static JToken GetRoot(JToken token)
        {
            var parent = token;

            while (parent.Parent != null)
            {
                parent = parent.Parent;
            }

            return parent;
        }

        public static JToken GetContext(JObject jObj)
        {
            if (jObj != null)
            {
                JToken context;

                if (jObj.TryGetValue(Constants.ContextName, out context))
                {
                    return context;
                }
            }

            return null;
        }

        public static Uri GetEntityUri(JObject jObj)
        {
            if (jObj != null)
            {
                JToken urlValue;

                foreach (var idName in IdNames)
                {
                    if (jObj.TryGetValue(idName, out urlValue))
                    {
                        return new Uri(urlValue.ToString());
                    }
                }
            }

            return null;
        }

        public static Uri GetEntityUri(JToken token)
        {
            return GetEntityUri(token as JObject);
        }

        public static Task<JToken> FindEntityInJson(Uri entity, JObject jObject)
        {
            var search = entity.AbsoluteUri;

            var idNode = jObject.Descendants().Concat(jObject).Where(n =>
                {
                    var prop = n as JProperty;

                    if (prop != null)
                    {
                        var url = prop.Value.ToString();

                        return StringComparer.Ordinal.Equals(url, search);
                    }

                    return false;
                }).FirstOrDefault();

            if (idNode != null)
            {
                return Task.FromResult(idNode.Parent.DeepClone());
            }

            return null;
        }

        public static bool IsInContext(JToken token)
        {
            var parent = token;

            while (parent != null)
            {
                var prop = parent as JProperty;

                if (prop != null
                    && StringComparer.Ordinal.Equals(prop.Name, Constants.ContextName))
                {
                    return true;
                }

                parent = parent.Parent;
            }

            return false;
        }

        public static void JsonEntityVisitor(JObject root, Action<JObject> visitor)
        {
            var props = root
                .Descendants()
                .Where(t => t.Type == JTokenType.Property)
                .Cast<JProperty>()
                .Where(p => IdNames.Contains(p.Name))
                .ToList();

            foreach (var prop in props)
            {
                visitor((JObject)prop.Parent);
            }
        }
    }
}
