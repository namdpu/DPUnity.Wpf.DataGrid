using DPUnity.Wpf.UI.Controls.PackIcon;
using System;
using System.Globalization;
using System.Windows.Data;

namespace DPUnity.Wpf.DpDataGrid.Converters
{
    public class ItemStatusToIconKindConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ItemStatus status)
            {
                return status switch
                {
                    ItemStatus.None => PackIconKind.None,
                    ItemStatus.Success => PackIconKind.SuccessThick,
                    ItemStatus.Warning => PackIconKind.Warning,
                    ItemStatus.Error => PackIconKind.CloseThick,
                    _ => PackIconKind.None
                };
            }
            return PackIconKind.None;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}