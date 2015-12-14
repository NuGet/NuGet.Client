Function Trace-Log($TraceMessage){
    Write-Host "[$(Trace-Time)]`t$TraceMessage" -ForegroundColor Cyan
}

Function Trace-Time()
{
    $prev = $Global:LastTraceTime;
    $currentTime = Get-Date;
    $time = $currentTime.ToString("HH:mm:ss");
    $diff = New-TimeSpan -Start $prev -End $currentTime
    $Global:LastTraceTime = $currentTime;
    "$time +$([math]::Round($diff.TotalSeconds, 0))"
}

$Global:LastTraceTime = Get-Date