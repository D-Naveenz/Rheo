namespace Rheo.Storage.Analyzing.Models
{
    internal class PatternDefinitionMap
    {
        public Pattern? Pattern { get; set; }
        public Definition Definition { get; set; } = new();
    }
}
