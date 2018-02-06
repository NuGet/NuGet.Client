using System;
using NuGet.VisualStudio;

namespace NuGetClientTestContracts
{
    public class ApexTestUIProject
    {
        private object _packageManagerControl;

        internal ApexTestUIProject(object project)
        {
            _packageManagerControl = project;
        }

        private void UIInvoke(Action action)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                action();
            });
        }

        private T UIInvoke<T>(Func<T> function)
        {
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                return function();
            });
        }
    }
}
