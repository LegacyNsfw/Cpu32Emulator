# Journal

# 7/26/2025

I started by creating the requirements and design markdown files in the specs directory, and gave Claude Sonnet 4 this prompt:

> We are going to create the application described in the requirements.md and design.md files. What other aspects of the requirements or design should be added to these documents before we begin?

Claude pointed out some stuff that needed to be specified more clearly, so I added a bunch of stuff to the 'requirements' file - it roughly doubled in size.

I should have added those initial versions to git, but didn't think about version control until later.

> I have updated the requirements and design documents. For anything unspecified, do the obviously right thing if there is an obvious right thing, or ask for clarification if there isn't obvious right thing to do.
> 
> Describe a plan for implementing this application.

And Claude wrote [this plan](claude-plan.md), which looks like it might actually work, if it can in fact do what it thinks it can do. So I asked it to try:

> Implement phase 1.

It took claude three attempts to get the project started with "dotnet new unoapp" and it had to run "dotnet new unoapp -h" once to get help... but the fact that it asked for help and then figured out the problem was kind of impressive. And it got the arguments right on the third try, and the app framework was done.

Then it added the Unicorn package.

Then it wrote all of the code in the "Phase 1" commit, and [this](claude-phase1.md). 

Me: 

> Implement phase 2.

Claude:

> I'll now implement Phase 2: Core Emulation Engine. Let me start by examining the current state and then implementing the Unicorn integration, memory management, and single-step execution functionality.

And that's the Phase 2 commit. 

It makes me nervous to have this much generated code with no testing yet. Because if there are bugs, it's only going to get harder to fix them as Claude adds more code. But the UI doesn't get started until phase 3.

Note to self: for the next vibe project, add some UI right after creating the app skeleton.

Also, Claude created a launch.json file for VS Code that requires GTK, but I'm on Windows, without GTK, so:

> Create a launch.json profile that will build and run the application with Skia and WPF.

So it did.

And then:

> Implement phase 3.