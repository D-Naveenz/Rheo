using Xunit.v3;

namespace Rheo.Storage.Test
{
    /// <summary>
    /// Specifies a category for an assembly, class, or method to support grouping and filtering in test frameworks and
    /// tooling.
    /// </summary>
    /// <remarks>Applying this attribute allows test runners and other tools to identify, group, or filter
    /// code elements based on the specified category. Multiple categories can be assigned to a single element by
    /// applying the attribute more than once.</remarks>
    /// <param name="name">The name of the category to associate with the attributed code element. Cannot be null or empty.</param>
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class CategoryAttribute(string name) : Attribute, ITraitAttribute
    {
        public string Name { get; } = name;

        public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
        {
            return
            [
                new KeyValuePair<string, string>("Category", Name)
            ];
        }
    }

    /// <summary>
    /// Specifies a feature category for an assembly, class, or method to support trait-based filtering and
    /// organization.
    /// </summary>
    /// <remarks>Use this attribute to annotate assemblies, classes, or methods with a feature name, enabling
    /// grouping and filtering in test frameworks or tooling that recognize trait attributes. Multiple instances can be
    /// applied to the same element to indicate membership in multiple feature categories.</remarks>
    /// <param name="name">The name of the feature category to associate with the attributed code element. Cannot be null or empty.</param>
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class FeatureAttribute(string name) : Attribute, ITraitAttribute
    {
        public string Name { get; } = name;

        public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
        {
            return
            [
                new KeyValuePair<string, string>("Category", Name)
            ];
        }
    }
}