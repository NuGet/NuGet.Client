#if DNX451
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Framework.Runtime.Roslyn;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.CommonModules.StrongNaming
{
    public class StrongNamingModule : ICompileModule
    {
        public void AfterCompile(IAfterCompileContext context)
        {

        }

        public void BeforeCompile(IBeforeCompileContext context)
        {
            string keyPath = Environment.GetEnvironmentVariable("NUGET_BUILD_KEY_PATH");
            string delaySignString = Environment.GetEnvironmentVariable("NUGET_BUILD_DELAY_SIGN");

            if (!string.IsNullOrEmpty(keyPath))
            {
                FileInfo keyFile = new FileInfo(keyPath);

                if (keyFile.Exists)
                {
                    bool delaySign = delaySignString != null && StringComparer.OrdinalIgnoreCase.Equals("true", delaySignString);

                    // Console.WriteLine("Signing assembly with: {0} Delay sign: {1}", keyFile.FullName, delaySign ? "true" : "false");

                    var parms = new CspParameters();
                    parms.KeyNumber = 2;

                    var provider = new RSACryptoServiceProvider(parms);
                    byte[] array = provider.ExportCspBlob(!provider.PublicOnly);

                    var strongNameProvider = new DesktopStrongNameProvider();


                    var options = context.Compilation.Options.WithStrongNameProvider(strongNameProvider)
                                                                   .WithCryptoKeyFile(keyFile.FullName)
                                                                   .WithDelaySign(delaySign);

                    // Enfore viral strong naming
                    var specificDiagnosticOptions = new Dictionary<string, ReportDiagnostic>(options.SpecificDiagnosticOptions);
                    specificDiagnosticOptions["CS8002"] = ReportDiagnostic.Error;
                    options = options.WithSpecificDiagnosticOptions(specificDiagnosticOptions);

                    context.Compilation = context.Compilation.WithOptions(options);
                }
                else
                {
                    // The key was not found. Throw a compile error.
                    var descriptor = new DiagnosticDescriptor(
                    id: "SN1001",
                    title: "Missing key file",
                    messageFormat: "Key file '{0}' could not be found",
                    category: "CA1001: \"StrongNaming\"",
                    defaultSeverity: DiagnosticSeverity.Error,
                    isEnabledByDefault: true);

                    // TODO: what should this reference for the location?
                    var textSpan = new TextSpan();
                    var position = new LinePosition(0, 0);
                    var span = new LinePositionSpan(position, position);

                    var location = Location.Create(context.ProjectContext.ProjectFilePath, textSpan, span);

                    var diagnsotic = Diagnostic.Create(descriptor, location, keyPath);

                    context.Diagnostics.Add(diagnsotic);
                }
            }
        }
    }
}
#endif