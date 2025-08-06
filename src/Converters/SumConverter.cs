using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace DPUnity.Wpf.DpDataGrid.Converters
{
    public class SumConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2 || values[0] is not CollectionViewGroup group || values[1] is not string columnName)
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(columnName) || group.ItemCount == 0)
            {
                return string.Empty;
            }

            var firstItem = group.Items[0];
            var propertyInfo = firstItem.GetType().GetProperty(columnName);

            if (propertyInfo == null || !IsNumericType(propertyInfo.PropertyType))
            {
                return string.Empty;
            }

            try
            {
                decimal sum = group.Items
                        .Select(item => Math.Round(System.Convert.ToDecimal(propertyInfo.GetValue(item)), 0, MidpointRounding.AwayFromZero))
                        .Sum();

                return string.Format("Sum {0}: {1:N}", columnName, sum);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsNumericType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type == typeof(byte) || type == typeof(sbyte) ||
                   type == typeof(short) || type == typeof(ushort) ||
                   type == typeof(int) || type == typeof(uint) ||
                   type == typeof(long) || type == typeof(ulong) ||
                   type == typeof(float) || type == typeof(double) ||
                   type == typeof(decimal);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
