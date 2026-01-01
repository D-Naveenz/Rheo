using Rheo.Storage.FileDefinition.Models;

namespace Rheo.Storage.DefinitionsBuilder.ETL.RIFF
{
    public class TrIDDefinition
    {
        public string FileType { get; set; } = "";
        public string Extension { get; set; } = "";
        public string MimeType { get; set; } = "";
        public string FileName { get; set; } = "";
        public int Tag { get; set; }
        public string Remarks { get; set; } = "";
        public string ReferenceUrl { get; set; } = "";
        public string User { get; set; } = "";
        public string Email { get; set; } = "";
        public string Home { get; set; } = "";
        public int FileCount { get; set; }
        public List<Pattern> Patterns { get; set; } = [];
        public List<byte[]> Strings { get; set; } = [];
    }
}
