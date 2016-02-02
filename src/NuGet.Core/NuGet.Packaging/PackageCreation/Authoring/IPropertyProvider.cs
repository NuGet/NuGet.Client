namespace NuGet.Packaging
{
    public interface IPropertyProvider
    {
        dynamic GetPropertyValue(string propertyName);
    }
}
