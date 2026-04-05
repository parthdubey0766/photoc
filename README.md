# PhotoC

PhotoC is a Windows desktop utility that automatically watches a folder and optimizes incoming photos to reduce disk usage while preserving visual fidelity and metadata.

## Description

Modern phone photos can consume significant storage. PhotoC runs in the background, monitors a configured sync folder, and processes new images using format-aware compression strategies. The app is designed for safe, unattended operation with queue-based processing and robust retry behavior.

## Mind of the App

The purpose of PhotoC is to make photo storage optimization automatic, reliable, and transparent.

- Automatic: No repetitive manual export or batch steps.
- Safe: Original files are protected by careful write/replace flow.
- Practical: Focus on meaningful storage reduction for large images.
- Trustworthy: Keep metadata and color information intact for real-world use.

## Features

- Folder monitoring with debounce to avoid partial-file processing.
- Background queue processing for stable throughput.
- JPEG adaptive compression flow.
- PNG optimization.
- WebP optimization.
- BMP/TIFF conversion to PNG for better space efficiency.
- Metadata preservation (EXIF/ICC/IPTC/XMP where supported).
- Retry handling and logging for recoverability.
- Tray-based desktop operation with settings and log windows.

## Tech Stack

- Language: C#
- Framework: .NET 10 (WPF)
- Image processing: Magick.NET
- UI components: MaterialDesignThemes, H.NotifyIcon.Wpf
- Logging: Serilog
- Configuration: Microsoft.Extensions.Configuration.Json

## Installation

1. Install .NET SDK 10.0 or newer.
2. Clone this repository.
3. Open the solution file `PhotoC.slnx` in Visual Studio or VS Code.
4. Restore dependencies:

```powershell
dotnet restore .\PhotoC\PhotoC.csproj
```

5. Build the project:

```powershell
dotnet build .\PhotoC\PhotoC.csproj --configuration Release
```

6. Run the application:

```powershell
dotnet run --project .\PhotoC\PhotoC.csproj --configuration Release
```

## Usage

1. Launch PhotoC.
2. Open Settings from the tray icon.
3. Select your watched folder (for example, your phone sync folder).
4. Save and apply settings.
5. Add images to the watched folder and monitor processing from the log window.

## Folder Structure

```text
PhotoC/
├─ PhotoC.slnx
├─ README.md
├─ TEST_CASES.md
└─ PhotoC/
	├─ App.xaml
	├─ App.xaml.cs
	├─ GlobalUsings.cs
	├─ appsettings.json
	├─ PhotoC.csproj
	├─ runtimeconfig.template.json
	├─ Assets/
	├─ Helpers/
	├─ Models/
	├─ Services/
	└─ UI/
```

## Future Improvements

- Optional backup mode before in-place replacement.
- Folder-specific compression profiles.
- Richer telemetry and summary reports.
- Optional CLI mode for scheduled batch processing.
- Automated CI build and release pipeline.
