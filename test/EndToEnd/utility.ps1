function New-Guid {
    [System.Guid]::NewGuid().ToString("d").Substring(0, 4).Replace("-", "")
}

function Get-HostSemanticVersion {
    return [NuGet.Packaging.MinClientVersionUtility]::GetNuGetClientVersion()
}

function Verify-BuildIntegratedMsBuildTask {
    $msBuildTaskPath = [IO.Path]::Combine(${env:ProgramFiles(x86)}, "MsBuild", "Microsoft", "NuGet", "Microsoft.NuGet.targets")

    if (!(Test-Path $msBuildTaskPath)) {
        Write-Warning "Build integrated NuGet target not found at $msBuildTaskPath"
        return $false
    }

    return $true
}

class SkipTest : Attribute {
    SkipTest([string]$Reason) { }
    [bool] ShouldRun() { return $False }
}

class SkipTestForVS14 : Attribute {
    [bool] ShouldRun() {
        return $global:VSVersion -ne '14.0'
    }
}

class SkipTestForVS15 : Attribute {
    [bool] ShouldRun() {
        return $global:VSVersion -ne '15.0'
    }
}

function ShouldRunTest {
    param(
        [System.Management.Automation.CommandInfo]
        $Command
    )
    $SkipTest = $Command.ScriptBlock.Attributes | ?{ $_.TypeID.Name -like 'SkipTest*' }

    return -not $SkipTest -or $SkipTest.ShouldRun()
}