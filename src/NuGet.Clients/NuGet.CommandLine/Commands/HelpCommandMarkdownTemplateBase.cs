using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.CommandLine
{
    public class HelpCommandMarkdownTemplateBase
    {
        private System.Text.StringBuilder _generatingEnvironment = new System.Text.StringBuilder();
        protected System.Text.StringBuilder GenerationEnvironment
        {
            get
            {
                return this._generatingEnvironment;
            }
            set
            {
                this._generatingEnvironment = value;
            }
        }
        public virtual void Execute()
        {
        }
        public void WriteLiteral(string textToAppend)
        {

            if (String.IsNullOrEmpty(textToAppend))
            {
                return;
            }
            this.GenerationEnvironment.Append(textToAppend);
        }
        public void Write(object value)
        {

            string stringValue;
            if ((value == null))
            {
                throw new global::System.ArgumentNullException("value");
            }
            System.Type t = value.GetType();
            System.Reflection.MethodInfo method = t.GetMethod("ToString", new System.Type[] {
                            typeof(System.IFormatProvider)});
            if ((method == null))
            {
                stringValue = value.ToString();
            }
            else
            {
                stringValue = ((string)(method.Invoke(value, new object[] { System.Globalization.CultureInfo.InvariantCulture })));
            }
            WriteLiteral(stringValue);

        }

        public string TransformText()
        {
            this.Execute();
            return this.GenerationEnvironment.ToString();
        }
    }
}
