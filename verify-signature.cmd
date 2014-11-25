@echo off

echo ==========NuGet.Packaging.dll==========
"C:\Program Files (x86)\Microsoft SDKs\Windows\v8.1A\bin\NETFX 4.5.1 Tools\x64\sn.exe" -Tp src\Packaging\bin\Debug\NuGet.Packaging.dll

echo ==========NuGet.Frameworks.dll==========
"C:\Program Files (x86)\Microsoft SDKs\Windows\v8.1A\bin\NETFX 4.5.1 Tools\x64\sn.exe" -Tp src\Frameworks\bin\Debug\NuGet.Frameworks.dll

echo ==========NuGet.PackagingCore.dll==========
"C:\Program Files (x86)\Microsoft SDKs\Windows\v8.1A\bin\NETFX 4.5.1 Tools\x64\sn.exe" -Tp src\Core\bin\Debug\NuGet.PackagingCore.dll