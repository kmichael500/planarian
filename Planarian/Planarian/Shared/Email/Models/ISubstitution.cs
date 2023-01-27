namespace Planarian.Shared.Email.Models;

public interface ISubstitution
{
    public Dictionary<string, object> Substitutions { get; set; }
}