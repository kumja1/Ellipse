using System.Text;

public static class TimeSpanExtensions
{
    public static string ToReadableString(this TimeSpan timeSpan)
    {
        StringBuilder builder = new(128);
        if (timeSpan.Days > 0)
        {
            builder.AppendJoin(" ", timeSpan.Days, "days");
            builder.Append(' ');
        }

        if (timeSpan.Hours > 0)
        {
            builder.AppendJoin(" ", timeSpan.Hours, "hours");
            builder.Append(' ');
        }
        
        if (timeSpan.Minutes > 0)
        {
            builder.AppendJoin(" ", timeSpan.Minutes, "minutes");
            builder.Append(' ');
        }

        if (timeSpan.Seconds > 0)
            builder.AppendJoin(" ", timeSpan.Seconds, "seconds");
        
        return builder.ToString();
    }
}