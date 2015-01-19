using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace NuGet.Options
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Microsoft.Interoperability",
        "CA1408:DoNotUseAutoDualClassInterfaceType")]
    [Guid("0F052CF7-BF62-4743-B190-87FA4D49421E")]
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class GeneralOptionPage : OptionsPageBase
    {
        private GeneralOptionControl _optionsWindow;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        protected override System.Windows.Forms.IWin32Window Window
        {
            get
            {
                return GeneralControl;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            GeneralControl.OnClosed();
            base.OnClosed(e);
        }

        protected override void OnActivate(CancelEventArgs e)
        {
            base.OnActivate(e);
            GeneralControl.Font = VsShellUtilities.GetEnvironmentFont(this);
            GeneralControl.OnActivated();
        }

        protected override void OnApply(PageApplyEventArgs e)
        {
            base.OnApply(e);
            GeneralControl.OnApply();
        }

        private GeneralOptionControl GeneralControl
        {
            get
            {
                if (_optionsWindow == null)
                {
                    _optionsWindow = new GeneralOptionControl();
                    _optionsWindow.Location = new Point(0, 0);
                }

                return _optionsWindow;
            }
        }
    }
}