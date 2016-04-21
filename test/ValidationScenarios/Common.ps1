Function Invoke-NuGetExe {
    $pinfo = New-Object System.Diagnostics.ProcessStartInfo
    $pinfo.FileName = $NuGetExe
    $pinfo.WorkingDirectory = $here
    $pinfo.RedirectStandardError = $true
    $pinfo.RedirectStandardOutput = $true
    $pinfo.UseShellExecute = $false
    $pinfo.Arguments = $Args

    Write-Verbose "$($pinfo.FileName) $($pinfo.Arguments)"

    $p = New-Object System.Diagnostics.Process
    $p.StartInfo = $pinfo
    $p.Start() | Out-Null
    $p.WaitForExit()

    [pscustomobject]@{
        StdOut = $p.StandardOutput.ReadToEnd()
        StdErr = $p.StandardError.ReadToEnd()
        ExitCode = $p.ExitCode
    }
}

Set-Alias -Name nuget -Value Invoke-NuGetExe