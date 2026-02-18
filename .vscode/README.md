# Visual Studio Code Setup for PitHero

This directory contains VS Code configuration files for developing PitHero with modern .NET support.

## Prerequisites

### Required
- [Visual Studio Code](https://code.visualstudio.com)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [C# Dev Kit Extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) (recommended)

### For macOS Users
- [Mono Debugger Extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode.mono-debug)
- Mono runtime (if using legacy Mono debugger configurations)

### For Content Pipeline
- [MonoGame](http://www.monogame.net/downloads/) (for content building with Pipeline tool)

## Initial Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/rpillai25/PitHero.git
   cd PitHero
   ```

2. **Initialize submodules and download FNA:**
   ```bash
   git submodule update --init --recursive
   # On macOS/Linux:
   bash getFNA.sh
   # On Windows:
   powershell ./getFNA.ps1
   ```

3. **Open in VS Code:**
   ```bash
   code .
   ```

4. **Install recommended extensions:**
   - VS Code will prompt you to install recommended extensions
   - Or manually install them from the Extensions view

5. **Build the project:**
   - Press `Ctrl+Shift+B` (or `Cmd+Shift+B` on Mac)
   - Select "dotnet: build" from the task list

## Available Tasks

### Modern .NET Tasks (Recommended)
- **dotnet: build** - Default build task (F5 or Ctrl+Shift+B)
- **dotnet: build (Debug)** - Build in Debug configuration
- **dotnet: build (Release)** - Build in Release configuration
- **dotnet: run** - Run the game without debugging
- **dotnet: test** - Run all unit tests
- **dotnet: clean** - Clean build artifacts

### Legacy MSBuild Tasks (Visual Studio Compatibility)
- **Build (Debug)** - MSBuild debug build
- **Build (Release)** - MSBuild release build
- **Build and Run (Debug)** - Build and run with MSBuild
- **Build and Run (Release)** - Build and run release with MSBuild
- **Restore Project** - Restore NuGet packages
- **Clean Project** - Clean with MSBuild
- **Update, Restore and Rebuild Nez** - Update Nez submodule

### Content & Effects Tasks
- **Build Content** - Build MonoGame content
- **Force Build Content** - Force rebuild all content
- **Build Effects** - Compile .fx shader files
- **Open Pipeline Tool** - Open MonoGame Pipeline editor
- **Process T4 Templates** - Process T4 template files

## Debugging Configurations

### Modern .NET Debugger (Recommended)
- **.NET: Launch** - Launch and debug with .NET debugger (builds first)
- **.NET: Launch Without Building** - Debug without building
- **.NET: Attach to Process** - Attach to running process

### Legacy Debuggers (For Compatibility)
- **Launch (Mac)** - Debug with Mono debugger on macOS
- **Launch Without Building (Mac)** - Mac debug without build
- **Launch (Windows)** - Debug with CLR debugger on Windows
- **Launch Without Building (Windows)** - Windows debug without build
- **Attach** - Attach Mono debugger to running process

## Running the Game

### Option 1: Debug with F5
1. Set breakpoints as needed
2. Press `F5` to launch with debugger
3. The game will build and start in debug mode

### Option 2: Run Task
1. Press `Ctrl+Shift+P` (or `Cmd+Shift+P` on Mac)
2. Type "Tasks: Run Task"
3. Select "dotnet: run" or "Build and Run (Debug)"

### Option 3: Command Line
```bash
dotnet run --project PitHero/PitHero.csproj
```

## Troubleshooting

### Build Errors
- **Missing FNA/Nez**: Run `git submodule update --init --recursive`
- **Missing FNA libs**: Run `./getFNA.sh` (Mac/Linux) or `./getFNA.ps1` (Windows)
- **NuGet restore fails**: Try `dotnet restore PitHero.sln`

### Debugger Not Working
- **Modern .NET debugger**: Ensure C# Dev Kit extension is installed
- **Mac debugger**: Install Mono Debugger extension
- **Check paths**: Verify paths in launch.json match your build output

### Content Build Issues
- **MGCB not found**: Install MonoGame SDK
- **Effects build fails**: Install DirectX SDK (or use Wine on Mac/Linux)

## Visual Studio Compatibility

This project is fully compatible with Visual Studio and Visual Studio Insiders:
- Open `PitHero.sln` in Visual Studio
- All MSBuild tasks work in both VS Code and Visual Studio
- Solution file is maintained for full IDE compatibility

## File Structure

```
.vscode/
├── extensions.json      # Recommended VS Code extensions
├── launch.json          # Debug configurations
├── tasks.json           # Build and run tasks
├── settings.json        # VS Code workspace settings
├── buildEffects.sh      # Shell script to build effects (Mac/Linux)
├── buildEffects.ps1     # PowerShell script to build effects (Windows)
├── processT4Templates.sh    # Process T4 templates (Mac/Linux)
├── processT4Templates.ps1   # Process T4 templates (Windows)
└── README.md           # This file
```

## Additional Resources

- [PitHero Repository](https://github.com/rpillai25/PitHero)
- [FNA Documentation](https://fna-xna.github.io/)
- [Nez Documentation](https://github.com/prime31/Nez)
- [.NET Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [VS Code C# Extension](https://code.visualstudio.com/docs/languages/csharp)
