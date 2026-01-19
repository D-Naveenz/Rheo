# Rheo.Storage

[![NuGet](https://img.shields.io/nuget/v/Rheo.Storage.svg)](https://www.nuget.org/packages/Rheo.Storage/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.txt)

**A next-generation .NET storage library that goes beyond `FileInfo` and `DirectoryInfo`.**

Rheo.Storage provides intelligent content analysis, built-in progress tracking, automatic change monitoring, and a modern async-first API that conventional .NET file system classes simply don't offer.

---

## Why Rheo.Storage?

Traditional .NET APIs (`File`, `Directory`, `FileInfo`, `DirectoryInfo`) lack critical modern features:

| Built-in .NET | Rheo.Storage |
|---------------|--------------|
| ❌ Extension-based type detection (unreliable) | ✅ Content-based file analysis (secure) |
| ❌ No progress reporting | ✅ Built-in `StorageProgress` |
| ❌ Manual `FileSystemWatcher` setup | ✅ Automatic change monitoring with debouncing |
| ⚠️ Limited async support | ✅ Full async/await + cancellation |
| ❌ No MIME types or formatted sizes | ✅ Standards-compliant MIME types, KB/MB/GB formatting |
| ❌ No automatic rollback | ✅ Automatic cleanup on operation failures |

**[See detailed comparison →](https://github.com/D-Naveenz/rheo-storage/wiki/Home#why-rheostorage)**

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

### Content-Based Security

```csharp
using var upload = new FileObject(userUploadPath);

// Don't trust file extensions - analyze actual content
if (upload.Information.ActualExtension != upload.Information.Extension)
{
    throw new SecurityException(
        $"File type mismatch: claimed {upload.Information.Extension}, " +
        $"actually {upload.Information.ActualExtension}");
}

// Verify MIME type
if (upload.Information.MimeType != "image/jpeg")
{
    throw new InvalidOperationException($"Expected JPEG, got {upload.Information.MimeType}");
}
```

---

## Documentation

📖 **[Complete Documentation & Wiki →](https://github.com/D-Naveenz/rheo-storage/wiki)**

- [Why Rheo.Storage?](https://github.com/D-Naveenz/rheo-storage/wiki/Home#why-rheostorage) - Comparison with built-in APIs
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
