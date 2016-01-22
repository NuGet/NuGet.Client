param (
[Parameter(Mandatory=$true)]
[ValidateSet("15.0", "14.0", "12.0", "11.0", "10.0")]
[string]$VSVersion,
[Parameter(Mandatory=$true)]
$DTEReadyPollFrequencyInSecs,
[Parameter(Mandatory=$true)]
$NumberOfPolls)

. "$PSScriptRoot\VSUtils.ps1"

KillRunningInstancesOfVS

LaunchVS $VSVersion

$dte2 = $null
$count = 0
Write-Host "Will wait for $NumberOfPolls times and $DTEReadyPollFrequencyInSecs seconds each time."

while($count -lt $NumberOfPolls)
{
    # Wait for $VSLaunchWaitTimeInSecs secs for VS to load before getting the DTE COM object
    Write-Host "Waiting for $DTEReadyPollFrequencyInSecs seconds for DTE to become available"
    start-sleep $DTEReadyPollFrequencyInSecs

    $dte2 = GetDTE2 $VSVersion
    if ($dte2)
    {
	    exit 0
    }

    $count++
}

Write-Error "Could not obtain DTE after waiting $NumberOfPolls * $DTEReadyPollFrequencyInSecs = " $NumberOfPolls * $DTEReadyPollFrequencyInSecs " secs"
exit 1