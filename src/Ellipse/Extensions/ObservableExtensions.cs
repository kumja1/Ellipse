
using System.Collections.ObjectModel;

namespace Ellipse.Extensions;

public static class ObservableExtensions
{
 

    public static ObservableCollection<T> Sort<T>(this ObservableCollection<T> collection, Comparison<T> comparison)
    {
        var sortableList = new List<T>(collection);
        sortableList.Sort(comparison);
        collection.Clear();
        sortableList.ForEach(collection.Add);
        return collection;
    }

}