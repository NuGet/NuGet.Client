using System;
using Microsoft.Test.Apex;
using Microsoft.Test.Apex.VisualStudio.Skus;
using NuGetClient.Test.Foundation.TestAttributes.Context;

namespace NuGetClient.Test.Integration.Platform
{
    public static class ContextExtesions
    {
        public static ContextImplementation GetImplementation(this Context context)
        {
            return new ContextImplementation(context);
        }

        /// <summary>
        /// Translate Bliss Product to Apex VisualStudioSku.
        /// </summary>
        public static VisualStudioSku TargetSkuConfiguration(this Context context)
        {
            switch (context.Product)
            {
                case Product.Blend:
                    return VisualStudioSku.Blend;
                case Product.VS:
                    return VisualStudioHostSkuFactory.GetDefaultSku();
                default:
                    throw new InvalidOperationException("Unrecognized Product context");
            }
        }
    }
}
