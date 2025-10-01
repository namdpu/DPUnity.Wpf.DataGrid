using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DPUnity.Wpf.DpDataGrid.Converters
{
    public class ItemStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Brush successBrush = new SolidColorBrush(Color.FromRgb(67, 160, 71));
            Brush warningBrush = new SolidColorBrush(Color.FromRgb(247, 144, 9));
            Brush dangerBrush = new SolidColorBrush(Color.FromRgb(240, 68, 56));
            Brush infoBrush = new SolidColorBrush(Color.FromRgb(21, 112, 239));
            if (value is not ItemStatus status)
                return successBrush;

            if (status == ItemStatus.Error)
            {
                return dangerBrush;
            }

            return status switch
            {
                ItemStatus.Success => successBrush,
                ItemStatus.Warning => warningBrush,
                _ => successBrush
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
