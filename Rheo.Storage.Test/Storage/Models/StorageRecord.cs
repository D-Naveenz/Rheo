namespace Rheo.Storage.Test.Storage.Models
{
    /// <summary>
    /// Represents a record of storage-related information, including metadata such as the name, directory path, 
    /// timestamp, and the type of operation associated with the record.
    /// </summary>
    /// <remarks>This class is designed to encapsulate the details of a storage operation, including its
    /// creation timestamp,  the associated directory path, and the operation type. Instances of this class are
    /// immutable except for the  <see cref="Operation"/> property, which can be updated to reflect changes in the
    /// operation type.</remarks>
    /// <param name="name"></param>
    /// <param name="dirPath"></param>
    /// <param name="operation"></param>
    internal class StorageRecord(string name, string dirPath, OperationType operation)
    {
        /// <summary>
        /// Gets the timestamp indicating when the object was created or initialized.
        /// </summary>
        public DateTimeOffset Timestamp { get; } = DateTimeOffset.Now;

        /// <summary>
        /// Gets the name associated with the current instance.
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// Gets the path of the directory associated with the current instance.
        /// </summary>
        public string DirectoryPath { get; } = dirPath;

        /// <summary>
        /// Gets or sets the type of operation to be performed.
        /// </summary>
        public OperationType Operation { get; set; } = operation;

        public StorageRecord(StorageController controller, OperationType operation)
            : this(controller.Name, controller.ParentDirectory, operation)
        {
        }
    }
}
