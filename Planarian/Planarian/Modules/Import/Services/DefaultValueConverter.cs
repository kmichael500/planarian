// using CsvHelper;
// using CsvHelper.Configuration;
// using CsvHelper.TypeConversion;
//
// namespace Planarian.Modules.Import.Services;
//
// public class DefaultValueConverter<T> : DefaultTypeConverter
// {
//     public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
//     {
//         try
//         {
//             // Delegate to the original converter for the type T
//             var converter = row.Configuration.TypeConverterCache.GetConverter<T>();
//             var result = converter.ConvertFromString(text, row, memberMapData);
//             
//             return result;
//         }
//         catch (TypeConverterException)
//         {
//             // Return default value of type T in case of conversion error
//             return default(T);
//         }
//     }
// }
