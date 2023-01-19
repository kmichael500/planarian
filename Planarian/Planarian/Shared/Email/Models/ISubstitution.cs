namespace Planarian.Shared.Services.Substitutions;

public interface ISubstitution
{
    public Dictionary<string, object> Substitutions { get; set; }
}