# Journal

# 7/26/2025

I started by creating the requirements and design markdown files in the specs directory, and gave Claude Sonnet 4 this prompt:

> We are going to create the application described in the requirements.md and design.md files. What other aspects of the requirements or design should be added to these documents before we begin?

Claude pointed out some stuff that needed to be specified more clearly, so I added a bunch of stuff to the 'requirements' file - it roughly doubled in size.

I should have added those initial versions to git, but didn't think about version control until later.

> I have updated the requirements and design documents. For anything unspecified, do the obviously right thing if there is an obvious right thing, or ask for clarification if there isn't obvious right thing to do.
> 
> Describe a plan for implementing this application.

And Claude wrote [this](claude-plan.md).

> Implement phase 1.

It took claude three tries to get the "dotnet new unoapp" syntax right, and it had to run "dotnet new unoapp -h" to get help... but it got it right on the third try.

Then it added the Unicorn package.

Then it wrote all of the code in the "Phase 1" commit, and [this](claude-phase1.md).

Me: 

> Implement phase 2.

Claude:

> I'll now implement Phase 2: Core Emulation Engine. Let me start by examining the current state and then implementing the Unicorn integration, memory management, and single-step execution functionality.

