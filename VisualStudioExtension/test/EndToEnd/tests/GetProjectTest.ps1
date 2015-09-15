function Test-ProjectNameReturnsUniqueName {
     # Arrange
     $f = New-SolutionFolder 'Folder1'
     $p1 = $f | New-ClassLibrary 'ProjectA'
     $p3 = $f | New-WebApplication 'ProjectB'

     $p2 = New-ConsoleApplication 'ProjectA'

     # Act
     $projectUniqueNames = @(Get-Project -All | Select-Object -ExpandProperty ProjectName | Sort-Object)

     # Assert
     Assert-True ($projectUniqueNames.Count -eq 3)
     Assert-AreEqual 'Folder1\ProjectA' $projectUniqueNames[0]
     Assert-AreEqual 'Folder1\ProjectB' $projectUniqueNames[1]
     Assert-AreEqual 'ProjectA' $projectUniqueNames[2]
}

function Test-DefaultProjectIsCorrectWhenProjectsAreAdded {
    # Act
    $f1 = New-SolutionFolder 'Folder1'
    $p1 = $f1 | New-ClassLibrary 'ProjectA'

    # Assert
    Assert-DefaultProject $p1

    # Act
    $p2 = New-ClassLibrary 'Projecta'
    Assert-DefaultProject $p1
}

function Test-DefaultProjectIsCorrectWhenProjectsAreAddedInReverseOrder {
    # Act
    $p1 = New-ClassLibrary 'Projecta'    

    # Assert
    Assert-DefaultProject $p1

    # Act
    $f1 = New-SolutionFolder 'Folder1'
    $p2 = $f1 | New-ClassLibrary 'ProjectA'
    Assert-DefaultProject $p1
}

function Test-GetProjectThrowsIfProjectNameAmbiguous {
    # Act
    $f1 = New-SolutionFolder 'foo'
    $f2 = New-SolutionFolder 'bar'
    $p1 = $f1 | New-ClassLibrary 'A'
    $p2 = $f2 | New-ClassLibrary 'A'

    # Assert
    Assert-Throws { Get-Project A } "Project 'A' is not found."
    Assert-AreEqual $p1 (Get-Project foo\A)
    Assert-AreEqual $p2 (Get-Project bar\A)
}

function Test-GetProjectCommandWithWildCardsWorksWithProjectHavingTheSameName {
    #
    #  Folder1
    #     + ProjectA
    #     + ProjectB
    #  Folder2
    #     + ProjectA
    #     + ProjectC
    #  ProjectA
    #

    # Arrange
    $f = New-SolutionFolder 'Folder1'
    $p1 = $f | New-ClassLibrary 'ProjectA'
    $p2 = $f | New-ClassLibrary 'ProjectB'

    $g = New-SolutionFolder 'Folder2'
    $p3 = $g | New-ClassLibrary 'ProjectA'
    $p4 = $g | New-ConsoleApplication 'ProjectC'

    $p5 = New-ConsoleApplication 'ProjectA'

    # Assert
    Assert-AreEqual $p1 (Get-Project 'Folder1\ProjectA')
    Assert-AreEqual $p2 (Get-Project 'Folder1\ProjectB')
    Assert-AreEqual $p2 (Get-Project 'ProjectB')
    Assert-AreEqual $p3 (Get-Project 'Folder2\ProjectA')
    Assert-AreEqual $p4 (Get-Project 'Folder2\ProjectC')
    Assert-AreEqual $p4 (Get-Project 'ProjectC')
    Assert-AreEqual $p5 (Get-Project 'ProjectA')

    $s1 = (Get-Project 'Folder1' -ea SilentlyContinue)
    Assert-Null $s1

    $s2 = (Get-Project 'Folder2' -ea SilentlyContinue)
    Assert-Null $s2

    $fs = @( Get-Project 'Folder1*' )
    Assert-AreEqual 2 $fs.Count
    Assert-AreEqual $p1 $fs[0]
    Assert-AreEqual $p2 $fs[1]

    $gs = @( Get-Project '*ProjectA*' )
    Assert-AreEqual 3 $gs.Count
    Assert-AreEqual $p1 $gs[0]
    Assert-AreEqual $p3 $gs[1]
    Assert-AreEqual $p5 $gs[2]
}

function Test-SimpleNameDoesNotWorkWhenAllProjectsAreNested {
    # Arrange
    $f = New-SolutionFolder 'Folder1'
    $p1 = $f | New-ClassLibrary 'ProjectA'

    $g = New-SolutionFolder 'Folder2'
    $p2 = $g | New-ClassLibrary 'ProjectA'

    # Assert
    Assert-Throws { (Get-Project -Name 'ProjectA') } "Project 'ProjectA' is not found."
}

function Test-RemovingAmbiguousProjectAllowsSimpleNameToBeUsed {
    # Act
    $f1 = New-SolutionFolder 'foo'
    $p1 = New-ClassLibrary 'A'
    $p2 = $f1 | New-ClassLibrary 'A'
    

    Assert-AreEqual $p2 (Get-Project -Name foo\A)
    Assert-AreEqual $p1 (Get-Project -Name A)

    Remove-Project $p1.Name

    Assert-AreEqual $p2 (Get-Project -Name foo\A)
    Assert-AreEqual $p2 (Get-Project -Name A)
}

function Test-RenameCreatingAmbiguityFollowedByRemovalAllowsSimpleNameToBeUsed {
    # Act
    $f1 = New-SolutionFolder 'foo'
    $p1 = New-ClassLibrary 'A'
    $p2 = $f1 | New-ClassLibrary 'B'
    

    Assert-AreEqual $p2 (Get-Project -Name foo\B)
    Assert-AreEqual $p1 (Get-Project -Name A)

    $p2.Name =  'A'

    Assert-AreEqual $p2 (Get-Project -Name foo\A)
    Assert-AreEqual $p1 (Get-Project -Name A)

    Remove-Project $p1.Name

    Assert-AreEqual $p2 (Get-Project -Name foo\A)
    Assert-AreEqual $p2 (Get-Project -Name A)
}

function Test-RenamingSolutionFolderDoesNotAffectGetProject {
    # Act
    $f1 = New-SolutionFolder 'foo'
    $p1 = New-ClassLibrary 'A'
    $p2 = $f1 | New-ClassLibrary 'B'
    

    Assert-AreEqual $p2 (Get-Project -Name foo\B)
    Assert-AreEqual $p1 (Get-Project -Name A)

    $p2.Name =  'A'

    Assert-AreEqual $p2 (Get-Project -Name foo\A)
    Assert-AreEqual $p1 (Get-Project -Name A)

    $f1.Name = 'bar'
    
    Assert-AreEqual $p2 (Get-Project -Name bar\A)
    Assert-AreEqual $p1 (Get-Project -Name A)
    
    Remove-Project $p1.Name
    Assert-AreEqual $p2 (Get-Project -Name bar\A)
    Assert-AreEqual $p2 (Get-Project -Name A)
}

function Test-RenamingSolutionFolderWithDeeplyNestedProjectsDoesNotAffectGetProject {
    # Act
    $f1 = New-SolutionFolder 'foo'
    $f2 = $f1 | New-SolutionFolder 'bar'
    $f3 = $f1 | New-SolutionFolder 'empty'
    
    $p1 = New-ClassLibrary 'A'
    $p2 = $f2 | New-ClassLibrary 'B'
    
    Add-File $f1 "$($context.RepositoryRoot)\coolpackage.nuspec"
    Add-File $f2 "$($context.RepositoryRoot)\secondpackage.nuspec"
    
    
    Assert-AreEqual $p2 (Get-Project -Name foo\bar\B)
    Assert-AreEqual $p1 (Get-Project -Name A)
    
    $p2.Name =  'A'
    
    Assert-AreEqual $p2 (Get-Project -Name foo\bar\A)
    Assert-AreEqual $p1 (Get-Project -Name A)
    
    $f1.Name = 'bar'
    
    Assert-AreEqual $p2 (Get-Project -Name bar\bar\A)
    Assert-AreEqual $p1 (Get-Project -Name A)
    
    Remove-Project $p1.Name
    Assert-AreEqual $p2 (Get-Project -Name bar\bar\A)
    Assert-AreEqual $p2 (Get-Project -Name A)
}

function Test-AmbiguousStartupProject {
    # Arrange
    $f = New-SolutionFolder foo
    $p1 = $f | New-ClassLibrary A
    $p2 = New-ClassLibrary A

    # Make sure the default project is p1
    Assert-DefaultProject $p1

    $path = Get-SolutionPath
    $p1.Save()
    $p2.Save()
    $dte.Solution.SaveAs($path)
    $dte.Solution.Close()

    # Re open the solution
    $dte.Solution.Open($path)
    $p1 = Get-Project foo\A
    $p2 = Get-Project A

    # Make sure the default project is p1
    Assert-DefaultProject $p1
}

function Test-GetProjectAfterDefaultProjectRemoved
{
	param($context)

    # Arrange
    $p1 = New-ClassLibrary
	$p2 = New-ClassLibrary

	#Act
	Remove-Project $p1.Name

    # Assert
	Assert-DefaultProject $p2
}

function Test-GetProjectForDNXClassLibrary
{
	param($context)

	if ($dte.Version -eq '14.0') {
		# Arrange
		$p1 = New-DNXClassLibrary

		#Act
		$name = @(Get-Project)

		# Assert
		Assert-NotNull $name
	}
}

function Assert-DefaultProject($p) {
    Assert-AreEqual $p (Get-Project) "Default project is actually $($p.UniqueName)"
}