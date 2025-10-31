using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Lettuce.Util.Converters;

public class DateTimeOffsetConverter() : ValueConverter<DateTimeOffset, DateTimeOffset>
(
    d => d.ToUniversalTime(),
    d => d.ToUniversalTime()
);