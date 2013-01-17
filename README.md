# README

ohLibSpotify is OpenHome's managed wrapper around the libspotify library.
To use ohLibSpotify, an application needs only the managed ohLibSpotify.dll,
and the native libspotify.so|.dll|.dylib. (It is not dependent on any other
OpenHome library.)

ohLibSpotify attempts to expose all libspotify entities - such as sessions,
playlists and tracks - as regular C# classes, and where possible libspotify
functions become instance methods on the appropriate class. It does all the
necessary conversions to and from UTF8, so that users see only managed
strings.

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
