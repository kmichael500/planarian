using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Model.Database.Entities.Projects;

public class ProjectVm : IProject
{
    public ProjectVm(string id, string name, int numberOfProjectMembers, int numberOfTrips)
    {
        Id = id;
        Name = name;
        NumberOfProjectMembers = numberOfProjectMembers;
        NumberOfTrips = numberOfTrips;
    }

    public ProjectVm(Project project, int numberOfProjectMembers, int numberOfTrips)
    {
        Id = project.Id;
        Name = project.Name;
        NumberOfProjectMembers = numberOfProjectMembers;
        NumberOfTrips = numberOfTrips;
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
}