# Default complication includes in iOS, tvOS, macOS and Mac Catalyst projects

Default compilation includes for .NET Core projects is explained here:
[Default compilation includes in .NET Core projects][1]

This document explains how default compilation includes is implemented for
iOS, tvOS, macOS, and Mac Catalyst projects.

Default inclusion can be completely disabled by setting
`EnableDefaultItems=false`. It can also be disabled per-platform by setting
the platform-specific variables `EnableDefaultiOSItems=false`,
`EnableDefaulttvOSItems=false`, `EnableDefaultMacCatalystItems=false`, or
`EnableDefaultmacOSItems=false`.

## Property lists

All \*.plist files in the root directory are included by default (as `None`
items).

## SceneKit Assets

All \*.scnassets directories anywhere in the project directory or any
subdirectory are included by default (as `SceneKitAsset` items).

## Storyboards

All \*.storyboard and \*.xib files in the project directory or any
subdirectory are included by default (as `InterfaceDefinition` items).

## Asset catalogs

All \*.pdf, \*.jpg, \*.png and \*.json files inside asset catalogs
(\*.xcassets) in the project directory or any subdirectory are included by
default (as `ImageAsset` items).

## Atlas Textures

All \*.png files inside \*.atlas directories in the project directory or any
subdirectory are included by default (as `AtlasTexture` items).

## Core ML Models

All \*.mlmodel files anywhere in the project directory or any subdirectory are
included by default (as `CoreMLModel` items).

## Metal

All \*.metal files anywhere in the project directory or any subdirectory are
included by default (as `Metal` items).

## Binding projects

Default compilation includes is turned off for binding projects, because
typically there are C# source files (ApiDefinition.cs, StructsAndEnums.cs,
etc.) in the binding project directory which should be compiled as binding
source code, and not as normal C# source code.

[1]: https://docs.microsoft.com/en-us/dotnet/core/tools/csproj#default-compilation-includes-in-net-core-projects

## Font files

All \*.ttf, \*.ttc and \*.otf files anywhere inside the Resources/
subdirectory are included by default (as `BundleResource` items).
