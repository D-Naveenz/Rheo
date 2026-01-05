namespace Rheo.Storage
{
    /// <summary>
    /// Represents the progress of a storage operation, including total bytes, transferred bytes, transfer speed, and progress percentage.
    /// </summary>
    public class StorageProgress
    {
        /// <summary>
        /// Gets or sets the total number of bytes to be transferred.
        /// </summary>
        public long TotalBytes { get; set; }

        /// <summary>
        /// Gets or sets the number of bytes that have been transferred so far.
        /// </summary>
        public long BytesTransferred { get; set; }

        /// <summary>
        /// Gets or sets the current transfer speed in bytes per second.
        /// </summary>
        public double BytesPerSecond { get; set; }

        /// <summary>
        /// Gets the percentage of progress based on bytes transferred and total bytes.
        /// </summary>
        public double ProgressPercentage => TotalBytes == 0 ? 0 : (double)BytesTransferred / TotalBytes * 100;
    }
}
