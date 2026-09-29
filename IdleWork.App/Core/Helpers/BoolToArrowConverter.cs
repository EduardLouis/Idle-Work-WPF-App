// [v0.2: Helpers] Converter turning boolean expanded state into arrow indicator (▼ or ▶)
using System;
using System.Globalization;
using System.Windows.Data;

namespace IdleWork.App.Core.Helpers
{
    public class BoolToArrowConverter : IValueConverter
    {
        public static readonly BoolToArrowConverter Instance = new BoolToArrowConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
            {
                return "▼";
            }
            return "▶";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
