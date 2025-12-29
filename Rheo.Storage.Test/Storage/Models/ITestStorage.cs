namespace Rheo.Storage.Test.Storage.Models
{
    internal interface ITestStorage
    {
        /// <summary>
        /// Gets the collection of storage records.
        /// </summary>
        public List<StorageRecord> StorageRecords { get; }

        /// <summary>
        /// Gets the timestamp of the most recent record.
        /// </summary>
        public DateTimeOffset LastRecordTime { get; }

        /// <summary>
        /// Updates the current state based on the specified operation type.
        /// </summary>
        /// <param name="operation">The type of operation to apply. This determines how the state will be updated.</param>
        public void Update(OperationType operation);
    }
}
