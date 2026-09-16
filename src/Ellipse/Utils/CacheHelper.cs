using System.Collections;
using System.Security.Cryptography;
using System.Text;
using AngleSharp.Text;

namespace Ellipse.Utils;

public static class CacheHelper
{
     public static string CreateCacheKey(params object[] values)
    {
        StringBuilder builder = StringBuilderPool.Obtain();
        foreach (object value in values)
        {
            if (value is IEnumerable<object> enumerable)
            
                builder.AppendJoin("|", enumerable);
            else
                builder.Append(value);

            builder.Append('|');
        }

        return builder.ToPool();
    }
}