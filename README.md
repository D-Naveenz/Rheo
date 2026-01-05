# Rheo.Storage

A modern .NET library for file and directory operations with rich metadata, content-based file analysis, and asynchronous I/O operations.

## Features

- **Unified Storage Objects**: Work with files and directories through intuitive `FileObject` and `DirectoryObject` classes
- **Rich Metadata**: Access detailed information including file types, MIME types, sizes, timestamps, and attributes
- **Content-Based Analysis**: Automatically detect file types based on content, not just extensions
- **Async Operations**: All I/O operations support async/await with progress reporting and cancellation
- **Change Monitoring**: Built-in file system watching for directory changes
- **Cross-Platform**: Works on Windows, Linux, and macOS with platform-specific optimizations

## Installation

```bash
dotnet add package Rheo.Storage
```

## Quick Start

### Working with Files

```csharp
using Rheo.Storage;

// Create a file object
var file = new FileObject("document.pdf");

// Access rich metadata
Console.WriteLine($"Type: {file.Information.TypeName}");
Console.WriteLine($"MIME: {file.Information.MimeType}");
Console.WriteLine($"Size: {file.Information.FormattedSize}");
Console.WriteLine($"Extension: {file.Information.ActualExtension}");

// Copy with progress reporting
await file.CopyAsync("backup/", progress: new Progress<StorageProgress>(p =>
{
    Console.WriteLine($"Progress: {p.BytesTransferred}/{p.TotalBytes} bytes ({p.BytesPerSecond:N0} B/s)");
}));

// Move or rename
await file.MoveAsync("archive/");
await file.RenameAsync("new-name.pdf");
```

### Working with Directories

```csharp
using Rheo.Storage;

// Create a directory object (automatically monitors for changes)
var directory = new DirectoryObject("C:/Projects");

// Access directory information
Console.WriteLine($"Files: {directory.Information.NoOfFiles}");
Console.WriteLine($"Subdirectories: {directory.Information.NoOfDirectories}");
Console.WriteLine($"Total Size: {directory.Information.FormattedSize}");

// Get files and subdirectories
var files = directory.GetFiles("*.cs", SearchOption.AllDirectories);
var subdir = directory.GetDirectory("bin");
var file = directory.GetFile("README.md");

// Copy entire directory tree with concurrency control
await directory.CopyAsync("backup/", maxConcurrent: 8);
```

### File Analysis

```csharp
var file = new FileObject("unknown-file");

// Content-based analysis happens automatically
var report = file.Information.IdentificationReport;

Console.WriteLine($"Detected Type: {file.Information.TypeName}");
Console.WriteLine($"True Extension: {file.Information.ActualExtension}");
Console.WriteLine($"MIME Type: {file.Information.MimeType}");

// Access all possible file definitions
foreach (var definition in report.Definitions)
{
    Console.WriteLine($"Possible: {definition.Subject.FileType}");
}
```

## Documentation

For comprehensive documentation, tutorials, and advanced usage examples, visit the [GitHub Wiki](https://github.com/D-Naveenz/Rheo/wiki).

## Requirements

- .NET 10.0 or later
- C# 14.0

## License

This project is licensed under the MIT License. See [LICENSE.txt](LICENSE.txt) for details.

## Contributing

Contributions are welcome! Please feel free to submit issues or pull requests.
