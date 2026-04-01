namespace Planarian.Shared.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class RateLimitAttribute : Attribute
{
    public RateLimitAttribute(int requestsPerMinute)
    {
        if (requestsPerMinute <= 0)
            throw new ArgumentOutOfRangeException(nameof(requestsPerMinute), "Requests per minute must be greater than zero.");

        RequestsPerMinute = requestsPerMinute;
    }

    public int RequestsPerMinute { get; }
}
