namespace Rheo.Storage.Test.Storage.Models
{
    /// <summary>
    /// Specifies the type of operation to be performed.
    /// </summary>
    /// <remarks>This enumeration defines the set of operations that can be performed, such as creating,
    /// copying, moving, deleting, or renaming an item. It is typically used to indicate the desired action in methods
    /// or workflows that handle file or object operations.</remarks>
    internal enum OperationType
    {
        Create,
        Copy,
        Move,
        Delete,
        Rename
    }

    internal enum ResourceType
    {
        Document,
        Video,
        Text,
        Image,
        Binary
    }
}
