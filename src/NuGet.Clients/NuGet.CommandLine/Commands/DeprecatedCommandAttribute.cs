using System;

namespace NuGet.CommandLine.Commands
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    class DeprecatedCommandAttribute: Attribute
    {
        public Type AlternativeCommandType { get; private set; }

        public string DeprecationMessage {
            get
            {
                foreach (var customAttribute in AlternativeCommandType.GetCustomAttributes(false))
                {
                    if (customAttribute.GetType().Equals(typeof(CommandAttribute)))
                    {
                        var cmdAttr = customAttribute as CommandAttribute;


                    }
                }

                return "";
            }
        }

        public DeprecatedCommandAttribute(Type altCommand)
        {
            AlternativeCommandType = altCommand;
        }
    }
}
