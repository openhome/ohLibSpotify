# README

_Insert a description of the project here._

## go / go.bat

Provides the "go" commands that can be used to fetch dependencies,
run builds and maintain files. Before you can invoke these commands,
you must fetch the ohDevTools repository and place it side-by-side
with this one. Run "go" on its own for a list of commands.

## projectdata

Contains configuration information used by automated builds and dependency
fetching tools.

## src

Contains the source code of the project.

## dependencies

This directory will be created by the "go fetch" command. It should contain
external dependencies required during the build process, such as non-framework
third-party assemblies.

## build

This directory will be created during a build. It contains whatever
assemblies, libraries and packages are created by the build.
