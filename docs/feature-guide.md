
# NuGet.Client engineering feature guide

## Overview

Most NuGet contributors at some point will work on a feature. Given NuGet's standing in the .NET eco-system, the features are frequently far reaching. In many cases, a feature would involve cooperation with other tooling, thus interacting with other teams, etc.

It is *very important* to clearly communicate the expectations & changes to the current engineers, the partners, the future engineers and most importantly, the customers.
That is done by designs or specs that focus on experience or technical aspects, as applicable.

This guide covers the engineering side of the feature story. From the initial technical design, to quantifying the success of the change.

## How do I know if I need a design for my work

* If your work is a new functionality, it needs a design.
* If your work is changing a current feature (DCR), it needs a design.
* If the scope of your work is very large, for example, an extensive, complex refactoring, it needs a design.

For example: new commands, new command options, new additions to NuGet.Config, UI changes in Visual Studio, things many might consider a significant behavior changes, etc.

If you are unsure whether your work requires a design, engage with the leads, PMs and more senior members of the NuGet.Client team.

## Where do I begin

This guide does not cover the typical work that PMs do.
The PMs are responsible for doing customer interviews, surveys, partner sync-ups, etc. Often times an engineer will work with a PM to help design a feature. Other times, there's a NuGet part of a bigger feature originating from .NET CLI/runtime, or Visual Studio itself.
What is covered here is the process once the ask lands on the plate of a NuGet contributor.

## Feature life-cycle

For completeness, the feature life-cycle can be divided in a few phases. Note that often times, certain phases might overlap to an extent. NuGet follows the [agile methodology](https://agilemanifesto.org/), so we respond to new information and reevaluate earlier decisions when appropriate.
Given that the guide is written with engineers in mind, we will not elaborate on the first phase.

1. Problem statement
2. Initial design
3. Design review
4. Implementation
5. Shipping and adoption

### Problem statement

The NuGet teams receives customer asks through many different feedback mechanisms, such as GitHub and Developer Community. Additionally, the PMs do customer studies, surveys and interviews.
A lot of effort is put in to formalize a problem statement, but as previously stated, we will not cover it here.

### Initial design

This is usually the latest stage at which a NuGet.Client contributor gets involved. Depending on where the problem statement was formulated, this might involve discussions in issues, meetings, discussions with partners. This phase usually involves a more limited group of people.
While all final designs are required to be `markdown`, in this phase you might be dealing with other document types.
The recommendation is to transition `markdown` as early as possible.

### Design review

As an open source project, NuGet does the large majority of it's work in public GitHub repos. All of our product code is public.
Always follow the [Design Review Guide](design-review-guide.md).
As the design evolves, ensure you expand the audience as necessary. Review it with the NuGet team first, then with partners and customers as necessary.

When a design is finalized, it is merged in the Home repo.

### Implementation

By the time the implementation phase comes along, it's important that many of the major design questions are addressed.
No design is ever perfect and sometimes during the implementation phase we discover new restrictions or a better approach.
*Do* ensure the design is updated if any changes happen during the implementation phase.
Any new changes should also follow the [Design Review Guide](design-review-guide.md).

### Shipping and adoption

The work does not end after the implementation is completed.
Technical designs are great and necessary, but no feature is complete without user docs, that live in [docs.microsoft.com-nuget](https://github.com/nuget/docs.microsoft.com-nuget/).
Always be on the lookout for early feedback during the preview releases and especially the first stable release that contains the change. For large features that sometimes shipp in phases, it's even more important to communicate expectations.

Lastly, ensure user engagement, adoption and related success metrics can be tracked.

## NuGet product areas

The NuGet.Client ships in various Microsoft products, NuGet.exe, dotnet.exe & Visual Studio.

Within those products there are many different functionalities available, including but not limited to, restore, pack, Package Manager UI & Package Manager Console. Every product and functionality has their own considerations. It is a complex product. To help us ship the highest quality work, we have a set of considerations.
These considerations are not perfect, they will not include every consideration, but they are a good start.

You can copy use these considerations in the epic issues, in the design documents, and everywhere you deem appropriate.
These considerations are also useful for smaller changes that might not require a design.

### NuGet feature considerations

* Design document
  * Reviewed by the Engineering & PM
  * Reviewed by affected partners
  * Reviewed by the community
* Partner dependencies considered
  * Partner to NuGet asks
  * NuGet to partner asks
* Implementation considerations
  * Accessibility considerations
  * Performance considerations
  * Security considerations
  * World readiness considerations
* User documentation

#### Restore considerations

* NuGet tool parity, ensure all products work as expected
  * Visual Studio
    * Nomination updates if necessary
  * nuget.exe
  * dotnet.exe
  * MSBuild.exe
* Are any restore output consumers affected, [sdk](https://github.com/dotnet/sdk), [project-system](https://github.com/dotnet/project-system), [NuGet.BuildTasks](https://github.com/dotnet/nuget.buildtasks)?
* Is the lock file affected?
* Backward compatibility considerations, what happens when older tools try to use this feature. Is the error experience satisfactory?
* Performance considerations
  * How is incremental restore affected?
  * How is full restore affected?

#### Pack considerations

* How are the NuGet tools affected?
  * NuGet.exe pack
  * Targets pack (dotnet.exe, MSBuild.exe)
  * Visual Studio pack
* Is traditional nuspec pack affected?
* Is this a schema change?
  * What happens when older tools try to read the nuspec?
* Are NuGet feeds affected? Any action required from NuGet.org or services that provide private feeds?

#### Visual Studio UI considerations

* Ensure screen-readers handles the change correctly
* Validate all themes
* More complex changes should be reviewed by UX experts

#### CLI (NuGet.exe & dotnet.exe) considerations

* Is the command available in both tools? Should it be?
* Is the equivalent functionality available in Visual Studio? Should it be?
* If dotnet.exe is affected, often times there is dotnet.exe side work, both on design and implementation side.
* The dotnet.exe user documentation is separate from the NuGet user documentation. Ensure it is properly updated when necessary.
