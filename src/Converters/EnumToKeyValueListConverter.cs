using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Data;

namespace DPUnity.Wpf.DpDataGrid.Converters
{
    public static class EnumExtensions
    {
        public static string GetDescription(this Enum value) =>
            value.GetType()
                 .GetField(value.ToString())!
                 .GetCustomAttribute<DescriptionAttribute>()?
                 .Description
            ?? value.ToString();
    }
    public class EnumToKeyValueListConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var enumType = value is Type { IsEnum: true }
                ? (Type)value
                : parameter is Type { IsEnum: true }
                    ? (Type)parameter
                    : null;
            if (enumType == null)
                return Array.Empty<KeyValuePair<Enum, string>>();

            return Enum
                .GetValues(enumType)
                .Cast<Enum>()
                .Select(e => new KeyValuePair<Enum, string>(e, e.GetDescription()))
                .ToList();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value as KeyValuePair<Enum, string>?)?.Key
                   ?? Binding.DoNothing;
        }
    }

    public class EnumListToKeyValueListConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not IEnumerable enumerable)
                return Array.Empty<KeyValuePair<Enum, string>>();

            var list = new List<KeyValuePair<Enum, string>>();
            foreach (var item in enumerable)
            {
                if (item is Enum enumValue)
                {
                    list.Add(new KeyValuePair<Enum, string>(enumValue, enumValue.GetDescription()));
                }
            }

            return list;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

}
