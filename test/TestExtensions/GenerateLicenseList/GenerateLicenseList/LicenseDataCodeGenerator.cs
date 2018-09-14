using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;

namespace GenerateLicenseList
{
    internal class LicenseDataCodeGenerator
    {

        private LicenseDataParser _parser;
        public LicenseDataCodeGenerator(string licenseFile, string exceptionsFile)
        {
            _parser = new LicenseDataParser(licenseFile, exceptionsFile);

        }

        public SyntaxNode GenerateLicenseDataClass()
        {
            var rootNode = CSharpSyntaxTree.ParseText(NamespaceDeclaration).GetRoot();

            var nameSpace = rootNode.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();

            if (nameSpace != null)
            {
                var licenseDataHolder = CSharpSyntaxTree.ParseText(GenerateLicenseData(_parser))
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault();

                var licenseDataClass = CSharpSyntaxTree.ParseText(LicenseData)
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault();

                var exceptionDataClass = CSharpSyntaxTree.ParseText(ExceptionData)
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault();

                var newNameSpace = nameSpace.AddMembers(licenseDataClass, exceptionDataClass);
                rootNode = rootNode.ReplaceNode(nameSpace, newNameSpace);
                rootNode = rootNode.NormalizeWhitespace();

                var bla = rootNode.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
                var newBla = bla.AddMembers(licenseDataHolder);
                rootNode = rootNode.ReplaceNode(bla, newBla);

                var workspace = new AdhocWorkspace();
                var formattedResult = Formatter.Format(rootNode, workspace);

                return formattedResult;
            }
            else
            {
                Console.WriteLine("The namespace could not be found.");
                return null;
            }
        }

        private string GenerateLicenseData(LicenseDataParser licenseDataParser)
        {
            var licenses = licenseDataParser.ParseLicenses();
            var exceptions = licenseDataParser.ParseExceptions();
            if (!licenses.LicenseListVersion.Equals(exceptions.LicenseListVersion))
            {
                throw new ArgumentException("The license list version and the exception list version are not equivalent");
            }

            var value = Environment.NewLine + LicenseDataHolderBase +
                string.Join(Environment.NewLine, licenses.LicenseList.Where(e => e.ReferenceNumber < 3).Select(e => PrettyPrint(e))) +
                Intermediate +
                string.Join(Environment.NewLine, exceptions.ExceptionList.Where(e => e.ReferenceNumber < 3).Select(e => PrettyPrint(e))) +
                last + Environment.NewLine;

            return value;
        }

        private static string PrettyPrint(LicenseData licenseData)
        {
            return $@"        {{""{licenseData.LicenseID}"", new LicenseData(licenseID: ""{licenseData.LicenseID}"", referenceNumber: {licenseData.ReferenceNumber}, isOsiApproved: {licenseData.IsOsiApproved.ToString().ToLowerInvariant()}, isDeprecatedLicenseId: {licenseData.IsDeprecatedLicenseId.ToString().ToLowerInvariant()}) }}, ";
        }

        private static string PrettyPrint(ExceptionData exceptionData)
        {
            return $@"        {{""{exceptionData.LicenseExceptionID}"", new ExceptionData(licenseID: ""{exceptionData.LicenseExceptionID}"", referenceNumber: {exceptionData.ReferenceNumber}, isDeprecatedLicenseId: {exceptionData.IsDeprecatedLicenseId.ToString().ToLowerInvariant()}) }}, ";
        }

        static string LicenseDataHolderBase = $@"internal class NuGetLicenseData
{{
    public static string LicenseListVersion = ""listversion"";

    public static Dictionary<string, LicenseData> LicenseList = new Dictionary<string, LicenseData>()
    {{" + Environment.NewLine;

        //{{licenseID, new LicenseData(licenseID, 0, true, true) }},
        static string Intermediate = Environment.NewLine + $@"    }};

    public static Dictionary<string, ExceptionData> ExceptionList = new Dictionary<string, ExceptionData>()
    {{" + Environment.NewLine;

        //{{exceptionID, new ExceptionData(exceptionID, 0, true) }},
        static string last = Environment.NewLine + $@"    }};" + Environment.NewLine + $@"}}";

        static string LicenseData = $@"internal class LicenseData
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
}}

";


        static string ExceptionData = $@"internal class ExceptionData
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
}}

";

        private static string NamespaceDeclaration = $@"// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Packaging
{{

}}
";
    }
}
