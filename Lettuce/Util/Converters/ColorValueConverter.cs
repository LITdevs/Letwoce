using System.Drawing;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Lettuce.Util.Converters;

public class ColorValueConverter() : ValueConverter<Color, int>
(
    c => c.ToArgb(),
    c => Color.FromArgb(c)
);