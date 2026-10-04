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
        AppendRecursively(values, builder);

        return builder.ToPool();
    }

    private static void AppendRecursively(ICollection values, StringBuilder builder)
    {
        foreach (object v in values)
        {
            if (v is ICollection collection)
            {
                builder.AppendFormat("ICollection[{0}] - (", collection.Count);
                AppendRecursively(collection, builder);
                builder.Append(')');
            }
            else
                builder.Append(v);
            builder.Append(';');
        }
    }
}