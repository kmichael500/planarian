namespace Planarian.Library.Constants;

public static class RegularExpressions
{
    public const string PasswordValidation =
        @"^(?=.{8,})(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$|^.{15,}$";
}