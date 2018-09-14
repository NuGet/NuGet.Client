// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GenerateLicenseList
{
    class Program
    {
        static int Main(string[] args)
            
        {
            var licenseDataCodeGenerator = new LicenseDataCodeGenerator(
                @"C:\Users\Roki2\Documents\Code\license-list-data\json\licenses.json",
                @"C:\Users\Roki2\Documents\Code\license-list-data\json\exceptions.json");
  
            var node = licenseDataCodeGenerator.GenerateLicenseDataFile();
            if (node != null)
            {
                var codeIssues = node.GetDiagnostics();

                // WRite to file...but first let's do the parsing.
                Console.ReadLine();
                if (!codeIssues.Any())
                {
                    return 0;
                }
                foreach (var codeIssue in codeIssues)
                {
                    var issue = $"ID: {codeIssue.Id}, Message: {codeIssue.GetMessage()}, Location: {codeIssue.Location.GetLineSpan()}, Severity: {codeIssue.Severity}";
                    Console.WriteLine(issue);
                }

            }
            return -1;
        }


    }
}
