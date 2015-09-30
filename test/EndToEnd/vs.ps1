param([parameter(Mandatory = $true)]
      [string]$OutputPath,
      [parameter(Mandatory = $true)]
      [string]$TemplatePath)

# Make sure we stop on exceptions
$ErrorActionPreference = "Stop"
$FileKind = "{6BB5F8EE-4483-11D3-8BCF-00C04F8EC28C}"

function New-BuildIntegratedProj 
{
    param(
        [string]$ProjectName,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    if ($dte.Version -ge '14.0')
    {
        $SolutionFolder | New-Project BuildIntegratedProj $ProjectName
    }
    else
    {
        throw "SKIP: $($_)"
    }
}

function New-CpsApp 
{
    param(
        [string]$ProjectName,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    if ($dte.Version -ge '14.0')
    {
        $SolutionFolder | New-Project CpsApp $ProjectName
    }
    else
    {
        throw "SKIP: $($_)"
    }
}

function Get-SolutionDir {
    if($dte.Solution -and $dte.Solution.IsOpen) {
        return Split-Path $dte.Solution.FullName
    }
    else {
        throw "Solution not avaliable"
    }
}

function Ensure-Solution {
    if(!$dte.Solution -or !$dte.Solution.IsOpen) {
        New-Solution
    }
}

function Close-Solution 
{
    if ($dte.Solution -and $dte.Solution.IsOpen) 
    {
        $dte.Solution.Close()
    }
}

function Open-Solution 
{
    param
    (
        [string]
        [parameter(Mandatory = $true)]
        $Path
    )
    
    $dte.Solution.Open($Path)
}

function Ensure-Dir {
    param(
        [string]
        [parameter(Mandatory = $true)]
        $Path
    )
    if(!(Test-Path $Path)) {
        mkdir $Path | Out-Null
    }
}

function New-Solution {
    param(
        [string]$solutionName
    )

    if ($solutionName) {
        $name = $solutionName 
    }
    else {
        $id = New-Guid
        $name = "Solution_$id"
    }

    $solutionDir = Join-Path $OutputPath $name
    $solutionPath = Join-Path $solutionDir $name
    
    Ensure-Dir $solutionDir
     
    $dte.Solution.Create($solutionDir, $name) | Out-Null
    $dte.Solution.SaveAs($solutionPath) | Out-Null    
}

function New-Project {
    param(
         [parameter(Mandatory = $true)]
         [string]$TemplateName,
         [string]$ProjectName,
         [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    $id = New-Guid
    if (!$ProjectName) {
        $ProjectName = $TemplateName + "_$id"
    }

    # Make sure there is a solution
    Ensure-Solution
    
	if ($TemplateName -eq 'DNXClassLibrary' -or $TemplateName -eq 'DNXConsoleApp')
	{
		# Get the zip file where the project template is located
		$projectTemplatePath = $TemplateName + '.vstemplate|FrameworkVersion=4.5'
		$lang = 'CSharp/Web'

		# Find the vs template file
		$projectTemplateFilePath = $dte.Solution.GetProjectTemplate($projectTemplatePath, $lang)
	}
	else
	{
		# Get the zip file where the project template is located
		$projectTemplatePath = Join-Path $TemplatePath "$TemplateName.zip"
    
		# Find the vs template file
		$projectTemplateFilePath = @(Get-ChildItem $projectTemplatePath -Filter *.vstemplate)[0].FullName
	} 

    # Get the output path of the project
    if($SolutionFolder) {
        $destPath = Join-Path (Get-SolutionDir) (Join-Path $SolutionFolder.Name $projectName)
    }
    else {
        $destPath = Join-Path (Get-SolutionDir) $projectName
    }
    
    # Store the active window so that we can set focus to it after the command completes
    # When we add a project to VS it usually tries to set focus to some page
    $window = $dte.ActiveWindow
    
    if($SolutionFolder) {
        $SolutionFolder.Object.AddFromTemplate($projectTemplateFilePath, $destPath, $projectName) | Out-Null
    }
    else {
        # Add the project to the solution from th template file specified
        $dte.Solution.AddFromTemplate($projectTemplateFilePath, $destPath, $projectName, $false) | Out-Null
    }
    
    # Close all active documents
    $dte.Documents | %{ try { $_.Close() } catch { } }

    # Change the configuration of the project to x86
    $dte.Solution.SolutionBuild.SolutionConfigurations | % { if ($_.PlatformName -eq 'x86') { $_.Activate() } } | Out-Null

    # Set the focus back on the shell
    $window.SetFocus()

    if ($TemplateName -eq 'JScriptVisualBasicLightSwitchProjectTemplate')
    {
        return
    }

    if ($TemplateName -eq "EmptyWeb")
    {
        # For WebSite project, the project name can be something like "ProjectName(12)".
        # So, use wildcard to search
        $ProjectName = "$ProjectName*"
    }

    # Return the project if it is NOT a LightSwitch project
    for ($counter = 0; $counter -lt 5; $counter++)
    {
        if ($SolutionFolder) {
            $solutionFolderPath = Get-SolutionFolderPathRecursive $SolutionFolder
            $project = Get-Project "$($solutionFolderPath)$projectName" -ErrorAction SilentlyContinue
        }
        else {
            $project = Get-Project $projectName -ErrorAction SilentlyContinue
        }

        if ($project)
        {
            break;
        }

        [System.Threading.Thread]::Sleep(100)
    }
    
    if(!$project) {
        $project = Get-Project "$destPath\"
    }
    
    $project
}

function Get-SolutionFolderPathRecursive([parameter(mandatory=$true)]$solutionFolder) {
    $path = ''
    while ($solutionFolder -ne $null) {
        $path = "$($solutionFolder.Name)\$path"
        $solutionFolder = $solutionFolder.ParentProjectItem.ContainingProject
    }
    return $path
}

function New-SolutionFolder {
    param(
        [string]$Name,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )
    
    $id = New-Guid
    if(!$Name) {
        $Name = "SolutionFolder_$id"
    }
    
    if(!$SolutionFolder) {
        # Make sure there is a solution
        Ensure-Solution

        $solution = Get-Interface $dte.Solution ([EnvDTE80.Solution2])
    }
    elseif($SolutionFolder.Object.AddSolutionFolder) {
        $solution = $SolutionFolder.Object
    }

    $solution.AddSolutionFolder($Name)
}

function New-ClassLibrary {
    param(        
        [string]$ProjectName,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    $SolutionFolder | New-Project ClassLibrary $ProjectName
}

function New-LightSwitchApplication 
{
    param(
        [string]$ProjectName,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    New-Project JScriptVisualBasicLightSwitchProjectTemplate $ProjectName $SolutionFolder
}

function New-PortableLibrary 
{
    param(
        [string]$ProjectName,
        [string]$Profile = $null,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    try
    {
        $project = New-Project PortableClassLibrary $ProjectName $SolutionFolder
    }
    catch {
        # If we're unable to create the project that means we probably don't have some SDK installed
        # Signal to the runner that we want to skip this test        
        throw "SKIP: $($_)"
    }

    if ($Profile) 
    {
        $name = $project.Name
        $project.Properties.Item("TargetFrameworkMoniker").Value = ".NETPortable,Version=v4.0,Profile=$Profile"
        $project = Get-Project -Name $name
    }

    $project
}

function New-BuildIntegratedProj 
{
    param(
        [string]$ProjectName,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    if ($dte.Version -ge '14.0')
    {
        $SolutionFolder | New-Project BuildIntegratedProj $ProjectName
    }
    else
    {
        throw "SKIP: $($_)"
    }
}

function New-JavaScriptApplication 
{
    param(
        [string]$ProjectName,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    try 
    {
        if ($dte.Version -eq '12.0')
        {
            $SolutionFolder | New-Project WinJSBlue $ProjectName
        }
        elseif ($dte.Version -eq '14.0')
        {
            $SolutionFolder | New-Project WinJS_Dev14 $ProjectName
        }
        else 
        {
            $SolutionFolder | New-Project WinJS $ProjectName
        }
    }
    catch {
        # If we're unable to create the project that means we probably don't have some SDK installed
        # Signal to the runner that we want to skip this test        
        throw "SKIP: $($_)"
    }
}

function New-JavaScriptApplication81 
{
    param(
        [string]$ProjectName,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    try 
    {
        $SolutionFolder | New-Project WinJSBlue $ProjectName
    }
    catch {
        # If we're unable to create the project that means we probably don't have some SDK installed
        # Signal to the runner that we want to skip this test        
        throw "SKIP: $($_)"
    }
}

function New-JavaScriptWindowsPhoneApp81 
{
    param(
        [string]$ProjectName,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    try 
    {
        $SolutionFolder | New-Project WindowsPhoneApp81JS $ProjectName
    }
    catch {
        # If we're unable to create the project that means we probably don't have some SDK installed
        # Signal to the runner that we want to skip this test
        throw "SKIP: $($_)"
    }
}

function New-NativeWinStoreApplication
{
    param(
        [string]$ProjectName,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    try
    {
        if ($dte.Version -eq '12.0' -or $dte.Version -eq '14.0')
        {
            $SolutionFolder | New-Project CppWinStoreApplicationBlue $ProjectName
        }
        elseif ($dte.Version -eq '14.0')
        {
            $SolutionFolder | New-Project CppWinStoreApplication_Dev14 $ProjectName
        }
        else 
        {
            $SolutionFolder | New-Project CppWinStoreApplication $ProjectName
        }
    }
    catch {
        # If we're unable to create the project that means we probably don't have some SDK installed
        # Signal to the runner that we want to skip this test        
        throw "SKIP: $($_)"
    }
}

function New-ConsoleApplication {
    param(        
        [string]$ProjectName,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    $SolutionFolder | New-Project ConsoleApplication $ProjectName
}

function New-WebApplication {
    param(        
        [string]$ProjectName,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    $SolutionFolder | New-Project EmptyWebApplicationProject40 $ProjectName
}

function New-VBConsoleApplication {
    param(        
        [string]$ProjectName,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    $SolutionFolder | New-Project VBConsoleApplication $ProjectName
}

function New-MvcApplication { 
    param(        
        [string]$ProjectName,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    $SolutionFolder | New-Project EmptyMvcWebApplicationProjectTemplatev4.0.csaspx $ProjectName
}

function New-MvcWebSite { 
    param(        
        [string]$ProjectName,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    $SolutionFolder | New-Project WebApplication45WebSite $ProjectName
}

function New-WebSite {
    param(        
        [string]$ProjectName,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    $SolutionFolder | New-Project EmptyWeb $ProjectName
}

function New-FSharpLibrary {
    param(        
        [string]$ProjectName,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    $SolutionFolder | New-Project FSharpLibrary $ProjectName
}

function New-FSharpConsoleApplication {
    param(        
        [string]$ProjectName,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    $SolutionFolder | New-Project FSharpConsoleApplication $ProjectName
}

function New-WPFApplication {
    param(        
        [string]$ProjectName,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    $SolutionFolder | New-Project WPFApplication $ProjectName
}

function New-SilverlightClassLibrary {
    param(        
        [string]$ProjectName,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    $SolutionFolder | New-Project SilverlightClassLibrary $ProjectName
}

function New-SilverlightApplication {
    param(        
        [string]$ProjectName,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    $SolutionFolder | New-Project SilverlightProject $ProjectName
}

function New-WindowsPhoneClassLibrary {
    param(        
        [string]$ProjectName,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    try {
        if ($dte.Version -eq '14.0') {
            $SolutionFolder | New-Project WindowsPhoneClassLibrary81 $ProjectName
        }
        else {
            $SolutionFolder | New-Project WindowsPhoneClassLibrary $ProjectName
        }
    }
    catch {
        # If we're unable to create the project that means we probably don't have some SDK installed
        # Signal to the runner that we want to skip this test        
        throw "SKIP: $($_)"
    }
}

function New-DNXClassLibrary
{
    param(
        [string]$ProjectName,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    try 
    {
        $SolutionFolder | New-Project DNXClassLibrary $ProjectName
    }
    catch {
        # If we're unable to create the project that means we probably don't have some SDK installed
        # Signal to the runner that we want to skip this test        
        throw "SKIP: $($_)"
    }
}

function New-DNXConsoleApp
{
    param(
        [string]$ProjectName,
        [parameter(ValueFromPipeline = $true)]$SolutionFolder
    )

    try 
    {
        $SolutionFolder | New-Project DNXConsoleApp $ProjectName
    }
    catch {
        # If we're unable to create the project that means we probably don't have some SDK installed
        # Signal to the runner that we want to skip this test        
        throw "SKIP: $($_)"
    }
}

function New-TextFile {
    $dte.ItemOperations.NewFile('General\Text File')
    $dte.ActiveDocument.Object("TextDocument")
}

function Build-Project {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [string]$Configuration
    )    
    if(!$Configuration) {
        # If no configuration was specified then use
        $Configuration = $dte.Solution.SolutionBuild.ActiveConfiguration.Name
    }
    
    # Build the project and wait for it to complete
    $dte.Solution.SolutionBuild.BuildProject($Configuration, $Project.UniqueName, $true)
}

function Clean-Project {
    # Clean the project and wait for it to complete
    $dte.Solution.SolutionBuild.Clean($true)
}

function Build-Solution {
    # Build and wait for it to complete
    $dte.Solution.SolutionBuild.Build($true)
}

function Get-AssemblyReference {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [parameter(Mandatory = $true)]
        [string]$Reference
    )    
    try {
        return $Project.Object.References.Item($Reference)
    }
    catch {        
    }
    return $null
}

function Get-PropertyValue {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [parameter(Mandatory = $true)]
        [string]$PropertyName
    )    
    try {
        $property = $Project.Properties.Item($PropertyName)        
        if($property) {
            return $property.Value
        }
    }
    catch {        
    }
    return $null
}

function Get-MsBuildPropertyValue {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [parameter(Mandatory = $true)]
        [string]$PropertyName
    )    

    $msBuildProject = Get-MsBuildProject $project
    return $msBuildProject.GetPropertyValue($PropertyName)

    return $null
}

function Get-MsBuildProject 
{
    param(
        [parameter(Mandatory = $true)]
        $project
    )

    $projectCollection = [Microsoft.Build.Evaluation.ProjectCollection]::GlobalProjectCollection

    $loadedProjects = $projectCollection.GetLoadedProjects($project.FullName)
    if ($loadedProjects.Count -gt 0) {
        foreach ($p in $loadedProjects) {
            return $p
        }
    }

    $projectCollection.LoadProject($project.FullName)
}

function Get-ProjectDir {
    param(
        [parameter(Mandatory = $true)]
        $Project
    )

    # c++ project has ProjectDirectory
    $path = Get-PropertyValue $Project 'ProjectDirectory'
    if ($path) 
    {
        return $path
    }

    $path = Get-PropertyValue $Project FullPath
    if ($path)
    {
        if ([System.IO.File]::Exists($path))
        {
            $path = Split-Path $path -Parent
        }
    }

    $path
}

function Get-ProjectName 
{
    param(
        [parameter(Mandatory = $true)]
        $Project
    )
    
    $projectName = $Project.Name

    if ($project.Type -eq 'Web Site' -and $project.Properties.Item("WebSiteType").Value -eq "0") 
    {
        # If this is a WebSite project and WebSiteType = 0, meaning it's configured to use Casini as opposed to IIS Express, 
        # then $Project.Name will return the full path to the website directory. We don't want to use the full path, thus
        # we extract the directory name out of it.

        $projectName = Split-Path -Leaf $projectName
    }
    
    $projectName
}

function Get-OutputPath {
    param(
        [parameter(Mandatory = $true)]
        $Project
    )
    
    $outputPath = $Project.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath").Value
    Join-Path (Get-ProjectDir) $outputPath
}

function Get-ErrorTasks {
    param(
        [parameter(Mandatory = $true)]
        $vsBuildErrorLevel
    )
    $dte.ExecuteCommand("View.ErrorList", " ")
    
    # Make sure there are no errors in the error list
    $errorList = $dte.Windows | ?{ $_.Caption -like 'Error List*' } | Select -First 1
    
    if(!$errorList) {
        throw "Unable to locate the error list"
    }

    # Forcefully show all the error items so that they can be retrieved when they are present
    $errorList.Object.ShowErrors = $True
    $errorList.Object.ShowWarnings = $True
    $errorList.Object.ShowMessages = $True
    
    # Get the list of errors from the error list window which contains errors, warnings and info
    $allItemsInErrorListWindow = $errorList.Object.ErrorItems

    $errorTasks = @()
    for($i=0; $i -lt $allItemsInErrorListWindow.Count; $i++)
    {
        $currentErrorLevel = [EnvDTE80.vsBuildErrorLevel]($allItemsInErrorListWindow[$i].ErrorLevel)
        if($currentErrorLevel -eq $vsBuildErrorLevel)
        {
            $errorTasks += $allItemsInErrorListWindow[$i]
        }
    }

    # Force return array. Arrays are zero-based
    return ,$errorTasks
}

function Get-Errors {
    $vsBuildErrorLevelHigh = [EnvDTE80.vsBuildErrorLevel]::vsBuildErrorLevelHigh
    $errors = Get-ErrorTasks $vsBuildErrorLevelHigh

    # Force return array. Arrays are zero-based
    return ,$errors
}

function Get-Warnings {
    $vsBuildErrorLevelMedium = [EnvDTE80.vsBuildErrorLevel]::vsBuildErrorLevelMedium
    $warnings = Get-ErrorTasks $vsBuildErrorLevelMedium

    # Force return array. Arrays are zero-based
    return ,$warnings
}

function Get-ProjectItemPath {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true)]
        [string]$Path
    )
    $item = Get-ProjectItem $Project $Path
    
    if($item) {
        return $item.Properties.Item("FullPath").Value
    }
}

function Remove-ProjectItem {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true)]
        [string]$Path
    )

    $item = Get-ProjectItem $Project $Path
    $path = Get-ProjectItemPath $Project $Path
    $item.Remove()
    Remove-Item $path
}

function Get-ProjectItem {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true)]
        [string]$Path
    )
    Process {
        $pathParts = $Path.Split('\')
        $projectItems = $Project.ProjectItems
        
        foreach($part in $pathParts) {
            if(!$part -or $part -eq '') {
                continue
            }
            
            try {
                $subItem = $projectItems.Item($part)
            }
            catch {
                return $null
            }

            $projectItems = $subItem.ProjectItems
        }

        if($subItem.Kind -eq $FileKind) {
            return $subItem
        }
        
        # Force array
       return  ,$projectItems
    }
}

function Add-File {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true)]
        [string]$FilePath
    )
    
    $Project.ProjectItems.AddFromFileCopy($FilePath) | out-null
}

function Add-ProjectReference {
    param (
        [parameter(Mandatory = $true)]
        $ProjectFrom,
        [parameter(Mandatory = $true)]
        $ProjectTo
    )

    if($ProjectFrom.Object.References.AddProject) {
        $ProjectFrom.Object.References.AddProject($ProjectTo) | Out-Null
    }
    elseif($ProjectFrom.Object.References.AddFromProject) {
        $ProjectFrom.Object.References.AddFromProject($ProjectTo) | Out-Null
    }
}

function Remove-Project {
    param (
        [parameter(Mandatory = $true)]
        $ProjectName
    )

    [NuGetConsole.Host.PowerShell.Implementation.ProjectExtensions]::RemoveProject($ProjectName)
}

function Get-SolutionPath {
    $dte.Solution.Properties.Item("Path").Value
}

function Close-Solution {
    if ($dte.Solution) {
        $dte.Solution.Close()
    }
}

function Enable-PackageRestore {
    if (!$dte.Solution -or !$dte.Solution.IsOpen) 
    {
        throw "No solution is available."
    }

    $componentService = Get-VSComponentModel
    
    # change active package source to "All"
    $packageSourceProvider = $componentService.GetService([NuGet.VisualStudio.IVsPackageSourceProvider])
    $packageSourceProvider.ActivePackageSource = [NuGet.VisualStudio.AggregatePackageSource]::Instance
    
    $packageRestoreManager = $componentService.GetService([NuGet.VisualStudio.IPackageRestoreManager])
    $packageRestoreManager.EnableCurrentSolutionForRestore($false)
}

function Check-NuGetConfig {
    # Create an empty NuGet.Config file if not exist. It will happen on machines with visual studio newly installed.
    $nuGetConfigPath = Join-Path $env:AppData 'NuGet\NuGet.Config'
    if (!(Test-Path $nuGetConfigPath))
    {
        $newFile = New-Item $nuGetConfigPath -ItemType File
        '<?xml version="1.0" encoding="utf-8"?>
        <configuration>
        </configuration>' > $newFile
    }
}