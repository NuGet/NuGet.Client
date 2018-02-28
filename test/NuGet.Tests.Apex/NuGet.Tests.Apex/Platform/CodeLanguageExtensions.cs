using System;
using Microsoft.Test.Apex.VisualStudio.Solution;
using NuGet.Tests.Foundation.TestAttributes.Context;

namespace NuGet.Tests.Apex.Platform
{
    public static class CodeLanguageExtensions
    {
        /// <summary>
        /// Translate CodeLanguage to Apex ProjectLanguage
        /// </summary>
        /// <param name="codeLanguage"></param>
        /// <returns></returns>
        public static ProjectLanguage AsProjectLanguage(this CodeLanguage codeLanguage)
        {
            switch (codeLanguage)
            {
                case CodeLanguage.CPP:
                    return ProjectLanguage.VC;
                case CodeLanguage.CSharp:
                case CodeLanguage.UnspecifiedLanguage:
                    return ProjectLanguage.CSharp;
                case CodeLanguage.VB:
                    return ProjectLanguage.VB;
                case CodeLanguage.JavaScript:
                    return ProjectLanguage.JavaScript;
                case CodeLanguage.TypeScript:
                    return ProjectLanguage.TypeScript;
                case CodeLanguage.FSharp:
                    return ProjectLanguage.FSharp;
                default:
                    throw new ArgumentException("codeLanguage");
            }
        }
    }
}
