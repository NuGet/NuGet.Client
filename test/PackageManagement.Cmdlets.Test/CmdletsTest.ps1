$ScriptRoot = Split-Path $SCRIPT:MyInvocation.MyCommand.Path
$AssemblyPath = Join-Path -path $ScriptRoot -childPath '..\..\src\PackageManagement.PowerShellCmdlets\bin\debug\PackageManagement.PowerShellCmdlets.dll'
Write-host '$ScriptRoot = ' $ScriptRoot
Write-host '$AssemblyPath = ' $AssemblyPath

Import-Module -Assembly ([Reflection.Assembly]::LoadFrom($AssemblyPath))

try
{
	Install-Package jquery -version 1.4.4
	Write-host "Passed!!!"
}
catch 
{
   Write-host $error[0].Exception.ToString()
   Write-Host "Failed!!!"
}


