using System.Windows.Threading;

namespace NuGet.Tests.Foundation.Utility
{
    public class DispatcherHelper
    {
        public DispatcherHelper()
        {
            this.Frame = new DispatcherFrame();
        }

        /// <summary>
        /// Clears all of the pending execution frames from the specified dispatcher.
        /// </summary>
        public void ClearFrames(Dispatcher dispatcher)
        {
            dispatcher.BeginInvoke(DispatcherPriority.SystemIdle,
                new DispatcherOperationCallback(this.ExitFrame), this.Frame);
            Dispatcher.PushFrame(this.Frame);
        }

        private object ExitFrame(object frame)
        {
            ((DispatcherFrame)frame).Continue = false;
            this.Frame = null;
            return null;
        }

        public void ExitFrame()
        {
            if (this.Frame != null)
            {
                ExitFrame(this.Frame);
            }
        }

        private DispatcherFrame Frame;
    }
}
