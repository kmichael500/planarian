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
}