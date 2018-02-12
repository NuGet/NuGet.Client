using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit.Abstractions;

namespace NuGetClient.Test.Foundation.TestAttributes.Context
{
    public class Context : IEquatable<Context>, IXunitSerializable
    {
        protected const string DisplayNameSeparator = ".";
        protected const char UniqueIDSeparator = ' ';

        public static Context EmptyContext = new Context();

        // Any class that implements IXunitSerializable is required to have a public parameterless constructor.
        public Context()
        {
            this.Platform = PlatformIdentifier.UnspecifiedPlatform;
            this.Version = PlatformVersion.UnspecifiedVersion;
            this.Product = Product.UnspecifiedProduct;
            this.Language = CodeLanguage.UnspecifiedLanguage;
            this.SolutionConfiguration = ActiveSolutionConfiguration.UnspecifiedConfiguration;
            this.BuildMethod = BuildMethod.UnspecifiedBuildMethod;
        }

        public Context(
            PlatformIdentifier platform = PlatformIdentifier.UnspecifiedPlatform,
            PlatformVersion version = PlatformVersion.UnspecifiedVersion,
            Product product = Product.UnspecifiedProduct,
            CodeLanguage language = CodeLanguage.UnspecifiedLanguage,
            ActiveSolutionConfiguration solutionConfiguration = ActiveSolutionConfiguration.UnspecifiedConfiguration,
            BuildMethod buildMethod = BuildMethod.UnspecifiedBuildMethod)
        {
            this.Platform = platform;
            this.Version = version;
            this.Product = product;
            this.Language = language;
            this.SolutionConfiguration = solutionConfiguration;
            this.BuildMethod = buildMethod;
        }

        public Context(
            Context defaults,
            PlatformIdentifier platform = PlatformIdentifier.UnspecifiedPlatform,
            PlatformVersion version = PlatformVersion.UnspecifiedVersion,
            Product product = Product.UnspecifiedProduct,
            CodeLanguage language = CodeLanguage.UnspecifiedLanguage,
            ActiveSolutionConfiguration solutionConfiguration = ActiveSolutionConfiguration.UnspecifiedConfiguration,
            BuildMethod buildMethod = BuildMethod.UnspecifiedBuildMethod)
            : this(
                platform: platform == PlatformIdentifier.UnspecifiedPlatform ? defaults.Platform : platform,
                version: version == PlatformVersion.UnspecifiedVersion ? defaults.Version : version,
                product: product == Product.UnspecifiedProduct ? defaults.Product : product,
                language: language == CodeLanguage.UnspecifiedLanguage ? defaults.Language : language,
                solutionConfiguration: solutionConfiguration == ActiveSolutionConfiguration.UnspecifiedConfiguration ? defaults.SolutionConfiguration : solutionConfiguration,
                buildMethod: buildMethod == BuildMethod.UnspecifiedBuildMethod ? defaults.BuildMethod : buildMethod)
        {
        }

        public Product Product { get; set; }
        public PlatformIdentifier Platform { get; set; }
        public PlatformVersion Version { get; set; }
        public CodeLanguage Language { get; set; }
        public ActiveSolutionConfiguration SolutionConfiguration { get; set; }
        public BuildMethod BuildMethod { get; set; }

        public virtual string DisplayName
        {
            get { return string.Join(Context.DisplayNameSeparator, this.SpecifiedProperties); }
        }

        private IEnumerable<string> SpecifiedProperties
        {
            get
            {
                if (this.Platform != PlatformIdentifier.UnspecifiedPlatform)
                {
                    yield return this.Platform.ToString();
                }

                if (this.Version != PlatformVersion.UnspecifiedVersion)
                {
                    yield return this.Version.ToString();
                }

                if (this.Product != Product.UnspecifiedProduct)
                {
                    yield return this.Product.ToString();
                }

                if (this.Language != CodeLanguage.UnspecifiedLanguage)
                {
                    yield return this.Language.ToString();
                }

                if (this.SolutionConfiguration != ActiveSolutionConfiguration.UnspecifiedConfiguration)
                {
                    yield return this.SolutionConfiguration.ToString();
                }

                if (this.BuildMethod != BuildMethod.UnspecifiedBuildMethod)
                {
                    yield return this.BuildMethod.ToString();
                }
            }
        }

        public virtual bool Matches(Context other)
        {
            return (this.Platform == PlatformIdentifier.UnspecifiedPlatform || other.Platform == this.Platform)
                && (this.Version == PlatformVersion.UnspecifiedVersion || other.Version == this.Version)
                && (this.Product == Product.UnspecifiedProduct || other.Product == this.Product)
                && (this.Language == CodeLanguage.UnspecifiedLanguage || other.Language == this.Language)
                && (this.SolutionConfiguration == ActiveSolutionConfiguration.UnspecifiedConfiguration || other.SolutionConfiguration == this.SolutionConfiguration)
                && (this.BuildMethod == BuildMethod.UnspecifiedBuildMethod || other.BuildMethod == this.BuildMethod);
        }

        public virtual bool Equals(Context other)
        {
            if (other == null)
            {
                return false;
            }

            return
                this.Platform == other.Platform
                && this.Version == other.Version
                && this.Language == other.Language
                && this.Product == other.Product
                && this.SolutionConfiguration == other.SolutionConfiguration
                && this.BuildMethod == other.BuildMethod;
        }

        public override bool Equals(object obj)
        {
            Context other = obj as Context;
            if (other == null)
            {
                return false;
            }

            return this.Equals(other);
        }

        public override int GetHashCode()
        {
            return this.Platform.GetHashCode()
                ^ this.Version.GetHashCode()
                ^ this.Language.GetHashCode()
                ^ this.Product.GetHashCode()
                ^ this.SolutionConfiguration.GetHashCode()
                ^ this.BuildMethod.GetHashCode();
        }

        public virtual string GetUniqueID()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} {1} {2} {3} {4} {5}",
                this.Platform,
                this.Version,
                this.Language,
                this.Product,
                this.SolutionConfiguration,
                this.BuildMethod);
        }

        public override string ToString()
        {
            return this.GetUniqueID();
        }

        public virtual void Deserialize(IXunitSerializationInfo info)
        {
            this.Platform = (PlatformIdentifier)Enum.Parse(typeof(PlatformIdentifier), info.GetValue<string>(nameof(Platform)));
            this.Version = (PlatformVersion)Enum.Parse(typeof(PlatformVersion), info.GetValue<string>(nameof(Version)));
            this.Language = (CodeLanguage)Enum.Parse(typeof(CodeLanguage), info.GetValue<string>(nameof(Language)));
            this.Product = (Product)Enum.Parse(typeof(Product), info.GetValue<string>(nameof(Product)));
            this.SolutionConfiguration = (ActiveSolutionConfiguration)Enum.Parse(typeof(ActiveSolutionConfiguration), info.GetValue<string>(nameof(SolutionConfiguration)));
            this.BuildMethod = (BuildMethod)Enum.Parse(typeof(BuildMethod), info.GetValue<string>(nameof(BuildMethod)));
        }

        public virtual void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(Platform), this.Platform.ToString());
            info.AddValue(nameof(Version), this.Version.ToString());
            info.AddValue(nameof(Language), this.Language.ToString());
            info.AddValue(nameof(Product), this.Product.ToString());
            info.AddValue(nameof(SolutionConfiguration), this.SolutionConfiguration.ToString());
            info.AddValue(nameof(BuildMethod), this.BuildMethod.ToString());
        }

        public static bool operator ==(Context left, Context right)
        {
            if (object.ReferenceEquals(left, null))
            {
                return object.ReferenceEquals(right, null);
            }

            return left.Equals(right);
        }

        public static bool operator !=(Context left, Context right)
        {
            return !(left == right);
        }
    }
}


