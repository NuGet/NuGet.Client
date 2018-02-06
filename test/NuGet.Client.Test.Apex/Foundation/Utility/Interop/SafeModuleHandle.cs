using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace NuGetClient.Test.Foundation.Utility.Interop
{
    public class SafeModuleHandle : SafeHandle
    {
        public SafeModuleHandle()
            : base(invalidHandleValue: IntPtr.Zero, ownsHandle: true)
        {
        }

        public override bool IsInvalid
        {
            get { return this.handle == IntPtr.Zero; }
        }

        protected override bool ReleaseHandle()
        {
            if (this.IsClosed)
            {
                return true;
            }

            return NativeMethods.FreeLibrary(this);
        }
    }
}
