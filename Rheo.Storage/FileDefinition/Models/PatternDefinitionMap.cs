namespace Rheo.Storage.FileDefinition.Models
{
    internal class PatternDefinitionMap
    {
        public Pattern? Pattern { get; set; }
        public Definition Definition { get; set; } = new();
    }
}
