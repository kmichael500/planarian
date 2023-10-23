namespace Planarian.Library.Extensions.DateTime;

public static class DateTimeExtensions
{
    public static T ToUtcKind<T>(this T dateTime) where T : struct
    {
        System.DateTime dt;

        if (typeof(T) == typeof(System.DateTime))
        {
            dt = (System.DateTime)(object)dateTime;
        }
        else if (typeof(T) == typeof(System.DateTime?))
        {
            var nullableDt = (System.DateTime?)(object)dateTime;
            dt = nullableDt.Value;
        }
        else
        {
            throw new ArgumentException("Unsupported type", nameof(dateTime));
        }

        return (T)(object)System.DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }
}