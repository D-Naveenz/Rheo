# Rheo.Storage

[![NuGet](https://img.shields.io/nuget/v/Rheo.Storage.svg)](https://www.nuget.org/packages/Rheo.Storage/)
[![License: Apache-2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE.txt)

**Intelligent file system operations with content-based analysis, real-time progress tracking, and built-in change monitoring.**

Rheo.Storage brings signature-based file type detection, comprehensive async operations, and automated event-driven monitoring to .NET file system programming.

---

## What Rheo.Storage Delivers

Rheo.Storage provides modern file system capabilities that .NET's built-in APIs don't offer:

| Feature | Built-in .NET | Rheo.Storage |
|---------|---------------|--------------|
| **File Type Detection** | Extension-based | ✅ Signature-based content analysis |
| **Progress Tracking** | Not available | ✅ Real-time `StorageProgress` with transfer speeds |
| **Change Monitoring** | Manual setup required | ✅ Integrated with automatic debouncing |
| **Async Operations** | Partial coverage | ✅ Complete async/await with CancellationToken |
| **Metadata** | Basic properties | ✅ MIME types, formatted sizes, platform-specific info |
| **Error Recovery** | Manual handling | ✅ Automatic rollback on failures |

**[Explore full capabilities →](https://github.com/D-Naveenz/rheo-storage/wiki)**

---

## Installation

```bash
dotnet add package Rheo.Storage
```

**Requirements:** .NET 10.0+

---

## Quick Start

### File Operations with Progress

```csharp
using Rheo.Storage;

using var file = new FileObject("document.pdf");

// Rich metadata - content analysis, not just extensions
Console.WriteLine($"Type: {file.Information.TypeName}");         // "PDF Document"
Console.WriteLine($"MIME: {file.Information.MimeType}");          // "application/pdf"
Console.WriteLine($"Actual Extension: {file.Information.ActualExtension}"); // ".pdf"
Console.WriteLine($"Size: {file.Information.FormattedSize}");    // "2.4 MB"

// Copy with real-time progress and cancellation
var progress = new Progress<StorageProgress>(p =>
    Console.WriteLine($"{p.ProgressPercentage:F1}% @ {p.BytesPerSecond/1024/1024:F2} MB/s"));

var cts = new CancellationTokenSource();
await file.CopyAsync("backup/document.pdf", progress, cancellationToken: cts.Token);
```

### Directory Monitoring

```csharp
using var dir = new DirectoryObject(@"C:\Projects");

// Built-in change monitoring - no manual FileSystemWatcher setup
dir.Changed += (sender, e) =>
{
    Console.WriteLine($"{e.ChangeType}: {e.NewInfo?.FullName}");
};

dir.StartWatching(); // Automatic debouncing, automatic cleanup on dispose

// Access comprehensive statistics
Console.WriteLine($"Files: {dir.Information.FileCount}");
Console.WriteLine($"Total Size: {dir.Information.FormattedSize}");
```

### Content-Based File Validation

```csharp
using var upload = new FileObject(userUploadPath);

// Verify file integrity - analyze actual content, not just extensions
if (upload.Information.ActualExtension != upload.Information.Extension)
{
    throw new SecurityException(
        $"File type mismatch: claimed {upload.Information.Extension}, " +
        $"actually {upload.Information.ActualExtension}");
}

// Validate MIME type
if (upload.Information.MimeType != "image/jpeg")
{
    throw new InvalidOperationException($"Expected JPEG, got {upload.Information.MimeType}");
}
```

---

## Documentation

📖 **[Complete Documentation & Wiki →](https://github.com/D-Naveenz/rheo-storage/wiki)**

- [Getting Started](https://github.com/D-Naveenz/rheo-storage/wiki) - Overview and key features
- [FileObject Class](https://github.com/D-Naveenz/rheo-storage/wiki/FileObject-Class) - File operations reference
- [DirectoryObject Class](https://github.com/D-Naveenz/rheo-storage/wiki/DirectoryObject-Class) - Directory operations reference
- [Content Analysis](https://github.com/D-Naveenz/rheo-storage/wiki/FileInformation-Class) - File type detection
- [Progress Reporting](https://github.com/D-Naveenz/rheo-storage/wiki/StorageProgress-Class) - Track long-running operations

---

## 🐛 Found a Bug?

**Help improve Rheo.Storage!** Bug reports are invaluable for making this library better.

**[Report an issue →](https://github.com/D-Naveenz/rheo-storage/issues/new)**

When reporting bugs, please include:
- Expected vs actual behavior
- Code sample to reproduce
- .NET version and OS
- Stack trace (if applicable)

Your feedback directly shapes future development.

---

## Contributing

Contributions are welcome! Please submit issues for bugs or feature requests, and pull requests for code contributions.

## License

Licensed under the [Apache License](LICENSE.txt).
