using Rheo.Storage;

namespace Rheo.Test.Models
{
    internal class StorageRecord(string name, string dirPath, OperationType operation)
    {
        public DateTimeOffset Timestamp { get; } = DateTimeOffset.Now;

        public string Name { get; } = name;

        public string DirectoryPath { get; } = dirPath;

        public OperationType Operation { get; set; } = operation;

        public StorageRecord(StorageController controller, OperationType operation)
            : this(controller.Name, controller.ParentDirectory, operation)
        {
        }
    }
}
