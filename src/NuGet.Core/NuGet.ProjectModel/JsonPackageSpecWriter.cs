using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.RuntimeModel;

namespace NuGet.ProjectModel
{
    public class JsonPackageSpecWriter
    {
        public static void WritePackageSpec(PackageSpec packageSpec, string fileName = null)
        {
            // Will write to packageSpec.FilePath if fileName is null

            JObject json = new JObject();

            SetValue(json, "title", packageSpec.Title);
            SetValue(json, "version", packageSpec.Version.ToNormalizedString());
            SetValue(json, "description", packageSpec.Description);
            SetArrayValue(json, "authors", packageSpec.Authors);
            SetArrayValue(json, "owners", packageSpec.Owners);
            SetArrayValue(json, "tags", packageSpec.Tags);
            SetValue(json, "projectUrl", packageSpec.ProjectUrl);
            SetValue(json, "iconUrl", packageSpec.IconUrl);
            SetValue(json, "licenseUrl", packageSpec.LicenseUrl);
            SetValue(json, "copyright", packageSpec.Copyright);
            SetValue(json, "language", packageSpec.Language);
            SetValue(json, "summary", packageSpec.Summary);
            SetValue(json, "releaseNotes", packageSpec.ReleaseNotes);
            SetValue(json, "requireLicenseAcceptance", packageSpec.RequireLicenseAcceptance.ToString());
            SetArrayValue(json, "contentFiles", packageSpec.ContentFiles);
            SetDictionaryValue(json, "packInclude", packageSpec.PackInclude);
            SetDictionaryValues(json, "scripts", packageSpec.Scripts);

            if (packageSpec.Dependencies.Any())
            {
                JObject dependencies = new JObject();
                foreach (var dependency in packageSpec.Dependencies)
                {
                    JObject dependencyObject = new JObject();

                    SetValue(dependencyObject, "include", dependency.IncludeType.ToString());
                    //SetValue(dependencyObject, "type", dependency.LibraryRange.TypeConstraint.ToString());
                    SetValue(dependencyObject, "version", dependency.LibraryRange.VersionRange.ToNormalizedString());
                    SetValue(dependencyObject, "suppressParent", dependency.SuppressParent.ToString());
                    SetValue(dependencyObject, "type", dependency.Type.ToString());
                    //SetValue(dependencyObject, "name", dependency.LibraryRange.Name);
                    
                    dependencies[dependency.Name] = dependencyObject;
                }
                SetValue(json, "dependencies", dependencies);
            }

            if (packageSpec.Tools.Any())
            {
                JObject tools = new JObject();
                foreach (var tool in packageSpec.Tools)
                {
                    JObject toolObject = new JObject();
                    toolObject["version"] = tool.LibraryRange.VersionRange.ToNormalizedString();

                    if (tool.Imports.Any())
                    {
                        JArray imports = new JArray();
                        foreach (var import in tool.Imports)
                        {
                            JObject importObject = new JObject();
                            importObject[import.] = import.DotNetFrameworkName
                            imports.Add()
                        }
                        toolObject["imports"] = imports;
                    }
                    tools[tool.LibraryRange.Name] = toolObject;
                }
            }

            packageSpec.TargetFrameworks;

        }

        private static void SetValue(JObject json, string name, string value)
        {
            if (value != null)
            {
                json[name] = value;
            }
        }

        private static void SetValue(JObject json, string name, JObject value)
        {
            if (value != null)
            {
                json[name] = value;
            }
        }

        private static void SetArrayValue(JObject json, string name, IEnumerable<string> values)
        {
            if (values != null && values.Any())
            {
                json[name] = new JArray(values);
            }
        }

        private static void SetDictionaryValue(JObject json, string name, IDictionary<string, string> values)
        {
            if (values != null && values.Any())
            {
                json[name] = new JObject();
                foreach (var pair in values)
                {
                    json[name][pair.Key] = pair.Value;
                }
            }
        }

        private static void SetDictionaryValues(JObject json, string name, IDictionary<string, IEnumerable<string>> values)
        {
            if (values != null && values.Any())
            {
                json[name] = new JObject();
                foreach (var pair in values)
                {
                    json[name][pair.Key] = new JArray(pair.Value);
                }
            }
        }
    }
}
