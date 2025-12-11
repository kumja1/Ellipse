using System.Text;

public static class TimeSpanExtensions
{
    public static string ToReadableString(this TimeSpan timeSpan)
    {
        StringBuilder builder = new(128);
        if (timeSpan.Days > 0)
            builder.AppendJoin(" ", timeSpan.Days, "days");
        if (timeSpan.Hours > 0)
            builder.AppendJoin(" ", timeSpan.Hours, "hours");
        if (timeSpan.Minutes > 0)
            builder.AppendJoin(" ", timeSpan.Minutes, "minutes");
        if (timeSpan.Seconds > 0)
            builder.AppendJoin(" ", timeSpan.Seconds, "seconds");

        return builder.ToString();

    }
}