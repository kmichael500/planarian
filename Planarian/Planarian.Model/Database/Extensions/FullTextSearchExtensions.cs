using Microsoft.EntityFrameworkCore;

namespace Planarian.Model.Database.Extensions;

public static class FullTextSearchExtensions
{
    [DbFunction("ts_headline_simple", "public")] 
    public static string TsHeadlineSimple(
        string config,
        string document,
        string query,
        string options)           
        => throw new NotSupportedException(); // this isn't actually executed
    
    [DbFunction("fts_matches_websearch", "public")]
    public static bool WebSearchMatch(
        string config,
        string document,
        string query) => throw new NotSupportedException(); // this isn't actually executed
}