#if ASPNET50
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

namespace Microsoft.CommonModules.StrongNaming
{
    public class StrongNamingModule : ICompileModule
    {
        public void AfterCompile(IAfterCompileContext context)
        {

        }

        public void BeforeCompile(IBeforeCompileContext context)
        {
            JObject projectFile = null;
            using (var fs = File.OpenRead(context.ProjectContext.ProjectFilePath))
            {
                projectFile = JObject.Load(new JsonTextReader(new StreamReader(fs)));
            }

            var keyFileValue = projectFile.Value<string>("keyFile");

            if (keyFileValue == null)
            {
                return;
            }

            var keyFile = Path.GetFullPath(Path.Combine(context.ProjectContext.ProjectDirectory, keyFileValue));

            if (!File.Exists(keyFile))
            {
                var descriptor = new DiagnosticDescriptor(
                id: "SN1001",
                title: "Missing key file",
                messageFormat: "Key file '{0}' could not be found",
                category: "CA1001: \"StrongNaming\"",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true);

                var lineInfo = (IJsonLineInfo)projectFile["keyFile"];

                var textSpan = new TextSpan();
                var position = new LinePosition(lineInfo.LineNumber - 1, lineInfo.LinePosition - 1);
                var span = new LinePositionSpan(position, position);

                var location = Location.Create(context.ProjectContext.ProjectFilePath, textSpan, span);

                var diagnsotic = Diagnostic.Create(descriptor, location, keyFile);

                context.Diagnostics.Add(diagnsotic);
                return;
            }

            var parms = new CspParameters();
            parms.KeyNumber = 2;

            var provider = new RSACryptoServiceProvider(parms);
            byte[] array = provider.ExportCspBlob(!provider.PublicOnly);
            var snk = new StrongNameKeyPair(File.ReadAllBytes(keyFile));
            byte[] publicKey = snk.PublicKey;

            var strongNameProvider = new DesktopStrongNameProvider();

            var options = context.Compilation.Options.WithStrongNameProvider(strongNameProvider)
                                                           .WithCryptoKeyFile(keyFile);

            // Enfore viral strong naming
            var specificDiagnosticOptions = new Dictionary<string, ReportDiagnostic>(options.SpecificDiagnosticOptions);
            specificDiagnosticOptions["CS8002"] = ReportDiagnostic.Error;
            options = options.WithSpecificDiagnosticOptions(specificDiagnosticOptions);

            context.Compilation = context.Compilation.WithOptions(options);
            
            // Rewrite [InternalsVisibleTo("assemblyName")] -> [InternalsVisibleToAttribute("assemblyName, PublicKey=key")]
            var assemblyNames = new List<string>();
            foreach (var attribute in context.Compilation.Assembly.GetAttributes())
            {
                if (attribute.AttributeClass.Name == "InternalsVisibleToAttribute")
                {
                    var assemblyName = attribute.ConstructorArguments[0].Value.ToString();
                    assemblyNames.Add(assemblyName);
                }
                else
                {
                    continue;
                }

                var tree = attribute.ApplicationSyntaxReference.SyntaxTree;
                var root = tree.GetRoot();
                var newRoot = root.RemoveNodes(new[] { attribute.ApplicationSyntaxReference.GetSyntax() }, SyntaxRemoveOptions.KeepNoTrivia | SyntaxRemoveOptions.KeepUnbalancedDirectives);
                var newTree = SyntaxFactory.SyntaxTree(newRoot, options: root.SyntaxTree.Options, path: root.SyntaxTree.FilePath, encoding: Encoding.UTF8);

                context.Compilation =
                      context.Compilation.ReplaceSyntaxTree(tree, newTree);
            }

            var hexPublicKey = string.Join("", publicKey.Select(b => b.ToString("x2")));

            var sb = new StringBuilder();
            foreach (var assemblyName in assemblyNames)
            {
                sb.AppendFormat(@"[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(""{0}, PublicKey={1}"")]", assemblyName, hexPublicKey)
                  .AppendLine();
            }

            context.Compilation =
                context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(sb.ToString()));
        }
    }
}
#endif