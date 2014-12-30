using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Xml;

namespace NuGet.Resolver
{
    public class PackageInfo
    {
        public RegistrationInfo Registration { get; set; }
        public NuGetVersion Version { get; set; }
        public Uri PackageContent { get; set; }
        public IList<DependencyGroupInfo> DependencyGroups { get; private set; }

        public PackageInfo()
        {
            DependencyGroups = new List<DependencyGroupInfo>();
        }

        public void Write(XmlWriter writer)
        {
            writer.WriteStartElement("PackageInfo");
            if (Version != null)
            {
                writer.WriteAttributeString("Version", Version.ToString());
            }
            if (PackageContent != null)
            {
                writer.WriteAttributeString("PackageContent", PackageContent.AbsoluteUri);
            }

            writer.WriteStartElement("DependencyGroups");
            foreach (DependencyGroupInfo dependencyGroupInfo in DependencyGroups)
            {
                dependencyGroupInfo.Write(writer);
            }
            writer.WriteEndElement();

            writer.WriteEndElement();
        }
    }
}
