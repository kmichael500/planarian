using Planarian.Modules.Tags;
using Xunit;

namespace Planarian.Tests.Unit.Tags;

public sealed class TagNameMatchPolicyTests
{
    [Fact]
    public void ExactCaseMatchBeatsCaseInsensitiveMatch()
    {
        var result = TagNameMatchPolicy.Select("David Parr", "account-01",
        [
            new Candidate("people-b", "david parr", "account-01"),
            new Candidate("people-a", "David Parr", null)
        ]);

        Assert.Equal("people-a", result?.Id);
    }

    [Fact]
    public void CurrentAccountCandidateWinsWhenCasingIsEqual()
    {
        var result = TagNameMatchPolicy.Select("David Parr", "account-01",
        [
            new Candidate("people-a", "David Parr", null),
            new Candidate("people-b", "David Parr", "account-01")
        ]);

        Assert.Equal("people-b", result?.Id);
    }

    [Fact]
    public void OrdinalIdBreaksRemainingAmbiguity()
    {
        var result = TagNameMatchPolicy.Select("David Parr", "account-01",
        [
            new Candidate("people-b", "David Parr", null),
            new Candidate("people-a", "David Parr", null)
        ]);

        Assert.Equal("people-a", result?.Id);
    }

    [Fact]
    public void NoCaseInsensitiveMatchReturnsNoCandidate()
    {
        var result = TagNameMatchPolicy.Select("David Parr", "account-01",
            [new Candidate("people-a", "Jane Doe", "account-01")]);

        Assert.Null(result);
    }

    [Fact]
    public void UnicodeCaseVariantMatchesExistingCandidate()
    {
        var candidate = new Candidate("people-a", "Élodie Martin", "account-01");

        Assert.Same(candidate, TagNameMatchPolicy.Select("élodie martin", "account-01", [candidate]));
    }

    private sealed record Candidate(string Id, string Name, string? AccountId) : ITagNameMatchCandidate;
}
