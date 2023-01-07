namespace Planarian.Modules.TripObjectives.Controllers;

public class CreateLeadVm
{
    public CreateLeadVm(string description, string classification, string closestStation)
    {
        Description = description;
        Classification = classification;
        ClosestStation = closestStation;
    }
    
    public CreateLeadVm(){}

    public string Description { get; set; }
    public string Classification { get; set; }
    public string ClosestStation { get; set; }
}