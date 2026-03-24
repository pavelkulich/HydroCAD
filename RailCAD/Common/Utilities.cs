using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RailCAD.Common
{
    internal static class Utilities
    {
        /// <summary>
        /// Determines if handle string is null (new entity not yet added to database).
        /// </summary>
        internal static bool IsNullHandle(this string handle)
        {
            return handle == null || handle == "" || handle == "0";
        }

        /// <summary>
        /// Finds out if the dictionary is empty or contains only null values.
        /// </summary>
        /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
        /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
        /// <param name="dictionary">The dictionary to check.</param>
        /// <returns>True if it is empty or contains only null values, false if it contains any non-null value.</returns>
        internal static bool IsEmpty<TKey, TValue>(this Dictionary<TKey, TValue> dictionary)
        {
            if (dictionary == null)
                return true;

            foreach (var item in dictionary)
            {
                TValue value = item.Value;

                // Check if value is an array
                if (value is Array array)
                {
                    foreach (var element in array)
                    {
                        if (element != null)
                            return false;
                    }
                }
                // Check if value is a generic list
                else if (value is System.Collections.IEnumerable enumerable && !(value is string))
                {
                    foreach (var element in enumerable)
                    {
                        if (element != null)
                            return false;
                    }
                }
                // Check simple value
                else
                {
                    if (value != null)
                        return false;
                }
            }

            return true;
        }
    }
}
