namespace NuGet.Packaging
{
    public interface IPropertyProvider
    {
        string GetPropertyValue(string propertyName);
    }
}
