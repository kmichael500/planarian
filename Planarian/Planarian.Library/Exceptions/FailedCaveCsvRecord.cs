namespace Planarian.Library.Exceptions;

public class FailedCaveCsvRecord<T>
{
    public FailedCaveCsvRecord(T rowData, int rowNumber, string reason)
    {
        CaveCsvModel = rowData;
        RowNumber = rowNumber;
        Reason = reason;
    }

    public T CaveCsvModel { get; set; }
    public int RowNumber { get; set; }
    public string Reason { get; set; }
}