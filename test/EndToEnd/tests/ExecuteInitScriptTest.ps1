function Test-NetCoreProjectExecuteInitScriptOnInstall {
    param($context)

    Remove-Variable PackageInitPS1Var -Scope Global -Force -ErrorAction Ignore

    $componentModel = Get-VSComponentModel
    $restoreEvents = $componentModel.GetService([NuGet.VisualStudio.IRestoreEvents])

    $restoreEvent = $null

    Get-Event | Remove-Event
    Register-ObjectEvent -InputObject $restoreEvents -EventName SolutionRestoreCompleted -SourceIdentifier RestoreEventSource

    Try {
        $p = New-NetCoreConsoleApp

        # Wait for initial restore
        $restoreEvent = Wait-Event -SourceIdentifier RestoreEventSource -TimeoutSec 20
        Assert-NotNull $restoreEvent
        Assert-AreEqual 'Succeeded' $restoreEvent.SourceEventArgs.RestoreStatus

        # Act
        $p | Install-Package PackageInitPS1

        $restoreEvent = Wait-Event -SourceIdentifier RestoreEventSource -TimeoutSec 20
        Assert-NotNull $restoreEvent
        Assert-AreEqual 'Succeeded' $restoreEvent.SourceEventArgs.RestoreStatus
        Assert-AreEqual 1 $global:PackageInitPS1Var
    }
    Finally {
        Unregister-Event RestoreEventSource
    }
}

function Test-NetCoreProjectExecuteInitScriptOnlyOnce {
    param($context)

    Remove-Variable PackageInitPS1Var -Scope Global -Force -ErrorAction Ignore

    $componentModel = Get-VSComponentModel
    $restoreEvents = $componentModel.GetService([NuGet.VisualStudio.IRestoreEvents])

    $restoreEvent = $null

    Get-Event | Remove-Event
    Register-ObjectEvent -InputObject $restoreEvents -EventName SolutionRestoreCompleted -SourceIdentifier RestoreEventSource

    Try {
        $p = New-NetCoreConsoleApp

        # Wait for initial restore
        Wait-Event -SourceIdentifier RestoreEventSource -TimeoutSec 20

        $p | Install-Package PackageInitPS1
        Wait-Event -SourceIdentifier RestoreEventSource -TimeoutSec 20

        # Act
        $p | Install-Package NuGet.Versioning -Version 3.5.0

        $restoreEvent = Wait-Event -SourceIdentifier RestoreEventSource -TimeoutSec 20
        Assert-NotNull $restoreEvent
        Assert-AreEqual 'Succeeded' $restoreEvent.SourceEventArgs.RestoreStatus
        Assert-AreEqual 1 $global:PackageInitPS1Var
    }
    Finally {
        Unregister-Event RestoreEventSource
    }
}

function Test-NetCoreProjectExecuteInitScriptAfterReopen {
    param($context)

    $componentModel = Get-VSComponentModel
    $restoreEvents = $componentModel.GetService([NuGet.VisualStudio.IRestoreEvents])

    $restoreEvent = $null

    Get-Event | Remove-Event
    Register-ObjectEvent -InputObject $restoreEvents -EventName SolutionRestoreCompleted -SourceIdentifier RestoreEventSource

    Try {
        $p = New-NetCoreConsoleApp

        # Wait for initial restore
        Wait-Event -SourceIdentifier RestoreEventSource -TimeoutSec 20

        $p | Install-Package PackageInitPS1
        Wait-Event -SourceIdentifier RestoreEventSource -TimeoutSec 20

        $solutionFile = Get-SolutionFullName
        SaveAs-Solution $solutionFile
        Close-Solution

        Remove-Variable PackageInitPS1Var -Scope Global -Force -ErrorAction Ignore

        # Reset script execution cache
        $p2 = New-NetCoreConsoleApp
        $p2 | Install-Package NuGet.Versioning -Version 3.5.0
        Wait-Event -SourceIdentifier RestoreEventSource -TimeoutSec 20
        Close-Solution

        # Act
        Open-Solution $solutionFile
        Wait-ForSolutionLoad

        $restoreEvent = Wait-Event -SourceIdentifier RestoreEventSource -TimeoutSec 20
        Assert-NotNull $restoreEvent
        Assert-AreEqual 'Succeeded' $restoreEvent.SourceEventArgs.RestoreStatus
        Assert-AreEqual 1 $global:PackageInitPS1Var
    }
    Finally {
        Unregister-Event RestoreEventSource
    }
}

function Test-NetCoreProjectExecuteInitScriptOnSolutionRestore {
    param($context)

    $componentModel = Get-VSComponentModel
    $restoreEvents = $componentModel.GetService([NuGet.VisualStudio.IRestoreEvents])

    $restoreEvent = $null

    Get-Event | Remove-Event
    Register-ObjectEvent -InputObject $restoreEvents -EventName SolutionRestoreCompleted -SourceIdentifier RestoreEventSource

    Try {
        $p = New-NetCoreConsoleApp

        # Wait for initial restore
        Wait-Event -SourceIdentifier RestoreEventSource -TimeoutSec 20

        $p | Install-Package PackageInitPS1
        Wait-Event -SourceIdentifier RestoreEventSource -TimeoutSec 20

        Remove-Variable PackageInitPS1Var -Scope Global -Force -ErrorAction Ignore

        # Act
        #Invoke-ShellCommand 'ProjectandSolutionContextMenus.Solution.RestoreNuGetPackages'
        Build-Solution

        $restoreEvent = Wait-Event -SourceIdentifier RestoreEventSource -TimeoutSec 20
        Assert-NotNull $restoreEvent
        Assert-AreEqual 'Succeeded' $restoreEvent.SourceEventArgs.RestoreStatus
        Assert-Null $global:PackageInitPS1Var
    }
    Finally {
        Unregister-Event RestoreEventSource
    }
}