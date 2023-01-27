namespace Planarian.Model.Database.Entities.Leads;

public class LeadVm
{
    public LeadVm(string id, string description, string classification, string closestStation)
    {
        Description = description;
        Classification = classification;
        ClosestStation = closestStation;
        Id = id;
    }

    public LeadVm(Lead lead) : this(lead.Id, lead.Description, lead.Classification, lead.ClosestStation)
    {
    }

    public LeadVm()
    {
    }

    public string Id { get; set; }
    public string Description { get; set; }
    public string Classification { get; set; }
    public string ClosestStation { get; set; }
}