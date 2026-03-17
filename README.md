# Software-Engineering-Projekt

## Code Conventions

names of variables, classes etc must be in english

variable names should be descriptive

* use **PascalCase** for React - Components
* use **camelCase** for other TypeScript functions
* use **PascalCase** for C# functions / methods and Classes

* Backend: use **PascalCase** for file names
* Frontend: use **kebab-case** for file names

## Commit Rules

possible branch names are:

* new feature: feature/\<your-awesome-feature\>
* bug fix: fix/\<annoying-bug\>
* changes that aren't bug fixes nor features: chore/\<boring-task\>

the corresponding PR names are

* feat(\<scope\>): \<cool feature\>
* fix(\<scope\>): \<uncool bug\>
* chore(<scope\>): \<tedious task>

Conversations / Comments in PRs must always be resolved by the author of the Conversation / Comment and **NOT** by the author of the PR

## How to

### Start the Backend Api

open the C# Project folder in a Terminal and run the command
~~~
dotnet run
~~~
or
```
dontnet run {path to project}/Programm.cs
```
note: .Net 10 SDK needs to be installed

alternatively start it with a .Net capable IDE e.g. VS Code with C# extension, Visual Studio, Rider