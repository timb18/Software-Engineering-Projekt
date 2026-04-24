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

open the C# project directory in a terminal and run the command (note: .Net 10 SDK needs to be installed)

~~~ cmd
dotnet run
~~~

or

~~~ cmd
dontnet run {path-to-project}/Programm.cs
~~~

alternatively start it with a .Net capable IDE e.g. VS Code with C# extension, Visual Studio, Rider

### Start the Web UI

Open the React project directory in a terminal and run the command (note: node needs to be installed)

~~~ cmd
cd Software-Engineering-Projekt/Frontend/TeaPotUi && npm install
cd Software-Engineering-Projekt/Frontend/TeaPotUi && npm run dev
~~~

afterwards enter the url shown in the terminal into a browser
