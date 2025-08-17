# PitHero - Event-Driven Game
A 2D event-driven game built with MonoGame 3.8.4 and Nez framework.

This project has been converted from FNA to MonoGame 3.8.4 (OpenGL) for enhanced compatibility and features.

## Features ##
- MonoGame 3.8.4 with Nez framework integration
- Boilerplate project already included -- no need to wrestle with MSBuild configurations or writing yet another Game1 class
- Visual Studio Code tasks for building and running your game, compiling T4 templates, cleaning/restoring your project, compiling .fx files and building content with the MonoGame Pipeline tool
- In-editor debugging support with the Mono Debugger


## Prerequisites ##
- [Visual Studio Code](https://code.visualstudio.com) or [Visual Studio](https://visualstudio.microsoft.com/) (recommended to use Visual Studio Code because it has some custom tasks but either will work fine)
- [Mono Debugger Extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode.mono-debug) (required for macOS debugging)
- [MonoGame](http://www.monogame.net/downloads/) (required for compiling content with the Pipeline tool)
- (optional) [.NET Core](https://dotnet.microsoft.com/download) (required for compiling T4 templates)
- (optional) [Microsoft DirectX SDK (June 2010)](https://www.microsoft.com/en-us/download/details.aspx?id=6812) (required for building effects, [you can use Wine to run this](WINE_INSTALL.md))
- (windows only) [7zip](https://www.7-zip.org) (if intending to use fnalibs, the filetype is unsupported for windows and requires 7zip to be installed to decompress/unzip)
- (windows only) [Build tools for Visual Studio](https://visualstudio.microsoft.com/thank-you-downloading-visual-studio/?sku=BuildTools&rel=16) (these build tools are required for all Visual Studio Code build tasks. if you intend to use the full Visual Studio, these are already included. be sure to include .NET Core Build Tools in the installation)


## Setup Instructions ##
1. Clone or download the repository
2. Run `dotnet restore` to restore NuGet packages (MonoGame.Framework.DesktopGL 3.8.4 and Nez dependencies)
3. Open the root folder that contains the .sln file in Visual Studio Code or the .sln file directly in Visual Studio

That's it! Now you're ready to build and run the project with MonoGame. Nez is setup as a submodule so you can update it in the normal fashion.

When developing, raw content (files not processed by the Pipeline tool) should be placed in the `Content` folders subfolders and anything that needs processing should go in the `CompiledContent` folder and added to the Pipeline tool.

The setup process will also init a git repo for you with Nez added as a submodule.

If you want to see debug output, use `Nez.Debug.Log()` calls which will output to the console. For traditional .NET debugging, you can still use `System.Diagnostics.Debug` calls in the VS Code Debug Console by adding a listener: `System.Diagnostics.Debug.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(System.Console.Out));`


## Build Tasks ##
- **Restore Project:** Restores the .csproj. Run it again whenever you change the .csproj file.
- **Restore and Rebuild Nez:** Fetches the latest version of Nez from GitHub, restores and rebuilds Nez
- **Build (Debug/Release):** Builds the project with the specified configuration but does not run it. This also runs MGCB.exe and copies over everything in the `Content` and `CompiledContent` subdirectories.
- **Build and Run (Debug/Release):** Builds and runs the project. On MacOS, it runs the output with Mono. On Windows, it runs the output with .NET Framework.
- **Clean Project:** Cleans the output directories and all their subdirectories.
- **Build Effects:** Runs `fxc.exe` on all of the `.fx` files found in the Content/ subdirectories and outputs corresponding `.fxb` files that can be loaded through the Content Manager at runtime.
- **Build Content:** Runs good old MGCB.exe on the Content.mgcb file
- **Force Build Content:** Force builds the content (MGCB.exe -r)
- **Open Pipeline Tool:** Opens the MonoGame Pipeline tool
- **Process T4 Templates:** Processes any T4 templates found in the `T4Templates` folder. Note that the install script will attempt to install the t4 command line program which requires the `dotnet` command line program to be installed. The install command it will run is `dotnet tool install -g dotnet-t4`.


## License and Credits ##
PitHero is released under the Microsoft Public License.

Originally based on FNA VSCode Template. Many thanks to Andrew Russell for his [FNA Template](https://github.com/AndrewRussellNet/FNA-Template), from which the project structure was learned.

This project has been converted from FNA to MonoGame 3.8.4 for enhanced compatibility.
