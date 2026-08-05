# CLAUDE.md

## Project overview

This repository contains a .NET solution for experimenting with a geometry library intended for use in games and other non-high precision applications.

## Stack

- C#
- Microsoft .NET 10
- Newtonsoft JSON parser
- NUnit tests 

## Primary projects

- SpatialTrees
- SpatialTreeTests

Other libraries can be examines, but not altered without explicit permission.

## Development guidance

- Keep changes compatible with .NET 10.
- There should be only one namespace per project, and it should match the project name.
- No private functions allowed. methods should be public to allow unit tests to be written.
- set up functions to use dependency injection to allow for easy testing.
- Prefer small, focused updates to the genetic logic and parser code.
- When changing behavior in the console app, keep the existing JSON parsing flow intact unless there is a specific reason to restructure it.
- If you add new features, update this file to reflect the new workflow.
- Don't upgrade any nuget package version without asking first. You can point out out of date packages to the user.
- Don't do write anything to git. You can read all you want, but no writes or commits.
- when refactoring existing code, do not remove comments. They can be updated if needed, but not removed.