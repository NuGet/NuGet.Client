using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.UI
{
    // Represents an item in the FileConflictAction combobox.
    public class FileConflictActionOptionItem
    {
        public string Text
        {
            get;
            private set;
        }

        public FileConflictAction Action
        {
            get;
            private set;
        }

        public FileConflictActionOptionItem(string text, FileConflictAction action)
        {
            Text = text;
            Action = action;
        }

        public override string ToString()
        {
            return Text;
        }
    }
}
