using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class PermissionType : EntityBaseNameId
{
}

public class PermissionTypeConfiguration : IEntityTypeConfiguration<PermissionType>
{
    public void Configure(EntityTypeBuilder<PermissionType> builder)
    {
    }
}