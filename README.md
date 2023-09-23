# Cosmic Engine

## NOTE: If you just want to know how to set up Sonic CD with Cosmic, click >[here](#playing-sonic-cd-with-cosmic)<

Cosmic Engine is an ongoing major rewrite/"source port" of the [Retro Engine v3 Decompilation](https://github.com/Rubberduckycooly/Sonic-CD-11-Decompilation). The entire codebase has been moved to another programming language to make use of higher-level programming constructs and better memory safety features. Currently, it is written in C#, though that's subject to change (more [below](#current-roadmap-for-cosmic)). **Cosmic is currently not stable. You can start projects with it right now, but expect to run into issues.**

#### Current features of the engine include:
- Rendering backend has been replaced with a new one using [Veldrid](https://github.com/veldrid/veldrid) to provide cross-platform hardware-accelerated graphics. Care has been taken to preserve features that were previously only present in the software renderer (such as multiple simultaneous cycling palettes and richer color blending).
- Games now run inside a GUI frontend powered by [Dear ImGui](https://github.com/ocornut/imgui). Debugging features and menus are accessed through dropdown menus with a mouse, without interrupting gameplay. Additionally, normal non-debug options can now be changed ingame through a popup window, without having to edit a text file and restart.
- The decomp-specific "HQ" rendering option has been expanded to work *everywhere* ingame. Alongside the 3D floor mode, all other visual effects in other modes that rely on scaling and rotation can now be calculated at twice the base resolution for cleaner visuals.

#### Features that have begun implementation, but are not yet complete:
- A level collision viewer has been added. Previously, only collisions between objects could be examined. Cosmic now also lets you view the makeup of level tiles, including heightmaps and solidity.
- A toggleable input display (of ingame buttons) has been added.
- Engine-specific data formats are being specified outside the source code itself, using the [Kaitai Struct declaration language](https://kaitai.io/) to generate cross-language parsers for them. This is intended to allow future tools for the engine to be written in other programming languages, without having to worry about keeping the parsing logic between engine and tools in sync when the format changes.

#### Features that haven't been started yet, but are intended to be implemented:
- An alternative higher-level scripting language, with syntax closer to C, to allow script logic to be written with less worry about running out of variables.

## Building

**NOTE: If you just want to try out Cosmic, building is not necessary. See [here](#playing-sonic-cd-with-cosmic).**

This repo is cleaned up from my own personal folder working on Cosmic before it was uploaded to GitHub, so apologies for any oddities! The steps boil down to:

#### File preparation
- Download and install the [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (if you're unsure which to choose for your platform, choose the installer).
- In a terminal, navigate to the directory you want to make the *parent* directory of the repo (not the directory of the repo itself). From there, run `git clone --recursive https://github.com/foreverWIP/cosmic`. If you don't have Git installed, you can find downloads for your platform [here](https://git-scm.com/downloads). GitHub Desktop and other clients also work; use whatever you're comfortable with.
  ##### NOTE: If you don't want to bother with Git, click the green button labelled "Code" at the top of the page, then click "Download ZIP", then extract the zip to your desired location.
- Grab [Kaitai Struct compiler](https://kaitai.io/#download) (download the "Universal .zip" option), and extract the single folder in that zip file (`kaitai-struct-compiler-<version>`) to the root of this repo.
- Download [fnalibs](http://fna.flibitijibibo.com/archive/fnalibs.tar.bz2), and extract its contents (it should be multiple folders and a readme, if you get to fnaliba.tar with no .bz2 just open that up too) to a folder named `fnalibs` inside this repository's root.

#### The actual build
- Navigate to the root of this repo in a terminal.
- Before building, generate data file definitions by running:
    - Windows: `./build-format-defs.ps1`
    - macOS/*nix: `./build-format-defs.sh`
- To build and run a debug build, run `dotnet run`. (To build without running, run `dotnet build`.)
- Release builds have so far only been tested on Windows. Those familiar with .NET on other platforms may attempt a release build on their own, but for starters on Windows, run `./build-release-windows.ps1`

#### Playing Sonic CD with Cosmic
##### NOTE: If you just want to use Cosmic without caring about its source code, just download a prebuilt version from the "Releases" tab on the right of the repo page.
##### NOTE: Cosmic is designed around the mobile version of Sonic CD's Data folder, as this is currently the only legally available standalone version of the game. How you obtain this folder is up to you.

Cosmic requires a couple very small modifications to the v3 decomp's normal setup in order to play Sonic CD. Mainly, it uses slightly different scripts, so unfortunately mods will not work out of the box. I've prepared some steps to make setting up the base game easier:
- Ensure your Data folder is in the same directory as the engine executable. If you've built from source, check the following locations:
    - Under a debug build, this should be `Cosmic.Desktop/bin/Debug/net8.0/win-x64`
    - Under a release build, this should be `Cosmic.Desktop/bin/Release/net8.0/win-x64/publish`

   If you're not actively touching the source code, copying the compiled files in this folder out of their normal locations to another folder is highly recommended for ease of access.
- Navigate to the root of this repo in a terminal.
- Run `git clone https://github.com/foreverWIP/cosmic-cd-data ./Scripts`
- Move the file `Achievements.txt` in the root of the repo you just cloned into the `Game` directory inside your Data folder. This will not affect it working with the original decomp, so you can safely keep it in the Data folder when using it elsewhere.
- **If you want video support, you'll need the Steam version's videos folder.** Just copy the folder into the executable's directory, like with Data and Scripts.
- You're ready to play! Just run `Cosmic.Desktop.exe` (on non-Windows platforms the `exe` will be omitted).

## Current Roadmap for Cosmic

Cosmic is currently written in C#. I chose it as the target language for a source port because:
- I was already very familiar with it.
- C# promises consistent runtime behavior across platforms, which would make it easy to get the engine compiled for multiple systems without having to mess with application logic or the build system heavily for cross-platform tasks.
- C# also promises memory safety via garbage collection. This is mainly a convenience feature for developers, but it also means that it's much easier to track down bugs like null-pointer exceptions than it would be in C/C++.

However, as of a little over a month ago, a [series](https://cohost.org/amy/post/1796458-what-do-you-do-when) of [predicaments](https://cohost.org/amy/post/2359696-ok-yeah-no-this-is-t) have left me frustrated with the development setup required to make the most of the language. Part of the drive to make Cosmic came from wanting to make development of projects running on Retro Engine v3 more accessible for others. However, C# development being locked behind heavy bias toward a specific IDE (Visual Studio) has me uncertain if this language is a good fit for the project. With this project becoming public, I hope to listen to other developers to get a good idea of whether this is a worthy tradeoff.
