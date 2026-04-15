namespace Planarian.Shared.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class ThrottleAttribute : Attribute
{
    private int _requestsPerMinute;

    public int RequestsPerMinute
    {
        get => _requestsPerMinute;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(RequestsPerMinute), "Requests per minute must be greater than zero.");

            _requestsPerMinute = value;
        }
    }
}
