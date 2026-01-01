using Rheo.Storage.FileDefinition.Models;

namespace Rheo.Storage.DefinitionsBuilder.ETL.Sluice
{
    internal static class Valuation
    {
        private const int ExtensionLevelWeight = 30;        // 0-150 points (5 levels * 30)
        private const int PatternPositionWeight = 5;        // points per pattern
        private const int SignatureStringWeight = 2;        // points per signature string
        private const int SignatureWeight = 10;             // 0-10 points
        private const int IdentifiabilityWeight = 10;       // 0-10 points

        /// <summary>
        /// Calculates the priority score for the specified definition at the given level.
        /// </summary>
        /// <param name="definition">The definition for which to calculate the priority score.</param>
        /// <param name="level">The level to use when determining the priority. Must be a non-negative integer.</param>
        /// <returns>An integer representing the calculated priority score for the definition at the specified level.</returns>
        public static int CalculatePriority(Definition definition, int level)
        {
            int totalValue = 0;
            totalValue += ValueByLevel(level);
            totalValue += ValueBySignature(definition);
            totalValue += ValueByIdentifiability(definition);
            
            return totalValue;
        }

        /// <summary>
        /// Calculates the score associated with a given extension level.
        /// </summary>
        /// <param name="level">The extension level for which to calculate the score. Must be a non-negative integer. A value of 0 indicates
        /// an uncommon extension.</param>
        /// <returns>The score corresponding to the specified extension level. Returns 0 if the level is 0.</returns>
        private static int ValueByLevel(int level)
        {
            int extensionScore = (6 - level) * ExtensionLevelWeight; // Higher level = higher score
            if (level == 0) extensionScore = 0; // Uncommon extensions get 0
            return extensionScore;
        }

        /// <summary>
        /// Calculates a weighted score based on the patterns and strings defined in the specified signature definition.
        /// </summary>
        /// <remarks>The score is determined by assigning weights to each pattern and string in the
        /// signature. Patterns at position zero receive additional weight. The final score is scaled by a signature
        /// weight constant.</remarks>
        /// <param name="definition">The <see cref="Definition"/> containing the signature whose patterns and strings are used to compute the
        /// score.</param>
        /// <returns>An integer representing the weighted score derived from the signature's patterns and strings.</returns>
        private static int ValueBySignature(Definition definition)
        {
            var score = 0;
            var patterns = definition.Signature.Patterns;
            var strings = definition.Signature.Strings;

            foreach (var pattern in patterns)
            {
                var patternScore = 1;
                if (pattern.Position == 0)
                {
                    patternScore += PatternPositionWeight;
                }
                score += patternScore;
            }

            foreach (var str in strings)
            {
                score += SignatureStringWeight;
            }

            return score * SignatureWeight;
        }

        /// <summary>
        /// Calculates the identifiability value for the specified <see cref="Definition"/>.
        /// </summary>
        /// <param name="definition">The definition to evaluate for identifiability. Cannot be <c>null</c>.</param>
        /// <returns>An integer representing the identifiability value. Returns a positive value if the definition contains one
        /// or more signature patterns; otherwise, returns 0.</returns>
        private static int ValueByIdentifiability(Definition definition)
        {
            if (definition.Signature.Patterns.Count > 0)
            {
                return IdentifiabilityWeight;
            }
            return 0;
        }
    }
}
