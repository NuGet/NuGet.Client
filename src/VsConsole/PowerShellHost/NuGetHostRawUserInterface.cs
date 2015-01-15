using System;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Windows.Input;

namespace NuGetConsole.Host.PowerShell.Implementation
{
    class NuGetRawUserInterface : PSHostRawUserInterface
    {
        private readonly NuGetPSHost _host;

        private IConsole Console
        {
            get
            {
                return _host.ActiveConsole;
            }
        }

        public NuGetRawUserInterface(NuGetPSHost host)
        {
            _host = host;
        }

        public override ConsoleColor BackgroundColor
        {
            get
            {
                // default color controlled by Visual Studio
                return NuGetHostUserInterface.NoColor;
            }
            set
            {
            }
        }

        public override Size BufferSize
        {
            get
            {
                return new Size(Console.ConsoleWidth, 0);
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override Coordinates CursorPosition
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override int CursorSize
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override void FlushInputBuffer()
        {
            throw new NotImplementedException();
        }

        public override ConsoleColor ForegroundColor
        {
            get
            {
                // default color controlled by Visual Studio
                return NuGetHostUserInterface.NoColor;
            }
            set
            {
            }
        }

        public override BufferCell[,] GetBufferContents(Rectangle rectangle)
        {
            throw new NotImplementedException();
        }

        public override bool KeyAvailable
        {
            get
            {
                return Console.Dispatcher.IsKeyAvailable;
            }
        }

        public override Size MaxPhysicalWindowSize
        {
            get { throw new NotImplementedException(); }
        }

        public override Size MaxWindowSize
        {
            get { throw new NotImplementedException(); }
        }

        public override KeyInfo ReadKey(ReadKeyOptions options)
        {
            // NOTE: readkey options are ignored as they are not really usable or applicable in PM console.

            VsKeyInfo keyInfo = Console.Dispatcher.WaitKey();

            if (keyInfo == null)
            {
                // abort current pipeline (ESC pressed)
                throw new PipelineStoppedException();
            }

            ControlKeyStates states = default(ControlKeyStates);
            states |= (keyInfo.CapsLockToggled ? ControlKeyStates.CapsLockOn : 0);
            states |= (keyInfo.NumLockToggled ? ControlKeyStates.NumLockOn : 0);
            states |= (keyInfo.ShiftPressed ? ControlKeyStates.ShiftPressed : 0);
            states |= (keyInfo.AltPressed ? ControlKeyStates.LeftAltPressed : 0); // assume LEFT alt
            states |= (keyInfo.ControlPressed ? ControlKeyStates.LeftCtrlPressed : 0); // assume LEFT ctrl

            return new KeyInfo(keyInfo.VirtualKey, keyInfo.KeyChar, states, keyDown: (keyInfo.KeyStates == KeyStates.Down));
        }

        public override void ScrollBufferContents(Rectangle source, Coordinates destination, Rectangle clip, BufferCell fill)
        {
            throw new NotImplementedException();
        }

        public override void SetBufferContents(Rectangle rectangle, BufferCell fill)
        {
            throw new NotImplementedException();
        }

        public override void SetBufferContents(Coordinates origin, BufferCell[,] contents)
        {
            throw new NotImplementedException();
        }

        public override Coordinates WindowPosition
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override Size WindowSize
        {
            get
            {
                return new Size(Console.ConsoleWidth, 0);
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override string WindowTitle
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }
    }
}