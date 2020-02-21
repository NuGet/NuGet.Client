Write-Host "Regenerating all generated files from all .tt files."
$ttFiles = Get-ChildItem -Filter *.tt -Recurse -File -Name
foreach ($ttFile in $ttFiles)
{
    if (-not $ttFile.endswith("AssemblySourceFileGenerator.tt"))
    {
        Write-Host t4.exe $ttFile
        t4.exe "$ttFile"
    }
}