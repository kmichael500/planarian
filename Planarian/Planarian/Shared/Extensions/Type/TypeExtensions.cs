using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;

namespace Planarian.Shared.Extensions.Type;

public static class TypeExtensions
{
    public static string GenerateSqlTableScript(this System.Type entityType, string tableName, bool isTemporary = false)
    {
        if (isTemporary)
        {
            tableName = $"#{tableName}";
        }

        var createTableScript = new StringBuilder($"CREATE TABLE {tableName} (");
        var primaryKeyColumns = new List<string>();

        var properties = entityType.GetProperties().ToList();
        foreach (var property in properties)
        {
            bool isNullable = Nullable.GetUnderlyingType(property.PropertyType) != null ||
                              (property.PropertyType == typeof(string)); // Strings are inherently nullable

            string? sqlType = property.PropertyType.ToSqlServerType();

            if (string.IsNullOrWhiteSpace(sqlType)) continue;

            // Check for MaxLength attribute
            var maxLengthAttr = property.GetCustomAttribute<MaxLengthAttribute>();
            if (maxLengthAttr != null && property.PropertyType == typeof(string))
            {
                sqlType = $"NVARCHAR({maxLengthAttr.Length})";
            }

            // Add NOT NULL constraint only if the property is not nullable and it doesn't have a [Required] attribute
            if (!isNullable && property.GetCustomAttribute<RequiredAttribute>() == null)
            {
                sqlType += " NOT NULL";
            }

            // Check for Key attribute (indicating a primary key)
            if (property.GetCustomAttribute<KeyAttribute>() != null)
            {
                primaryKeyColumns.Add(property.Name);
            }

            createTableScript.Append($"{property.Name} {sqlType}, ");
        }

        if (primaryKeyColumns.Count > 0)
        {
            createTableScript.Append($"PRIMARY KEY ({string.Join(", ", primaryKeyColumns)}), ");
        }

        // Remove trailing comma and close parenthesis
        createTableScript.Length -= 2;
        createTableScript.Append(")");

        return createTableScript.ToString();

    }

    private static string? ToSqlServerType(this System.Type clrType)
    {
        // If the type is nullable, get its underlying type
        var actualType = Nullable.GetUnderlyingType(clrType) ?? clrType;

        return actualType switch
        {
            { } t when t == typeof(string) => "NVARCHAR(MAX)",
            { } t when t == typeof(int) => "INT",
            { } t when t == typeof(long) => "BIGINT",
            { } t when t == typeof(short) => "SMALLINT",
            { } t when t == typeof(byte) => "TINYINT",
            { } t when t == typeof(bool) => "BIT",
            { } t when t == typeof(DateTime) => "DATETIME",
            { } t when t == typeof(DateTimeOffset) => "DATETIMEOFFSET",
            { } t when t == typeof(decimal) => "DECIMAL(18, 2)", // Adjust precision and scale as needed
            { } t when t == typeof(double) => "FLOAT",
            { } t when t == typeof(float) => "REAL",
            { } t when t == typeof(Guid) => "UNIQUEIDENTIFIER",
            { } t when t == typeof(byte[]) => "VARBINARY(MAX)",
            { } t when t.IsEnum => "INT", // Enums are typically stored as integers,
            { } t when t == typeof(NetTopologySuite.Geometries.Point) => "GEOGRAPHY",

            _ => null
        };
    }
}