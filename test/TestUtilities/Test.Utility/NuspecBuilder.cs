using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Test.Utility
{
    public class NuspecBuilder
    {
        public List<Tuple<string, string>> FileEntries { get; private set; }
        public string IconEntry { get; private set; }
        public string IconUrlEntry { get; private set; }
        public string PackageIdEntry { get; private set; } = "testPackage";
        public string PackageVersionEntry { get; private set; } = "0.0.1";

        private NuspecBuilder()
        {
            FileEntries = new List<Tuple<string, string>>();
        }

        public static NuspecBuilder Create()
        {
            var builder = new NuspecBuilder();
            return builder;
        }

        public NuspecBuilder WithIcon(string icon)
        {
            IconEntry = icon;
            return this;
        }

        public NuspecBuilder WithIconUrl(string iconUrl)
        {
            IconUrlEntry = iconUrl;
            return this;
        }

        public NuspecBuilder WithPackageId(string packageId)
        {
            PackageIdEntry = packageId;
            return this;
        }

        public NuspecBuilder WithPackageVersion(string packageVersion)
        {
            PackageVersionEntry = packageVersion;
            return this;
        }

        public NuspecBuilder WithFile(string source)
        {
            return WithFile(source, string.Empty);
        }

        public NuspecBuilder WithFile(string source, string target)
        {
            FileEntries.Add(Tuple.Create(source, target));
            return this;
        }

        public StringBuilder Build()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<package>");
            sb.AppendLine("  <metadata>");
            sb.Append("    <id>").Append(PackageIdEntry).AppendLine("</id>");
            sb.Append("    <version>").Append(PackageVersionEntry).AppendLine("</version>");
            sb.AppendLine("    <authors>Author1, author2</authors>");
            sb.AppendLine("    <description>A short sample description</description>");

            if (IconEntry != null)
            {
                sb.Append("    <icon>").Append(IconEntry).AppendLine("</icon>");
            }

            if (IconUrlEntry != null)
            {
                sb.Append("    <iconUrl>").Append(IconUrlEntry).AppendLine("</iconUrl>");
            }

            sb.AppendLine("  </metadata>");
            sb.AppendLine("  <files>");
            foreach (var fe in FileEntries)
            {
                sb.Append($"    <file src=\"{fe.Item1}\"");
                if (!string.Empty.Equals(fe.Item2))
                {
                    sb.Append($" target=\"{fe.Item2}\"");
                }
                sb.AppendLine(" />");
            }
            sb.AppendLine("  </files>");
            sb.AppendLine("</package>");

            return sb;
        }
    }
}
