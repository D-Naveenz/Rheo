namespace Rheo.Storage.MIME.Models
{
    internal class PatternDefinitionMap
    {
        public Pattern? Pattern { get; set; }
        public Definition Definition { get; set; } = new();
    }
}
