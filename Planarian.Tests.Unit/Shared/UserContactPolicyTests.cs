using Planarian.Library.Extensions.String;
using Planarian.Model.Shared;
using Xunit;

namespace Planarian.Tests.Unit.Shared;

public sealed class UserContactPolicyTests
{
    [Theory]
    [InlineData("person@example.com", true)]
    [InlineData("person+tag@example.co.uk", true)]
    [InlineData("not-an-email", false)]
    [InlineData("", false)]
    public void LibraryEmailExtensionMatchesModelPolicy(string email, bool expected)
    {
        Assert.Equal(expected, UserContactPolicy.IsValidEmail(email));
        Assert.Equal(expected, email.IsValidEmail());
    }

    [Theory]
    [InlineData("6155551212", "+16155551212")]
    [InlineData("(615) 555-1212", "+16155551212")]
    [InlineData("+1 615 555 1212", "+16155551212")]
    public void LibraryPhoneNormalizationMatchesModelPolicy(string input, string expected)
    {
        Assert.Equal(expected, UserContactPolicy.NormalizePhoneNumber(input));
        Assert.Equal(expected, input.ExtractPhoneNumber());
        Assert.True(UserContactPolicy.IsValidPhoneNumber(expected));
        Assert.True(expected.IsValidPhoneNumber());
    }

    [Fact]
    public void InvalidPhoneStillThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => UserContactPolicy.NormalizePhoneNumber("123"));
        Assert.Throws<ArgumentException>(() => "123".ExtractPhoneNumber());
    }
}
