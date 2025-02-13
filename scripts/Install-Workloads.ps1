$VSpath = & "$PSScriptRoot\Get-VSPath.ps1"
$VSSetupPath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\setup.exe"
$VSConfigPath = "$PSScriptRoot\..\.vsconfig"

& $VSSetupPath --installPath $VSpath --config $VSConfigPath --includeRecommended --quiet --norestart