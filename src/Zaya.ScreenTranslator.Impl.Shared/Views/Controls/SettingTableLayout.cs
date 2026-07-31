using System.Collections;
using Avalonia.Controls;
using Zaya.Primitives;

namespace Zaya.ScreenTranslator.Impl.Shared.Views.Controls;

/// <summary>Column geometry and row normalization for table setting editors.</summary>
internal static class SettingTableLayout
{
    public const double DeleteButtonWidth = 32;
    public const string BoolSharedSizeGroup = "TableBool";
    public const string DeleteSharedSizeGroup = "TableDelete";

    public static ColumnDefinitions BuildColumnDefinitions(IReadOnlyList<SettingDescriptor> columns)
    {
        var defs = new ColumnDefinitions();
        foreach (var col in columns)
        {
            if (col is BooleanSettingDescriptor)
            {
                defs.Add(new ColumnDefinition(GridLength.Auto)
                {
                    SharedSizeGroup = $"{BoolSharedSizeGroup}_{col.Key}",
                });
            }
            else
            {
                defs.Add(new ColumnDefinition(GridLength.Star));
            }
        }

        defs.Add(new ColumnDefinition(GridLength.Auto)
        {
            SharedSizeGroup = DeleteSharedSizeGroup,
            MinWidth = DeleteButtonWidth,
        });
        return defs;
    }

    public static ColumnDefinitions CloneColumnDefinitions(ColumnDefinitions source)
    {
        var defs = new ColumnDefinitions();
        foreach (var col in source)
        {
            defs.Add(new ColumnDefinition(col.Width)
            {
                SharedSizeGroup = col.SharedSizeGroup,
                MinWidth = col.MinWidth,
                MaxWidth = col.MaxWidth,
            });
        }

        return defs;
    }

    public static List<Dictionary<string, object>> NormalizeMutableRows(object? currentValue)
    {
        var rows = new List<Dictionary<string, object>>();
        if (currentValue is null)
            return rows;

        if (currentValue is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                if (item is Dictionary<string, object> dict)
                {
                    rows.Add(new Dictionary<string, object>(dict));
                    continue;
                }

                if (item is IReadOnlyDictionary<string, object> iro)
                {
                    rows.Add(new Dictionary<string, object>(iro));
                    continue;
                }

                if (item is IDictionary legacy)
                {
                    var converted = new Dictionary<string, object>();
                    foreach (DictionaryEntry entry in legacy)
                    {
                        if (entry.Key is string k && entry.Value is not null)
                            converted[k] = entry.Value;
                    }
                    rows.Add(converted);
                }
            }
        }

        return rows;
    }
}
