using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using NuGetClient.Test.Foundation.TestAttributes.Context;
using NuGetClient.Test.Foundation.TestCommands;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace NuGetClient.Test.Foundation.TestAttributes.Context
{
    public abstract class ContextBaseTestCase : UIThreadTestCase
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Called by the de-serializer", true)]
        public ContextBaseTestCase()
        {
        }

        public ContextBaseTestCase(
            IMessageSink diagnosticMessageSink,
            TestMethodDisplay testMethodDisplay,
            ITestMethod testMethod,
            Context context,
            object[] testMethodArguments = null)
            : base(diagnosticMessageSink, testMethodDisplay, testMethod, testMethodArguments)
        {
            this.Context = context;
        }

        protected override void Initialize()
        {
            base.Initialize();
            if (this.Context == null || this.Context == Context.EmptyContext)
            {
                return;
            }

            string contextDisplayName = this.Context.DisplayName;
            if (!string.IsNullOrEmpty(contextDisplayName))
            {
                this.DisplayName = string.Concat(this.DisplayName, " - ", contextDisplayName);
            }
        }

        public Context Context { get; set; }

        static void Write(Stream stream, string value)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
            stream.WriteByte(0);
        }

        static char NibbleToHexChar(int b)
        {
            Debug.Assert(b < 16);
            return (char)(b < 10 ? b + '0' : (b - 10) + 'a');
        }

        static string BytesToHexString(byte[] bytes)
        {
            char[] chars = new char[bytes.Length * 2];
            int i = 0;
            foreach (byte b in bytes)
            {
                chars[i++] = NibbleToHexChar(b >> 4);
                chars[i++] = NibbleToHexChar(b & 0xF);
            }
            return new string(chars);
        }

        protected override string GetUniqueID()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Write(ms, base.GetUniqueID());
                Write(ms, this.Context.GetUniqueID());

                ms.Position = 0;

                return BytesToHexString(new SHA1Managed().ComputeHash(ms));
            }
        }

        public override void Serialize(IXunitSerializationInfo data)
        {
            base.Serialize(data);
            data.AddValue(nameof(Context), this.Context);
        }

        public override void Deserialize(IXunitSerializationInfo data)
        {
            base.Deserialize(data);
            this.Context = data.GetValue<Context>(nameof(Context));
        }
    }
}
