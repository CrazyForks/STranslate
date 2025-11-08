# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

**重要提示：使用中文回答用户的所有问题和交互。**

## Project Overview

STranslate is a Windows desktop translation application built with .NET 9.0 and WPF. It features a plugin-based architecture supporting translation, OCR, text-to-speech (TTS), and vocabulary management.

## Build and Development Commands

### Build Solution
```bash
# Build entire solution (all projects including main app and plugins)
dotnet build src/STranslate.sln --configuration Debug
dotnet build src/STranslate.sln --configuration Release
```

### Build Individual Projects
```bash
# Build main application only
dotnet build src/STranslate/STranslate.csproj --configuration Debug

# Build plugin framework library
dotnet build src/STranslate.Plugin/STranslate.Plugin.csproj --configuration Debug

# Build a specific plugin (example: GoogleBuiltIn)
dotnet build "src/Plugins/STranslate.Plugin.Translate.GoogleBuiltIn/STranslate.Plugin.Translate.GoogleBuiltIn.csproj" --configuration Debug
```

### Clean Build
```bash
dotnet clean src/STranslate.sln
```

### Run Application
```bash
# Debug build
dotnet run --project src/STranslate/STranslate.csproj --configuration Debug

# Or directly execute the built executable
.artifacts/Debug/STranslate.exe
```

### Restore NuGet Packages
```bash
dotnet restore src/STranslate.sln
```

## Architecture Overview

### Solution Structure
The solution (`src/STranslate.sln`) contains 12 projects:
- **STranslate** - Main WPF application (entry point)
- **STranslate.Plugin** - Plugin framework SDK library
- **10 Plugin Projects** - Translation, OCR, TTS, and Vocabulary plugins

### Core Architecture Patterns

**MVVM with Dependency Injection:**
- Uses `CommunityToolkit.Mvvm` for ViewModels and commands
- Microsoft.Extensions.DependencyInjection for IoC container
- Configured in `App.xaml.cs` using `IHostBuilder`

**Plugin System:**
Plugins are dynamically loaded assemblies packaged as `.spkg` files (ZIP format):
1. Each plugin contains: DLL, `plugin.json` (metadata), `icon.png`, and language XAML files
2. `PluginManager` (in `Core/PluginManager.cs`) handles plugin discovery, extraction, and loading
3. Plugins use separate `AssemblyLoadContext` for isolation
4. Plugin interfaces: `ITranslatePlugin`, `IDictionaryPlugin`, `IOcrPlugin`, `ITtsPlugin`, `IVocabularyPlugin`
5. All plugins reference `STranslate.Plugin` project

**Application Entry Point:**
- `App.xaml.cs` bootstraps the entire application:
  - Enforces single-instance via `ISingleInstanceApp`
  - Loads settings from JSON storage (`AppStorage<T>`)
  - Configures Serilog for logging
  - Registers services: PluginManager, ServiceManager, and service instances (TranslateInstance, OcrInstance, etc.)
  - Main window: `Views/MainWindow.xaml`

### Key Directories

**src/STranslate/ (Main Application):**
- `Core/` - Core services (PluginManager, ServiceManager, Settings, HttpService, Screenshot, etc.)
- `ViewModels/` - MVVM ViewModels (MainWindowViewModel, SettingsWindowViewModel, page ViewModels)
- `Views/` - XAML windows and user controls
  - `Pages/` - Settings pages (GeneralPage, HotkeyPage, PluginPage, TranslatePage, etc.)
- `Instances/` - Service coordinators (TranslateInstance, OcrInstance, TtsInstance, VocabularyInstance)
- `Controls/` - Custom WPF controls (ServicePanel, InputControl, OutputControl, HotkeyControl, etc.)
- `Helpers/` - Utility classes (Win32Helper, UACHelper, ProxyHelper, LanguageDetector, etc.)
- `Converters/` - XAML value converters for data binding
- `Languages/` - Internationalization XAML files (en.xaml, zh-cn.xaml, zh-tw.xaml)

**src/STranslate.Plugin/ (Plugin Framework):**
- Plugin interfaces and base classes
- Shared models and enums
- `PluginMetaData` for plugin.json parsing

**src/Plugins/ (Plugin Implementations):**
- Each plugin is a separate project outputting to `.artifacts/[Debug|Release]/Plugins/{PluginName}/`
- Translation: GoogleBuiltIn, MTranServer, TransmartBuiltIn, OpenAI, BigModel, KingSoftDict, BingDict
- OCR: WeChat (uses WeChatOcr library)
- TTS: MicrosoftEdge
- Vocabulary: Eudict

### Build System Details

**Centralized Package Management:**
- `src/Directory.Packages.props` - All NuGet package versions (42 packages)
- `src/Directory.Build.props` - Shared build properties (C# preview features, nullable enabled)
- `src/nuget.config` - NuGet package sources

**Build Configurations:**
- **Debug:** Outputs to `.artifacts/Debug/`, portable debug symbols, verbose logging
- **Release:** Outputs to `.artifacts/Release/`, no debug symbols, optimized, uses Costura.Fody to merge assemblies

**Fody Weavers:**
- Custom MSBuild target `SelectFodyWeaversConfig` copies `FodyWeavers.[Configuration].xml` before build
- Debug uses `MethodBoundaryAspect.Fody` for AOP
- Release adds `Costura.Fody` for assembly embedding (single-file distribution)

**Output Paths:**
- Main app: `.artifacts/[Debug|Release]/STranslate.exe`
- Plugins: `.artifacts/[Debug|Release]/Plugins/{PluginName}/`

### Configuration and Settings

**Application Settings:**
Settings are stored as JSON files via `AppStorage<T>` class:
- `Settings` - General application settings
- `HotkeySettings` - Global hotkey configurations
- `ServiceSettings` - Service-specific settings (API keys, endpoints, etc.)

All settings implement `ObservableObject` for two-way binding and auto-save on property changes.

### Key Technologies

- **.NET 9.0** with C# preview language features
- **WPF** with `iNKORE.UI.WPF.Modern` for modern UI
- **Serilog** for structured logging to files
- **NHotkey.Wpf** and **MouseKeyHook** for global hotkeys
- **NAudio** for audio playback (TTS)
- **WeChatOcr** for OCR functionality
- **System.IdentityModel.Tokens.Jwt** for API authentication
- **ScreenGrab** for screenshot functionality

## Plugin Development

### Creating a New Plugin

1. Reference the `STranslate.Plugin` NuGet package or project
2. Implement the appropriate interface (`ITranslatePlugin`, `IOcrPlugin`, etc.)
3. Create `plugin.json` with metadata:
   ```json
   {
     "id": "unique-plugin-id",
     "name": "Plugin Display Name",
     "version": "1.0.0",
     "type": "translate" // or "ocr", "tts", "vocabulary"
   }
   ```
4. Add `icon.png` and language XAML files (en.xaml, zh-cn.xaml, zh-tw.xaml)
5. Set output path to `.artifacts/[Debug|Release]/Plugins/{YourPluginName}/`
6. Package as `.spkg` file (ZIP format) for distribution

### Plugin Project Configuration

Use this `.csproj` template:
```xml
<PropertyGroup>
  <TargetFramework>net9.0-windows</TargetFramework>
  <UseWPF>true</UseWPF>
  <OutputPath>..\..\.artifacts\Debug\Plugins\{YourPluginName}\</OutputPath>
</PropertyGroup>

<ItemGroup>
  <Content Include="Languages\*.*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
  <Content Include="icon.png">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </Content>
  <Content Include="plugin.json">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </Content>
</ItemGroup>

<ItemGroup>
  <ProjectReference Include="..\..\STranslate.Plugin\STranslate.Plugin.csproj" />
</ItemGroup>
```

## Important Development Notes

### Settings Storage
Settings are loaded in `App` constructor before DI container initialization. Never modify settings structure without migration logic in `AppStorage<T>`.

### Plugin Loading
Plugins are loaded dynamically via reflection. Ensure plugin metadata (ID, version, type) in `plugin.json` matches the implementation.

### Single Instance Enforcement
The application enforces single-instance via `ISingleInstanceApp`. Secondary instances activate the existing instance instead of launching.

### Internationalization
All UI strings should be defined in language XAML files (`Languages/` directory) and referenced via DynamicResource binding.

### UAC and Admin Rights
The app can request admin elevation via `UACHelper`. Optional scheduled task creation for UAC bypass is available.

### Logging
Serilog is configured globally. Use `ILogger<T>` injected from DI container. Log files are written to the application data directory.

### Code Style
Follow `.editorconfig` rules in the repository root. C# preview features are enabled, and nullable reference types are enforced.
