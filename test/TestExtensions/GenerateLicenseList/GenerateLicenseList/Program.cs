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
  
            var node = licenseDataCodeGenerator.GenerateLicenseDataClass();
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
