namespace Planarian.Model.Database.Extensions;

public static class TsqueryBuilder
{
    /// <summary>
    /// Converts a user-supplied search query into a tsquery string.
    /// - Quoted phrases (e.g. "breakdown beyond") become a sequence joined by <->,
    ///   enforcing that the words appear in order.
    /// - Unquoted words get a fuzzy (prefix) match appended (e.g. word:*).
    /// - Simple boolean words (and, or, not) are translated into tsquery operators.
    /// </summary>
    /// <param name="searchQuery">The raw query string from the user.</param>
    /// <returns>A tsquery string ready for use with to_tsquery.</returns>
    public static string BuildTsquery(string searchQuery)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
            return "";

        var queryParts = new List<string>();

        // 1. Process quoted phrases.
        // Regex: match text in double quotes.
        var quotedPattern = "\"([^\"]+)\"";
        var quotedMatches = Regex.Matches(searchQuery, quotedPattern);

        foreach (Match match in quotedMatches)
        {
            string phrase = match.Groups[1].Value;
            // Split on whitespace and remove empty parts.
            var tokens = phrase.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length > 0)
            {
                // Join tokens with the positional (<->) operator.
                var phraseQuery = string.Join(" <-> ", tokens.Select(token => token.ToLowerInvariant()));
                queryParts.Add($"({phraseQuery})");
            }
        }

        // 2. Remove all quoted phrases from the original query.
        var remainingQuery = Regex.Replace(searchQuery, quotedPattern, "").Trim();

        // 3. Process the remaining (unquoted) text.
        if (!string.IsNullOrWhiteSpace(remainingQuery))
        {
            // Split by whitespace.
            var tokens = remainingQuery.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var processedTokens = new List<string>();

            foreach (var token in tokens)
            {
                // Lowercase token for consistency.
                var lower = token.ToLowerInvariant();

                // Translate simple boolean words (you can expand this further)
                if (lower == "and")
                {
                    processedTokens.Add("&");
                }
                else if (lower == "or")
                {
                    processedTokens.Add("|");
                }
                else if (lower == "not")
                {
                    processedTokens.Add("!");
                }
                else
                {
                    // Append fuzzy (prefix) matching operator. Adjust or remove :* if strict matching is desired.
                    processedTokens.Add($"{lower}:*");
                }
            }

            // Group the free text tokens together.
            var freeTextQuery = string.Join(" ", processedTokens);
            queryParts.Add($"({freeTextQuery})");
        }

        // 4. Combine the parts using the & operator (logical AND).
        // This means that each group (quoted phrase AND unquoted text) must be present.
        var finalQuery = string.Join(" & ", queryParts);

        return finalQuery;
    }
}