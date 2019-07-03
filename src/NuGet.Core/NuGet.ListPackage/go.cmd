dotnet pack
dotnet tool uninstall -g nuget.listpackage
dotnet tool install -g nuget.listpackage --add-source ..\..\..\artifacts\nuget.listpackage\16.0\bin\debug\
