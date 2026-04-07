# Removes UTF-8 BOM from localized files produced by the XLoc tool.
# Used via the xLocCustomPowerShellScript input in the OneLocBuild task.
# The XLocFileList environment variable is set by the task and contains
# the path to a text file listing all localized files to process.

function RemoveUtf8Bom
{
    param(
        [string]$FilePath
    )

    $encoding = New-Object -TypeName System.Text.UTF8Encoding -ArgumentList $false
    $content = Get-Content -Path $FilePath -Encoding UTF8
    [System.IO.File]::WriteAllText($FilePath, $content -join "`n", $encoding)
}

foreach ($locFile in (Get-Content -Path $env:XLocFileList))
{
    RemoveUtf8Bom -FilePath $locFile
}
