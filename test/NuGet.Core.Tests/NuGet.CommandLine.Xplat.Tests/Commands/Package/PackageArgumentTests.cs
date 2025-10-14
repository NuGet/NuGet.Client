using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using FluentAssertions;
using NuGet.CommandLine.XPlat.Commands.Package;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests.Commands.Package
{
    public class PackageArgumentTests
    {
        private static readonly IEqualityComparer<string> DefaultVersionComparer = EqualityComparer<string>.Default;
        private static readonly Func<string, string> DefaultErrorFactory = v => "Invalid version: " + v;

        private static (IReadOnlyList<PackageArgument<string>> Packages, IReadOnlyList<ParseError> Errors)
            ParsePackages(
                string input,
                PackageArgument<string>.TryParseVersion tryParseVersion,
                IEqualityComparer<string> versionComparer = null,
                Func<string, string> errorFactory = null)
        {
            versionComparer ??= DefaultVersionComparer;
            errorFactory ??= DefaultErrorFactory;

            var root = new RootCommand();
            var arg = new Argument<IReadOnlyList<PackageArgument<string>>>("package")
            {
                Arity = ArgumentArity.ZeroOrMore,
                CustomParser = (ArgumentResult r) => PackageArgument<string>.Parse(
                    r,
                    tryParseVersion,
                    errorFactory,
                    versionComparer)
            };
            root.Arguments.Add(arg);

            var result = root.Parse(input);
            var packages = result.GetValue(arg);
            return (packages, result.Errors);
        }

        private static PackageArgument<string> Package(string id, string version, IEqualityComparer<string> cmp = null)
        {
            cmp ??= DefaultVersionComparer;
            var package = new PackageArgument<string>(cmp)
            {
                Id = id,
                Version = version
            };
            return package;
        }

        // A permissive parser used by several tests
        private static bool PassThrough(string value, out string version)
        {
            version = value;
            return true;
        }

        [Theory]
        [InlineData("package")]
        [InlineData("Package")]
        [InlineData("my.cool.pkg")]
        public void Parse_IdOnlyNoVersion_SetsNullVersion(string id)
        {
            // Arrange & Act
            var (packages, errors) = ParsePackages(id, PassThrough);

            // Assert
            packages.Should().BeEquivalentTo(
            [
                Package(id, null)
            ]);
            errors.Should().BeEmpty();
        }

        [Fact]
        public void Parse_MultiplePackages_ParsesAll()
        {
            // Arrange & Act
            var (packages, errors) = ParsePackages("A@1.0.0 B C@[2,3) d@(,5]", PassThrough);

            // Assert
            packages.Should().BeEquivalentTo(
            [
                Package("A", "1.0.0"),
                Package("B", null),
                Package("C", "[2,3)"),
                Package("d", "(,5]")
            ]);
            errors.Should().BeEmpty();
        }

        [Theory]
        [InlineData("pkg@")]
        [InlineData("x@  ")]
        public void Parse_MissingOrEmptyVersion_AddsError(string input)
        {
            // Arrange
            static bool RejectWhitespace(string value, out string version)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    version = default;
                    return false;
                }
                version = value;
                return true;
            }

            // Act & Assert
            var result = Assert.Throws<InvalidOperationException>(() => ParsePackages(input, RejectWhitespace));
            result.Message.Should().Contain("Missing version");
        }

        [Fact]
        public void Parse_InvalidVersion_AddsError()
        {
            // Arrange
            string version = "v1.2.3";
            static bool DigitsOnly(string value, out string version)
            {
                version = default;
                return false;
            }

            // Act & Assert
            var result = Assert.Throws<InvalidOperationException>(() => ParsePackages($"pkg@{version}", DigitsOnly));
            result.Message.Should().Be(DefaultErrorFactory(version));
        }

        [Theory]
        [InlineData("pkg@1.2.3", "1.2.3-normalized")]
        [InlineData("pkg@[1,3)", "[1,3)-normalized")]
        public void Parse_ActionParser_CanTransformVersion(string input, string normalized)
        {
            // Arrange
            static bool Normalize(string value, out string version)
            {
                version = value + "-normalized";
                return true;
            }

            // Act & Assert
            var (packages, errors) = ParsePackages(input, Normalize);

            packages.Should().BeEquivalentTo(
            [
                Package("pkg", normalized)
            ]);
            errors.Should().BeEmpty();
        }

        [Fact]
        public void EqualityComparer_IsCaseInsensitiveOnId_AndUsesProvidedVersionComparer()
        {
            // Arrange
            var firstCharacterComparer = EqualityComparer<string>.Create(
                (a, b) =>
                {
                    if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                    {
                        return a == b;
                    }

                    return a[0] == b[0];
                },
                v =>
                {
                    if (string.IsNullOrEmpty(v))
                    {
                        return 0;
                    }

                    return v[0].GetHashCode();
                });

            var eq = new PackageArgument<string>(firstCharacterComparer) { Id = null, Version = null };

            var a = new PackageArgument<string>(firstCharacterComparer) { Id = "Foo", Version = "1.0.0" };
            var b = new PackageArgument<string>(firstCharacterComparer) { Id = "foo", Version = "1.0.0+meta" };
            var c = new PackageArgument<string>(firstCharacterComparer) { Id = "bar", Version = "1.0.0" };
            var d = new PackageArgument<string>(firstCharacterComparer) { Id = "bar", Version = "2.0.0" };

            // Act & Assert
            eq.Equals(a, b).Should().BeTrue();
            eq.Equals(a, c).Should().BeFalse();
            eq.Equals(c, d).Should().BeFalse();
        }

        [Theory]
        [InlineData("package", "1.2.3")]
        [InlineData("package", "(1,2)")]
        [InlineData("package", "[1,3]")]
        public void Parse_NoActionParser_ReturnsTheSameVersion(string id, string version)
        {
            var (packages, _) = ParsePackages(id + "@" + version, PassThrough);
            packages.Should().BeEquivalentTo([Package(id, version)]);
        }
    }
}
