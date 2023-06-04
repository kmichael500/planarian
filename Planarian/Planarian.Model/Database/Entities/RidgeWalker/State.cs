using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class State : EntityBaseNameId
{
    public string Abbreviation { get; set; } = null!;
    public ICollection<AccountState> AccountStates { get; set; } = new HashSet<AccountState>();
    public ICollection<County> Counties { get; set; } = new HashSet<County>();
}

public class StateConfiguration : BaseEntityTypeConfiguration<State>
{
    public override void Configure(EntityTypeBuilder<State> builder)
    {
        base.Configure(builder);
    }
}