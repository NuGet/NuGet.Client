$ScriptRoot = Split-Path $SCRIPT:MyInvocation.MyCommand.Path
$AssemblyPath = Join-Path -path $ScriptRoot -childPath '..\..\src\PackageManagement.PowerShellCmdlets\bin\debug\PackageManagement.PowerShellCmdlets.dll'
write-host '$ScriptRoot = ' $ScriptRoot
write-host '$AssemblyPath = ' $AssemblyPath

if(($env:Path -split ';') -notcontains $ScriptRoot) {
    $env:Path += ';' + $ScriptRoot
	$env:Path += ';' + $CmdletRoot
}

Import-Module -Assembly ([Reflection.Assembly]::LoadFrom($AssemblyPath))

try
{
	Install-Package jquery -version 1.4.4
	Write-Host "Passed!!!"
}
catch 
{
   Write-host $error[0].Exception.ToString()
   Write-Host "Failed!!!"
}


