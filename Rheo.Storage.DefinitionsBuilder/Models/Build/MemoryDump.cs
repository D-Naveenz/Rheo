namespace Rheo.Storage.DefinitionsBuilder.Models.Build
{
    public class MemoryDump
    {
        public string Category { get; set; } = string.Empty;
        public List<string> MimeTypes { get; set; } = [];
        public List<string> Extentions { get; set; } = [];
    }
}
