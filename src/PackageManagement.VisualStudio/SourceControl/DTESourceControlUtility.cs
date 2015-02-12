using EnvDTE;
using NuGet.ProjectManagement;
using System.Collections.Generic;
using System.IO;

namespace NuGet.PackageManagement.VisualStudio
{
    public static class DTESourceControlUtility
    {
        public static void EnsureCheckedOutIfExists(SourceControl sourceControl, string fullPath)
        {
            if (File.Exists(fullPath))
            {
                FileSystemUtility.MakeWriteable(fullPath);

                if (sourceControl != null &&
                    sourceControl.IsItemUnderSCC(fullPath) &&
                    !sourceControl.IsItemCheckedOut(fullPath))
                {
                    // Check out the item
                    sourceControl.CheckOutItem(fullPath);
                }
            }
        }

        //public static void AddOrCheckoutItems(SourceControl sourceControl, IEnumerable<string> files)
        //{
        //    if(sourceControl != null)
        //    {
        //        List<object> filesToAdd = new List<object>();
        //        foreach(var path in files)
        //        {
        //            if(File.Exists(path))
        //            {
        //                sourceControl.CheckOutItem(path);
        //            }
        //        }
        //    }
        //}
    }
}
