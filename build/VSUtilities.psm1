function Start-VsDevShell {
    $chosenVisualStudioVersion = ChooseVisualStudioInstallation @(Get-VSInstalls)
    if ($chosenVisualStudioVersion -eq $null) {
            return
    }

    $installDir = $chosenVisualStudioVersion.InstallationPath
    $devShellModule = Join-Path $installDir 'Common7\Tools\Microsoft.VisualStudio.DevShell.dll'
    Import-Module $devShellModule
    Enter-VsDevShell -DevCmdArguments "-no_logo" -StartInPath "$pwd" $chosenVisualStudioVersion.InstanceId
}

function Get-VsComnToolsPath {
    if([string]::IsNullOrEmpty($env:VSINSTALLDIR)) {
        $chosenVisualStudioVersion = ChooseVisualStudioInstallation @(Get-VSInstalls)
        $installDir = $chosenVisualStudioVersion.InstallationPath
    }
    else {
        $installDir = $env:VSINSTALLDIR
    }

    if([string]::IsNullOrEmpty($installDir)) {
        return
    }

    $vsComnToolsPath = Join-Path $installDir 'Common7\Tools\'
    return $vsComnToolsPath
}

function Get-VSWhere {
    $ProgramFile = ${env:ProgramFiles(x86)}

    if ($null -eq $ProgramFile) {
        $ProgramFile = (Get-ChildItem env:ProgramFiles).Value
    }

    $vsWhereVersion = "2.8.4"
    $vsWhereDir = Join-Path $ProgramFile 'Microsoft Visual Studio\Installer'
    $vsWhereExe = "$vsWhereDir\vswhere.exe"
    
    if (!(Test-Path $vsWhereExe)) {
        if (!(Test-Path $vsWhereDir)) {
            Write-Host "Creating folder $vsWhereDir"
            New-Item -ItemType Directory -Path $vsWhereDir
        }

        Write-Host "Downloading vswhere"
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest "http://github.com/Microsoft/vswhere/releases/download/$vsWhereVersion/vswhere.exe" -OutFile $vswhereExe
    }

    return $vsWhereExe
}

function Get-VSInstalls {

    $vsWhereExe = Get-VSWhere

    $temp = & "$vsWhereExe" -nologo -all -prerelease -format text | select-string "instanceId|installationPath|installationName|catalog_productSemanticVersion"

    [System.Collections.ArrayList]$vsSkus = @()

    for($i = 0; $i -lt $temp.line.Length; $i++) {
        $vsInstall = New-Object -TypeName PSObject
        $vsInstall | Add-Member -MemberType NoteProperty -Name InstanceId -Value $temp.line[$i].split(":", 2)[1].Trim()
        $vsInstall | Add-Member -MemberType NoteProperty -Name InstallationName -Value $temp.line[++$i].split(":", 2)[1].Trim()
        $vsInstall | Add-Member -MemberType NoteProperty -Name InstallationPath -Value $temp.line[++$i].split(":", 2)[1].Trim()
        $vsInstall | Add-Member -MemberType NoteProperty -Name CatalogName -Value $temp.line[++$i].split(":", 2)[1].Trim()

        
        $vsSkus.Add($vsInstall) | Out-Null
    }

    return $vsSkus
}

function ChooseVisualStudioInstallation {
    param ([parameter(mandatory=$true)][AllowEmptyCollection()][collections.arraylist]$options)

    if(!$options) {
        Write-Host "No Visual Studio was found on the machine..." -foreground Red
        return
    }

    Write-Host ""
    Write-Host "Choose an installation..." -foreground Yellow
      
    for ($i = 0; $i -lt $options.Count -and $i -le 9; $i++) {
        if ($i -eq 0) { 
            Write-Host "[$i] $($options[$i].InstallationName)" -foreground Cyan 
        }
        else {
            Write-Host "[$i] $($options[$i].InstallationName)"
        }
    }

    Write-Host "[x] Exit without doing anything" -foreground Red

    do {
        Write-Host "Select Installation (or Enter for " -foreground Yellow -nonewline
        Write-Host "default" -foreground Cyan   -nonewline
        Write-Host "): "    -foreground Yellow -nonewline
        $i = Read-Host
    }
    until ((($i -ge 0) -and ($i -le 9)) -or [string]::IsNullOrEmpty($i) -or $i -eq 'x')

    if ($i -eq 'x') {
        return
    }
    
    if ([string]::IsNullOrEmpty($i)) {
        $match = $options[0] } else {
             $match = $options[$i]
    }

    return $match
}

Export-ModuleMember -Function Start-VsDevShell,Get-VsComnToolsPath