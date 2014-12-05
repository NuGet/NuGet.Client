@ECHO OFF

echo ---------DEBUG---------
"C:\Program Files (x86)\Microsoft SDKs\Windows\v8.1A\bin\NETFX 4.5.1 Tools\x64\sn.exe" -Tp src\Versioning\bin\Debug\NuGet.Versioning.dll

echo ---------RELEASE---------
"C:\Program Files (x86)\Microsoft SDKs\Windows\v8.1A\bin\NETFX 4.5.1 Tools\x64\sn.exe" -Tp src\Versioning\bin\Release\NuGet.Versioning.dll