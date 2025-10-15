using Rheo.Storage;

namespace Rheo.Test.Models
{
    internal class TestStorage<T> where T : StorageController
    {
        private readonly List<StorageRecord> _storageRecords = [];

        public static AssertAs StorageType
        {
            get
            {
                if (typeof(T) == typeof(FileController))
                {
                    return AssertAs.File;
                }
                else if (typeof(T) == typeof(DirectoryController))
                {
                    return AssertAs.Directory;
                }
                else
                {
                    throw new InvalidOperationException("Unsupported storage type");
                }
            }
        }

        public T Content { get; private set; }

        public string Name => Content.Name;

        public string DirectoryPath => Content.ParentDirectory;

        public string FullPath => Content.FullPath;

        public DateTimeOffset LastRecordTime => _storageRecords.Count > 0 ? _storageRecords[^1].Timestamp : DateTimeOffset.MinValue;

        public TestStorage(string storagePath)
        {
            Content = CreateStorageController(storagePath, StorageType);

            // Initialize with a creation record
            _storageRecords.Add(new StorageRecord(Content, OperationType.Create));
        }

        public void Update(OperationType operation, string newPath)
        {
            Content = CreateStorageController(newPath, StorageType);
            _storageRecords.Add(new StorageRecord(Content, operation));
        }

        public static TestStorage<DirectoryController> CreatTestDirectory()
        {
            var uniqeId = Guid.NewGuid().ToString();
            // Combine the prefix, unique ID, and temporary path to create the full path
            var folderName = $"Rheo_{uniqeId}";
            var fullPath = Path.Combine(Path.GetTempPath(), folderName);

            // Create the directory at the specified path
            Directory.CreateDirectory(fullPath);

            return new TestStorage<DirectoryController>(fullPath);
        }

        private static T CreateStorageController(string path, AssertAs storageType)
        {
            var obj = Activator.CreateInstance(typeof(T), path, storageType) as T;
            if (obj is not null)
                return obj;

            throw new InvalidOperationException($"Could not create an instance of type {typeof(T).FullName}.");
        }
    }
}
