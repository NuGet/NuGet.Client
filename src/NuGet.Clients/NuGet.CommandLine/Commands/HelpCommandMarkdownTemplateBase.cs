// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text;

namespace NuGet.CommandLine
{
    /// <summary>
    /// Base class for used in the auto-generated Markdown template renderers
    /// <see cref="HelpCommandMarkdownTemplate"/>
    /// </summary>
    /// <remarks>
    /// Inspired from https://github.com/RazorGenerator/RazorGenerator/blob/937972e94d83e71a5a1d372e81c2de98723c1165/RazorGenerator.Templating/RazorTemplateBase.cs
    /// </remarks>
    public abstract class HelpCommandMarkdownTemplateBase
    {
        protected StringBuilder GenerationEnvironment { get; set; } = new StringBuilder();

        /// <summary>
        /// Autognerated code for the template implement this method
        /// </summary>
        public abstract void Execute();

        public void WriteLiteral(string textToAppend)
        {
            if (string.IsNullOrEmpty(textToAppend))
            {
                return;
            }
            GenerationEnvironment.Append(textToAppend);
        }

        public void Write(object value)
        {
            string stringValue;
            if (value == null)
            {
                throw new System.ArgumentNullException("value");
            }
            var t = value.GetType();
            var method = t.GetMethod("ToString", new System.Type[] { typeof(System.IFormatProvider) });
            if (method == null)
            {
                stringValue = value.ToString();
            }
            else
            {
                stringValue = (string)method.Invoke(value, new object[] { System.Globalization.CultureInfo.InvariantCulture });
            }
            WriteLiteral(stringValue);
        }

        public string TransformText()
        {
            Execute();
            return GenerationEnvironment.ToString();
        }
    }
}
