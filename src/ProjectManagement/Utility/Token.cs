using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.ProjectManagement
{
    public class Token
    {
        public string Value { get; private set; }
        public TokenCategory Category { get; private set; }

        public Token(TokenCategory category, string value)
        {
            Category = category;
            Value = value;
        }
    }

    public enum TokenCategory
    {
        Text,
        Variable
    }    
}
