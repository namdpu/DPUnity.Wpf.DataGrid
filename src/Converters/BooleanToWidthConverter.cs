using System;
using System.Globalization;
using System.Windows.Data;

namespace DPUnity.Wpf.DpDataGrid.Converters
{
    /// <summary>
    /// Converter to convert boolean to width for row header
    /// </summary>
    public class BooleanToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool showRowsCount)
            {
                return showRowsCount ? 40.0 : 0.0;
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
