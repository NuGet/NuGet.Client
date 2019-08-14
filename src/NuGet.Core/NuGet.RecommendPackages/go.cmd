dotnet pack
dotnet tool uninstall -g nuget.recommendpackages
dotnet tool install -g nuget.recommendpackages --add-source ..\..\..\artifacts\nuget.recommendpackages\16.0\bin\debug\
