using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DPUnity.Wpf.DpDataGrid.Converters
{
    public class ItemStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not ItemStatus status)
                return (Brush)Application.Current.Resources["SuccessBrush"];

            if (status == ItemStatus.Error)
            {
                var dangerBrush = Application.Current.Resources["DangerBrush"] as Brush;
                var brush = dangerBrush?.CloneCurrentValue();
                brush.Opacity = 10;
                return brush;
            }

            return status switch
            {
                ItemStatus.Success => (Brush)Application.Current.Resources["SuccessBrush"],
                ItemStatus.Warning => (Brush)Application.Current.Resources["WarningBrush"],
                _ => (Brush)Application.Current.Resources["SuccessBrush"]
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
