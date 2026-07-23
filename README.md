<div align="center">

  <img src="src/Assets/Logo-and-text.png" alt="SP Converter Logo" width="200" />

  [![Framework](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
  [![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D4?style=flat-square&logo=windows)](https://microsoft.com/windows)
  [![UI](https://img.shields.io/badge/UI-Fluent%20WPF-0078D4?style=flat-square)](https://github.com/lepoco/wpfui)
  [![License](https://img.shields.io/badge/License-MIT-green.svg?style=flat-square)](LICENSE)
  [![Release](https://img.shields.io/badge/Release-v1.1-blue.svg?style=flat-square)](https://github.com/uasdiuduiasdasd/SP-onverter/releases)

</div>

## Overview

SP Converter is a Windows application designed for fast, multithreaded image processing and format conversion. It supports both single-file and folder-based conversion, making effective use of multi-core processors.

## Technical Specifications & Features

- **Multi-Core Parallel Execution:** Asynchronous batch conversion powered by `Magick.NET` and `Parallel.ForEachAsync`.
- **Fluent User Interface:**
  - Support for native Windows 11 **Mica** and **Acrylic** backdrop materials.
  - Four UI themes: System Default, Light Mode, Dark Mode, and High-Contrast Black & White.
  - Dynamic responsive layout with hardware-accelerated rendering.
- **Image Processing Capabilities:**
  - **EXIF Auto-Orientation:** Automatic rotation based on camera and smartphone EXIF metadata tags.
  - **Alpha Channel Handling:** Automatic white-background flattening when converting transparent images to targets that do not preserve alpha, such as JPEG and BMP.
  - **Compression Tuning:** Dedicated lossy compression controls (50%, 75%, 90%, 100%, Custom) isolated from lossless targets.
  - **Multi-Image Containers:** Optional extraction of all frames, pages, or icon sizes from GIF, WEBP, AVIF, TIFF, PDF, ICO, HEIC, and HEIF files.
  - **ICO Output:** Large source images are converted into a multi-size icon set automatically.
- **Automated Directory Management:**
  - Automatic creation of target output subfolders for batch operations in root/system paths.
  - Collision-free file naming (`Converted (1)`).
- **Multilingual Support:** Native English and Russian language localization.
- **Supported Formats:**
  - **Input:** JPG, JPEG, PNG, WEBP, AVIF, BMP, TIFF/TIF, TGA, GIF, ICO, JXL, PDF, HEIC, HEIF, PSD, SVG, DDS, EXR, PPM, PGM, PBM, RAW (CR2, CR3, NEF, ARW, DNG).
  - **Output:** JPG, JPEG, PNG, WEBP, GIF, PDF, TIFF, BMP, ICO, AVIF, JXL, TGA, SVG, PSD, DDS, EXR, PPM, PGM, PBM.
  - **Input-only in the bundled build:** HEIC and HEIF can be read, but are not offered as output formats because the current bundled encoder does not support writing them.

## Architecture & Dependencies

- **Core Framework:** .NET 10.0 (Windows Desktop WPF)
- **Image Processing Library:** Magick.NET (Q16 AnyCPU)
- **UI Control Library:** WPF-UI (Fluent Controls)
- **Design Pattern:** MVVM (CommunityToolkit.Mvvm) + Dependency Injection (`Microsoft.Extensions.Hosting`)
- **Testing Suite:** xUnit Test Framework (`SPConverter.Tests`)

## Building from Source

### Build Commands

1. **Clone repository:**
   ```bash
   git clone https://github.com/uasdiuduiasdasd/SP-onverter.git
   cd SP-onverter
   ```

2. **Compile project:**
   ```bash
   dotnet build SPConverter.slnx -c Release
   ```

3. **Build clean release files:**
   ```powershell
   .\build-release.ps1
   ```

   The script runs tests, publishes a self-contained portable build, creates an Inno Setup installer, and places both release artifacts in `setup/`.

## Installation

**Note:** Windows SmartScreen may display a security warning when running the installer or the application for the first time. This occurs because the executable is not signed with a commercial Code Signing Certificate. To proceed, click **"More info"** and then **"Run anyway"**. The application is open-source and can be audited via this repository.

## License

This project is licensed under the terms of the **MIT License**. See `LICENSE` for details.
