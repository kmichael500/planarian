namespace Planarian.Model.Shared;

public static class PropertyLength
{
    public const int Id = 10;
    public const int Key = 100;
    public const int Name = 100;
    public const int EmailAddress = 512;
    public const int PhoneNumber = 20;
    public const int SmallText = 50;
    public const int MediumText = 255;
    public const int BlobKey = 255;
    public const int FileType = 10;
    public const int StationName = 100;
    public const int PasswordHash = 100;
    public const int PasswordResetCode = 20;
    public const int EmailConfirmationCode = 20;
    public const int FileName = 1000;
}


public static class TagTypeKeyConstant
{
    public const string Default = "Default";
    public const string Trip = "Trip";
    public const string Photo = "Photo";
    
    public const string LocationQuality = "LocationQuality";
    public const string Geology = "Geology";
    public const string EntranceStatus = "EntranceStatus";
    public const string FieldIndication = "FieldIndication";
    public const string EntranceHydrology = "EntranceHydrology";
    public const string EntranceHydrologyFrequency = "EntranceHydrologyFrequency";
    public const string File = "File";
    private static readonly List<string> All = new() { Default, Trip, Photo, LocationQuality };

    public static bool IsValidTagKey(string tagKey)
    {
        return All.Any(e => e == tagKey);
    }
}