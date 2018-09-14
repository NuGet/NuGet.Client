using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace GenerateLicenseList
{
    internal class LicenseDataCodeGenerator
    {

        private LicenseDataParser _parser;
        public LicenseDataCodeGenerator(string licenseFile, string exceptionsFile)
        {
            _parser = new LicenseDataParser(licenseFile, exceptionsFile);
        }

        private ClassDeclarationSyntax GetLicenseDataHolderClass()
        {
            return CSharpSyntaxTree.ParseText(GenerateLicenseData(_parser))
                   .GetRoot()
                   .DescendantNodes()
                   .OfType<ClassDeclarationSyntax>()
                   .FirstOrDefault();
        }

        public SyntaxNode GenerateLicenseDataFile()
        {
            var rootNode = CSharpSyntaxTree.ParseText(NamespaceDeclaration).GetRoot();

            var nameSpace = rootNode.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();

            if (nameSpace != null)
            {
                var licenseDataClass = GetLicenseDataClass();
                var exceptionDataClass = GetExceptionDataClass();
                var licenseDataHolder = GetLicenseDataHolderClass();

                var newNameSpace = nameSpace.AddMembers(licenseDataClass, exceptionDataClass, licenseDataHolder);
                rootNode = rootNode.ReplaceNode(nameSpace, newNameSpace);
                var workspace = new AdhocWorkspace();
                return Formatter.Format(rootNode, workspace);
            }
            else
            {
                Console.WriteLine("The namespace could not be found.");
                return null;
            }
        }

        private ClassDeclarationSyntax GetLicenseDataClass()
        {
            var licenseDataFormattedClass =
                Environment.NewLine +
                CSharpSyntaxTree.ParseText(LicenseData)
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault().NormalizeWhitespace().ToFullString() +
                Environment.NewLine;

            return CSharpSyntaxTree.ParseText(licenseDataFormattedClass)
                .GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault();
        }

        private ClassDeclarationSyntax GetExceptionDataClass()
        {
            var exceptionDataFormattedClass =
                Environment.NewLine +
                CSharpSyntaxTree.ParseText(ExceptionData)
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault().NormalizeWhitespace().ToFullString() +
                Environment.NewLine;

            return CSharpSyntaxTree.ParseText(exceptionDataFormattedClass)
                 .GetRoot()
                 .DescendantNodes()
                 .OfType<ClassDeclarationSyntax>()
                 .FirstOrDefault();
        }

        private string GenerateLicenseData(LicenseDataParser licenseDataParser)
        {
            var licenses = licenseDataParser.ParseLicenses();
            var exceptions = licenseDataParser.ParseExceptions();
            if (!licenses.LicenseListVersion.Equals(exceptions.LicenseListVersion))
            {
                throw new ArgumentException("The license list version and the exception list version are not equivalent");
            }

            return Environment.NewLine + Environment.NewLine + LicenseDataHolderBase +
                string.Join(Environment.NewLine, licenses.LicenseList.Where(e => e.ReferenceNumber < 3).Select(e => PrettyPrint(e))) +
                Intermediate +
                string.Join(Environment.NewLine, exceptions.ExceptionList.Where(e => e.ReferenceNumber < 3).Select(e => PrettyPrint(e))) +
                last;
        }

        private static string PrettyPrint(LicenseData licenseData)
        {
            return $@"            {{""{licenseData.LicenseID}"", new LicenseData(licenseID: ""{licenseData.LicenseID}"", referenceNumber: {licenseData.ReferenceNumber}, isOsiApproved: {licenseData.IsOsiApproved.ToString().ToLowerInvariant()}, isDeprecatedLicenseId: {licenseData.IsDeprecatedLicenseId.ToString().ToLowerInvariant()}) }}, ";
        }

        private static string PrettyPrint(ExceptionData exceptionData)
        {
            return $@"            {{""{exceptionData.LicenseExceptionID}"", new ExceptionData(licenseID: ""{exceptionData.LicenseExceptionID}"", referenceNumber: {exceptionData.ReferenceNumber}, isDeprecatedLicenseId: {exceptionData.IsDeprecatedLicenseId.ToString().ToLowerInvariant()}) }}, ";
        }

        private static string LicenseDataHolderBase = $@"internal class NuGetLicenseData
{{
    public static string LicenseListVersion = ""listversion"";

    public static Dictionary<string, LicenseData> LicenseList = new Dictionary<string, LicenseData>()
        {{" + Environment.NewLine;

        private static string Intermediate = Environment.NewLine + $@"        }};

    public static Dictionary<string, ExceptionData> ExceptionList = new Dictionary<string, ExceptionData>()
        {{" + Environment.NewLine;

        private static string last = Environment.NewLine + $@"        }};" + Environment.NewLine + $@"}}";

        private static string LicenseData = $@"internal class LicenseData
{{
    public LicenseData(string licenseID, int referenceNumber, bool isOsiApproved, bool isDeprecatedLicenseId)
    {{
        LicenseID = licenseID;
        ReferenceNumber = referenceNumber;
        IsOsiApproved = isOsiApproved;
        IsDeprecatedLicenseId = isDeprecatedLicenseId;
    }}

    string LicenseID {{ get; }}
    int ReferenceNumber {{ get; }}
    bool IsOsiApproved {{ get; }}
    bool IsDeprecatedLicenseId {{ get; }}
}}";

        private static string ExceptionData = $@"internal class ExceptionData
{{
    public ExceptionData(string licenseID, int referenceNumber, bool isDeprecatedLicenseId)
    {{
        LicenseExceptionID = licenseID;
        ReferenceNumber = referenceNumber;
        IsDeprecatedLicenseId = isDeprecatedLicenseId;
    }}

    string LicenseExceptionID {{ get; }}
    int ReferenceNumber {{ get; }}
    bool IsDeprecatedLicenseId {{ get; }}
}}";

        private static string NamespaceDeclaration = $@"// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Packaging
{{

}}
";
    }
}
