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
    public const int Max = Int32.MaxValue;
    public const int BlobKey = 255;
    public const int FileType = 10;
    public const int StationName = 100;
    public const int PasswordHash = 100;
    public const int InvitationCode = 10;
    public const int FileName = 1000;
    public const int Delimiter = 10;
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
    public const string File = "File";
    public const string People = "People";

    public const string Biology = "Biology";
    public const string Archeology = "Archeology";
    public const string MapStatus = "MapStatus";
    public const string CaveOther = "CaveOther";
    public const string GeologicAge = "GeologicAge";
    public const string PhysiographicProvince = "PhysiographicProvince";

    private static readonly List<string> AllProjectTags = new() { Default, Trip, Photo };

    private static readonly List<string> AllAccountTags = new()
    {
        Geology, EntranceStatus, FieldIndication, EntranceHydrology, File, People, Biology, Archeology, MapStatus,
        CaveOther, GeologicAge, PhysiographicProvince, LocationQuality
    };

    private static readonly List<string> All = new()
    {
        Default, Trip, Photo, Geology, EntranceStatus, FieldIndication, EntranceHydrology,
        File, People, Biology, Archeology, MapStatus, CaveOther, GeologicAge, PhysiographicProvince,
    };

    public static bool IsValidTagKey(string tagKey)
    {
        return All.Any(e => e == tagKey);
    }

    public static bool IsValidAccountTagKey(string tagKey)
    {
        return AllAccountTags.Any(e => e == tagKey);
    }
}