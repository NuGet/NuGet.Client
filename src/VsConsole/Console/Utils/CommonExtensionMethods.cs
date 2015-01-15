using System;

namespace NuGetConsole
{
    static class CommonExtensionMethods
    {
        public static void Raise(this EventHandler ev, object sender = null, EventArgs e = default(EventArgs))
        {
            if (ev != null)
            {
                ev(sender, e);
            }
        }

        public static void Raise<Args>(this EventHandler<Args> ev, object sender = null, Args e = default(Args))
            where Args : EventArgs
        {
            if (ev != null)
            {
                ev(sender, e);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Microsoft.Performance",
            "CA1811:AvoidUncalledPrivateCode",
            Justification = "This file is shared between two projects. One project uses this method, the other don't.")]
        public static T GetService<T>(this IServiceProvider sp, Type serviceType)
            where T : class
        {
            return (T)sp.GetService(serviceType);
        }
    }
}
