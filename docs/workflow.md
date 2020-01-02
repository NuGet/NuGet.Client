# Workflow

In here we describe the general workflow guidelines the NuGet developer/contributor 

## Basics

### Code reviews

To help ensure that only the highest quality code makes its way into the project, all code changes need to be submitted to GitHub as PRs. 

In general a PR should be approved by the Subject Matter Expert (SME) of that code. For example, a change to the Banana project should be signed off by `@MrMonkey`, and not by `@MrsGiraffe`. If you don't know the SME, someone on the team will help you identify them. Of course, sometimes it's the SME who is making a change, in which case a secondary person will have to sign off on the change (e.g. `@JuniorMonkey`).

To commit the PR to the repo use the GitHub `Squash and Merge` button. We can't stress this enough. Always use `Squash and Merge` unless an exception is explicitly stated in this document. 

- *Do* favor having more than 1 reviewer.
- *Do not* merge too quickly. Wait for at least 24h after the last significant changes before merging unless the change is urgent. 
- *Do* address all feedback. Not necessarily by accepting it, but by reaching a resolution with the reviewer. All comments need to be marked as resolved before merging. 
- *Do* use GitHub's tooling. Re-request review after all feedback has been addressed.

### Branching strategy

Talk about branch strategy, feature branches, release branches etc.
The active development branch in our repo is `dev`. What we ship comes from the `release-majorminorx` branches.

We use trunk based development model. See https://trunkbaseddevelopment.com/youre-doing-it-wrong/ and https://rollout.io/blog/trunk-based-development-what-why/

### Solution and project folder structure and naming

The NuGet.Client repo currently has only one solution file named `NuGet.sln`. We do not want/need to have more than one solution file. 
If deemed necessary by the team, we can consider solution filters at a future point. 

Every project in the NuGet.Client repo should be PackageReference based and if possible (read this as not .NET Framework WPF), an SDK based one. 
The production source code is under the `src` folder.
The test source code is under the `test` folder.
The files affecting build are located under the `build` folder.
The powershell files that are not part of the `Package Manager Console` are located under the `scripts` folder.

Follow the existing pattern for new project files (for example, if NuGet.Commands imports common.props at some point, so should NuGet.MyNewProject). 
Test projects have a different convention for the build customization files they import, so be mindful of that.

### Project naming pattern

The general naming pattern is `NuGet.<area>.<subarea>`. All NuGet assemblies ship together and follow the same assembly versioning, save for some exceptions like `NuGet.VisualStudio.Interop`. 
All assemblies have the same name as their project.
All package versions are the same. No exceptions.