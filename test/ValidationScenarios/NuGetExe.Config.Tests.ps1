[CmdletBinding()]
param (
    [string]$NuGetExe
)

$here = Split-Path -Parent $MyInvocation.MyCommand.Path

. "$here\Common.ps1"

$NuGetConfigContent = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration />
"@

Describe "NuGet.exe Config" {
    $NuGetExe | Should Not BeNullOrEmpty
    $NuGetExe | Should Exist

    It "displays help" {
        $p = nuget config -help
        $p.StdErr | Should BeNullOrEmpty
        $p.ExitCode | Should Be 0
        $p.StdOut | Should Match '^usage:\ NuGet\ config\ <-Set\ name=value\ \|\ name>.+'
    }

    Context "Local Config File" {
        $TestConfig = "$TestDrive\NuGet.Config"

        It "sets new single config value" {
            $p = nuget config -set HTTP_PROXY=http://127.0.0.1 -NonInteractive -ConfigFile $TestConfig
            $p.StdErr | Should BeNullOrEmpty
            $p.ExitCode | Should Be 0
            "TestDrive:\NuGet.Config" | Should Contain '<add\ key="HTTP_PROXY"\ value="http://127\.0\.0\.1"\ />'
        }

        It "updates single config value" {
            nuget config -set KeyA=ValueA -NonInteractive -ConfigFile $TestConfig

            $p = nuget config -set KeyA=ValueB -NonInteractive -ConfigFile $TestConfig
            $p.StdErr | Should BeNullOrEmpty
            $p.ExitCode | Should Be 0
            "TestDrive:\NuGet.Config" | Should Contain '<add\ key="KeyA"\ value="ValueB"\ />'
        }

        It "sets multiple config values" {
            $p = nuget config -set KeyA=ValueA -set KeyB=ValueB -NonInteractive -ConfigFile $TestConfig
            $p.StdErr | Should BeNullOrEmpty
            $p.ExitCode | Should Be 0
            "TestDrive:\NuGet.Config" | Should Contain '<add\ key="KeyA"\ value="ValueA"\ />'
            "TestDrive:\NuGet.Config" | Should Contain '<add\ key="KeyB"\ value="ValueB"\ />'
        }

        It "gets config value for known key" {
            nuget config -set HTTP_PROXY=http://127.0.0.1 -NonInteractive -ConfigFile $TestConfig

            $p = nuget config HTTP_PROXY -NonInteractive -ConfigFile $TestConfig
            $p.StdErr | Should BeNullOrEmpty
            $p.ExitCode | Should Be 0
            $p.StdOut | Should Be "http://127.0.0.1`r`n"
        }

        It "returns the config value as a path" {
            nuget config -set KeyA=ValueA -NonInteractive -ConfigFile $TestConfig

            $p = nuget config KeyA -AsPath -NonInteractive -ConfigFile $TestConfig
            $p.StdErr | Should BeNullOrEmpty
            $p.ExitCode | Should Be 0
            $p.StdOut | Should Be "$TestDrive\ValueA`r`n"
        }

        It "shows error for unknown key" {
            nuget config -set KeyA=ValueA -NonInteractive -ConfigFile $TestConfig

            $p = nuget config KeyB -NonInteractive -ConfigFile $TestConfig
            $p.StdErr | Should Be "Key 'KeyB' not found.`r`n"
            $p.ExitCode | Should Be 0
            $p.StdOut | Should BeNullOrEmpty
        }

        It "removes config value" {
            nuget config -set HTTP_PROXY=http://127.0.0.1 -NonInteractive -ConfigFile $TestConfig
            $p = nuget config HTTP_PROXY -NonInteractive -ConfigFile $TestConfig
            $p.StdOut | Should Be "http://127.0.0.1`r`n"

            $p = nuget config -set HTTP_PROXY= -NonInteractive -ConfigFile $TestConfig
            $p.StdErr | Should BeNullOrEmpty
            $p.ExitCode | Should Be 0
            "TestDrive:\NuGet.Config" | Should Not Contain 'HTTP_PROXY'
        }

        It "fails when file does not exist" {
            $p = nuget config -set HTTP_PROXY=http://127.0.0.1 -NonInteractive -ConfigFile "$TestDrive\DoesNotExist.Config"
            $p.StdErr | Should Match ([regex]::Escape("File '$TestDrive\DoesNotExist.Config' does not exist."))
            $p.ExitCode | Should Be 1
        }

        It "honors detailed verbosity switch" {
            $p = nuget config -set HTTP_PROXY=http://127.0.0.1 -verbosity detailed -NonInteractive -ConfigFile "$TestDrive\DoesNotExist.Config"
            $p.StdErr | Should Match ([regex]::Escape("System.InvalidOperationException: File '$TestDrive\DoesNotExist.Config' does not exist."))
            $p.ExitCode | Should Be 1
        }

        BeforeEach {
            Set-Content $TestConfig -Value $NuGetConfigContent
            Write-Verbose ("BEFORE:`r`n" + ((gc $TestConfig) -join "`r`n"))
        }

        AfterEach {
            Write-Verbose ("AFTER:`r`n" + ((gc $TestConfig) -join "`r`n"))
            Remove-Item $TestConfig -Force
        }
    }

    Context "Global Config File" {
        $GlobalConfigFile = "$env:APPDATA\NuGet\NuGet.Config"
        $BackupConfigFile = "$env:APPDATA\NuGet\$([System.Guid]::NewGuid().ToString())_NuGet.Config"
        Move-Item $GlobalConfigFile $BackupConfigFile

        Set-Content $GlobalConfigFile -Value $NuGetConfigContent
        It "adds, gets, removes config value" {
            $p = nuget config -set HTTP_PROXY=http://127.0.0.1 -NonInteractive
            $p.StdErr | Should BeNullOrEmpty
            $p.ExitCode | Should Be 0
            $GlobalConfigFile | Should Contain '<add\ key="HTTP_PROXY"\ value="http://127\.0\.0\.1"\ />'

            $p = nuget config HTTP_PROXY -NonInteractive
            $p.StdErr | Should BeNullOrEmpty
            $p.ExitCode | Should Be 0
            $p.StdOut | Should Be "http://127.0.0.1`r`n"

            $p = nuget config -set HTTP_PROXY= -NonInteractive
            $p.StdErr | Should BeNullOrEmpty
            $p.ExitCode | Should Be 0
            $GlobalConfigFile | Should Not Contain 'HTTP_PROXY'
        }

        Remove-Item $GlobalConfigFile -Force
        Move-Item $BackupConfigFile $GlobalConfigFile
    }
}
