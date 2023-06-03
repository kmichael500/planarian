using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Model.Database.Entities.Projects;

public class ProjectVm : IProject
{
    public ProjectVm(string id, string name, int numberOfProjectMembers, int numberOfTrips, DateTime createdOn,
        DateTime? modifiedOn)
    {
        Id = id;
        Name = name;
        NumberOfProjectMembers = numberOfProjectMembers;
        NumberOfTrips = numberOfTrips;
        CreatedOn = createdOn;
        ModifiedOn = modifiedOn;
    }

    public ProjectVm(Project project, int numberOfProjectMembers, int numberOfTrips, DateTime createdOn,
        DateTime? modifiedOn) : this(project.Id, project.Name, numberOfProjectMembers, numberOfTrips, createdOn, modifiedOn)
    {
    }

    public ProjectVm()
    {
    }

    [Required]
    [MaxLength(PropertyLength.Id)]
    public string Id { get; set; } = null!;

    public int NumberOfProjectMembers { get; set; }
    public int NumberOfTrips { get; set; }
    [Required] public string Name { get; set; } = null!;
    public DateTime CreatedOn { get; set; }
    public DateTime? ModifiedOn { get; set; } = null!;
    
}