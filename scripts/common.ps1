
function ReadPropertiesFile {
    Param(
        [Parameter(Mandatory=$True)]
        [string]$Path
    )
    Write-Verbose "Reading properties file '$Path'"
    $properties = @{}
    Get-Content $Path |
        ? { -not $_.StartsWith('#') } |
        % { $_.Trim() } |
        ? { $_ -ne '' } |
        % { $tokens = $_ -split "\s*=\s*"; $properties.Add($tokens[0], $tokens[1]) }
    $properties
}

function ReplaceTextInFiles {
    [CmdletBinding(SupportsShouldProcess=$True)]
    Param(
        [Parameter(ValueFromPipeline=$True, Mandatory=$True, Position=0)]
        [string[]]$Files,
        [Parameter(Mandatory=$True)]
        [Alias('old')]
        [string]$OldText,
        [Parameter(Mandatory=$True)]
        [Alias('new')]
        [string]$NewText
    )
    Process {
        $Files |
            ?{ (Get-Content $_ | Out-String) -match $OldText } |
            ?{ $pscmdlet.ShouldProcess($_, 'replace text') } |
            %{
                $updated = (Get-Content $_) | %{
                    $_.Replace($OldText, $NewText)
                }

                Set-Content $_ $updated | Out-Null
            }
    }
}

Function New-TempDir {
    $TempDir = Join-Path $env:TEMP "nuget_$([System.Guid]::NewGuid())"
    New-Item -ItemType Directory -Force -Path $TempDir | Out-Null
    $TempDir
}

Function New-ZipArchive {
    param(
        [string]$SourceDirectory,
        [string]$ZipFile
    )
    Add-Type -Assembly 'System.IO.Compression.FileSystem'
    [System.IO.Compression.ZipFile]::CreateFromDirectory($SourceDirectory, $ZipFile, 'Optimal', $False)
}