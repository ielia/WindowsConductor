# CLAUDE.md

## Overview
WindowsConductor client-server solution for controlling Windows 10/11 desktop
applications remotely through a websocket connection, targetting native Windows elements and interacting with them.

## Tech Stack
* .NET 8
* FlaUI.UIA3

## Solution Structure
* `WindowsConductor.Client`: Client-side. Code-based API to connect with the driver (server-side) in charge of
    executing commands.
* `WindowsConductor.Client.Tests`: Client-side project unit and integration tests.
* `WindowsConductor.DriverFlaUI`: Server-side. WebSockets endpoint and JSON-based API.
    Command translation to FlaUI.UIA3 implementation.
* `WindowsConductor.DriverFlaUI.Tests`: Server-side project unit tests.

## Commands
* Build: `dotnet build`
* Test: `dotnet test`
* Run driver: `dotnet run --project WindowsConductor.DriverFlaUI`

## C# Coding Standards
* Use modern C# features: primary constructors, file-scoped namespaces, record types, and pattern matching.
* Enable nullable reference types and treat warnings as errors.
* Prefer immutability by default using records and `readonly` structs.
* Use `async` and `await` for all I/O operations; never call `.Result` or `.Wait()` on tasks.
* Prefer good naming schemas rather than abundance of comments. Keep comments to a minimum, only whenever necessary.
* Favour short code lengths, but not at the expense of making it cryptic. Always prefer readable code.

## AI Workflow
* **YOU MUST NEVER commit code to the git repository. Always leave the human to control that aspect of development.**
* After code generation, first run the `dotnet format` command to ensure style consistency.
* Then, run the `dotnet test --filter "TestCategory=Unit"` command to verify all tests pass and no errors or warnings exist.
* If a mistake is made, add a specific rule to this file to prevent it in the future.
