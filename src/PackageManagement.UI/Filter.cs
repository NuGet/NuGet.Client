namespace NuGet.PackageManagement.UI
{
    public enum Filter
    {
        All,
        Installed,
        UpdatesAvailable
    }

    // The item added to the filter combobox on the UI
    public class FilterItem
    {
        public FilterItem(Filter filter, string text)
        {
            Filter = filter;
            Text = text;
        }

        public Filter Filter
        {
            get;
            private set;
        }

        // The text that is displayed on UI
        public string Text
        {
            get;
            private set;
        }
    }
}