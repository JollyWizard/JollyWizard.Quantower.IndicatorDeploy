# JollyWizard.Quantower.IndicatorDeploy
A project to help deploy indicator projects for the Quantower platform.

## Overview

This project builds an installer (`JollyWizard.Quantower.IndicatorDeploy.exe`) for Quantower Indicator Projects.

It also builds a `MSBuild/nuget` project dependency, which enables build tasks to help with Quantower development:

* Automatically detect running Quantower instances and include their assemblies as references.
  * This prevents hardcoding local paths into repository like Quantower Algo.
* Automatically deploy builds to the current Quantower instance.
* Package Builds with the installer for distribution.

## Usage

Main documentation will be updated when the uploading of sibling projects is complete.

## Required

For the installer, Dotnet 4.8 is required. It is the same version used by Quantower Algo project templates, so it is should be bundled with Quantower.

For development tools, it currently requires either Quantower to be running or that the `QuantowerRoot` environment variable is set to the Quantower folder with `Start.lnk`.
