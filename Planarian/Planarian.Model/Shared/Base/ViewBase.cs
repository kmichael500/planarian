using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planarian.Model.Shared.Base;

public abstract class ViewBase
{
    public const string SqlViewClrTypeAnnotation = "Planarian:SqlView:ClrType";
    public const string SqlViewHashAnnotation = "Planarian:SqlView:Hash";
    public const string SqlViewSqlAnnotation = "Planarian:SqlView:Sql";
    public const string SqlViewViewNameAnnotation = "Planarian:SqlView:ViewName";

    private const string ViewSuffix = "View";
    private const string SqlResourceRoot = "Planarian.Model.Database.Views";

    protected ViewBase()
    {
        EnsureViewTypeName(GetType());
    }

    public static string GetViewName<TView>() where TView : ViewBase
    {
        return GetViewName(typeof(TView));
    }

    public static string GetViewName(Type viewType)
    {
        EnsureViewTypeName(viewType);
        var name = viewType.Name;
        return name[..^ViewSuffix.Length];
    }

    private static void EnsureViewTypeName(Type viewType)
    {
        var name = viewType.Name;
        if (!name.EndsWith(ViewSuffix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"View entity type '{name}' must end with '{ViewSuffix}' to use the SQL view convention.");
        }
    }

    public static string GetSqlResourceName<TView>() where TView : ViewBase
    {
        return GetSqlResourceName(typeof(TView));
    }

    public static string GetSqlResourceName(Type viewType)
    {
        var viewName = GetViewName(viewType);
        return $"{SqlResourceRoot}.{viewName}.sql";
    }

    public static string GetDropViewSql<TView>() where TView : ViewBase
    {
        return $@"DROP VIEW IF EXISTS ""{GetViewName<TView>()}"";";
    }

    public static string ReadSqlResource<TView>() where TView : ViewBase
    {
        return ReadSqlResource(typeof(TView));
    }

    public static string ReadSqlResource(Type viewType)
    {
        if (!typeof(ViewBase).IsAssignableFrom(viewType))
        {
            throw new InvalidOperationException($"Type '{viewType.FullName}' must inherit from '{nameof(ViewBase)}'.");
        }

        var assembly = viewType.Assembly;
        var resourceName = GetSqlResourceName(viewType);
        using var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
        {
            var availableResources = string.Join(Environment.NewLine, assembly.GetManifestResourceNames());
            throw new InvalidOperationException(
                $"SQL resource '{resourceName}' was not found in assembly '{assembly.GetName().Name}'. " +
                $"Available resources:{Environment.NewLine}{availableResources}");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static string GetSqlHash<TView>() where TView : ViewBase
    {
        return GetSqlHash(typeof(TView));
    }

    public static string GetSqlHash(Type viewType)
    {
        var sql = ReadSqlResource(viewType);
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sql));
        return Convert.ToHexString(hash);
    }
}

public abstract class ViewTypeConfiguration<TView> : IEntityTypeConfiguration<TView> where TView : ViewBase
{
    public void Configure(EntityTypeBuilder<TView> builder)
    {
        builder.HasNoKey();
        builder.ToView(ViewBase.GetViewName<TView>());
        builder.HasAnnotation(ViewBase.SqlViewClrTypeAnnotation, typeof(TView).FullName);
        builder.HasAnnotation(ViewBase.SqlViewHashAnnotation, ViewBase.GetSqlHash<TView>());
        builder.HasAnnotation(ViewBase.SqlViewSqlAnnotation, ViewBase.ReadSqlResource<TView>());
        builder.HasAnnotation(ViewBase.SqlViewViewNameAnnotation, ViewBase.GetViewName<TView>());
        ConfigureView(builder);
    }

    protected abstract void ConfigureView(EntityTypeBuilder<TView> builder);
}
