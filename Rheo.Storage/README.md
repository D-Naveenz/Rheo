# Rheo.Storage

Rheo.Storage is a modern, high-performance .NET 9 library for managing files and directories with rich metadata, asynchronous operations, and progress reporting. It provides a flexible abstraction for storage entities, supporting advanced scenarios such as concurrent file operations, custom storage information, and robust error handling.

## Features

- Abstract base class for storage entities (`StorageController<T>`)
- File management via `FileController` (copy, move, rename, delete)
- Rich metadata support (size, content type, display name, etc.)
- Asynchronous operations with cancellation and progress reporting
- Buffer size optimization for large file transfers
- Unit of measurement (UOM) support for storage size
- Extensible storage information via `StorageInfomation` and `FileInfomation`
- Utility helpers for storage paths (`StoragePaths`)
- .NET 9 and C# 13 support

## Getting Started

### Installation

Add a reference to the `Rheo.Storage` project or install via NuGet (if available):

### Usage Example
```csharp
using Rheo.Storage;

// Create a file controller for an existing file
var fileController = new FileController("example.txt");

// Get file metadata
Console.WriteLine($"Created: {fileController.CreatedAt}"); 
Console.WriteLine($"Extension: {fileController.Extension}"); 
Console.WriteLine($"Size: {fileController.GetSizeString()}");

// Copy file asynchronously with progress reporting 
await fileController.CopyAsync(
	destination: "backup/", 
	overwrite: true, 
	progress: new Progress<StorageProgress>(p => {
		Console.WriteLine($"Copied {p.BytesTransferred} of {p.TotalBytes} bytes ({p.ProgressPercentage:F2}%)");
	})
);
```

### API Overview

#### StorageController<T>

- Abstract base for storage management
- Properties: `Name`, `ParentDirectory`, `FullPath`, `CreatedAt`, `IsAvailable`, `SizeInBytes`
- Methods: `CopyAsync`, `MoveAsync`, `DeleteAsync`, `RenameAsync`, `GetSize`, `GetSizeString`

#### FileController

- Inherits from `StorageController<FileInfomation>`
- File-specific operations and metadata

#### StorageProgress

- Progress reporting for async operations
- Properties: `TotalBytes`, `BytesTransferred`, `BytesPerSecond`, `ProgressPercentage`

#### StoragePaths

- Helpers for assembly root, local app data, and executable paths

## Testing

Unit tests are provided in the `Rheo.Test` project using xUnit and Moq. To run tests:

## Requirements

- .NET 9.0 or later
- C# 13.0

## Contributing

Contributions are welcome! Please submit issues or pull requests via [GitHub](https://github.com/D-Naveenz/Rheo).

## License

This project is licensed under the MIT License.
