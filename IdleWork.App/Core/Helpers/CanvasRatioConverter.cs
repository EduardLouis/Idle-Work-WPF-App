// [v0.2: Helpers] Converter multiplying ratio (0.0 - 1.0) by a base timeline width (e.g. 800)
using System;
using System.Globalization;
using System.Windows.Data;

namespace IdleWork.App.Core.Helpers
{
    public class CanvasRatioConverter : IValueConverter
    {
        public static readonly CanvasRatioConverter Instance = new CanvasRatioConverter();
        public const double DefaultBaseWidth = 850.0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double ratio)
            {
                double baseWidth = DefaultBaseWidth;
                if (parameter != null && double.TryParse(parameter.ToString(), out double parsed))
                {
                    baseWidth = parsed;
                }
                return ratio * baseWidth;
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
