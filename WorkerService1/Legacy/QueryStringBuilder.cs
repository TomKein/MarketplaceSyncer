using System.Collections;
using System.Text.RegularExpressions;
using System.Web;

namespace WorkerService1.Legacy;

public class QueryStringBuilder
    {
        private readonly List<KeyValuePair<string, object>> _keyValuePairs
          = new List<KeyValuePair<string, object>>();

        /// <summary> Builds the query string from the given instance. </summary>
        public static string BuildQueryString(object queryData, string argSeperator = "&")
        {
            var encoder = new QueryStringBuilder();
            encoder.AddEntry(null, queryData, allowObjects: true);

            return encoder.GetUriString(argSeperator);
        }

        private void ReplaceAllOther(ref string s)
        {
            s = s.Replace("!", "%21")
                 .Replace("#", "%23")
                 .Replace("$", "%24")
                 .Replace("&", "%26")
                 .Replace("'", "%27")
                 .Replace("(", "%28")
                 .Replace(")", "%29")
                 .Replace("*", "%2A");
            s = Regex.Replace(s, @"%[a-f0-9]{2}", m => m.Value.ToUpperInvariant());
        }


        /// <summary>
        ///  Convert the key-value pairs that we've collected into an actual query string.
        /// </summary>
        private string GetUriString(string argSeperator)
        {
            return String.Join(argSeperator,
                               _keyValuePairs.Select(kvp =>
                               {
                                   var key = HttpUtility.UrlEncode(kvp.Key);
                                   ReplaceAllOther(ref key);
                                   //key = Regex.Replace(key, @"[()*]", m => "%" + Convert.ToString((int)m.Captures[0].Value[0], 16));
                                   //key = Regex.Replace(key, @"%[a-f0-9]{2}", m => m.Value.ToUpperInvariant());

                                   var value = HttpUtility.UrlEncode(kvp.Value.ToString());
                                   ReplaceAllOther(ref value);
                                   //value = Regex.Replace(value, @"[()*]", m => "%" + Convert.ToString((int)m.Captures[0].Value[0], 16));
                                   //value = Regex.Replace(value, @"%[a-f0-9]{2}", m => m.Value.ToUpperInvariant());

                                   //var key = Uri.EscapeDataString(kvp.Key);
                                   //var value = Uri.EscapeDataString(kvp.Value.ToString());
                                   return $"{key}={value}";



                                   //  Regex.Replace(Uri.EscapeDataString(s), "[\!*\'\(\)]", Function(m) Uri.HexEscape(Convert.ToChar(m.Value(0).ToString())))
                               }));
        }

        /// <summary> Adds a single entry to the collection. </summary>
        /// <param name="prefix"> The prefix to use when generating the key of the entry. Can be null. </param>
        /// <param name="instance"> The instance to add.
        ///  
        ///  - If the instance is a dictionary, the entries determine the key and values.
        ///  - If the instance is a collection, the keys will be the index of the entries, and the value
        ///  will be each item in the collection.
        ///  - If allowObjects is true, then the object's properties' names will be the keys, and the
        ///  values of the properties will be the values.
        ///  - Otherwise the instance is added with the given prefix to the collection of items. </param>
        /// <param name="allowObjects"> true to add the properties of the given instance (if the object is
        ///  not a collection or dictionary), false to add the object as a key-value pair. </param>
        private void AddEntry(string prefix, object instance, bool allowObjects)
        {
            var dictionary = instance as IDictionary;
            var collection = instance as ICollection;

            if (dictionary != null)
            {
                Add(prefix, GetDictionaryAdapter(dictionary));
            }
            else if (collection != null)
            {
                Add(prefix, GetArrayAdapter(collection));
            }
            else if (allowObjects)
            {
                Add(prefix, GetObjectAdapter(instance));
            }
            else
            {
                _keyValuePairs.Add(new KeyValuePair<string, object>(prefix, instance));
            }
        }

        /// <summary> Adds the given collection of entries. </summary>
        private void Add(string prefix, IEnumerable<Entry> datas)
        {
            foreach (var item in datas)
            {
                var newPrefix = String.IsNullOrEmpty(prefix)
                  ? item.Key
                  : $"{prefix}[{item.Key}]";

                AddEntry(newPrefix, item.Value, allowObjects: false);
            }
        }

        private struct Entry
        {
            public string Key;
            public object Value;
        }

        /// <summary>
        ///  Returns a collection of entries that represent the properties on the object.
        /// </summary>
        private IEnumerable<Entry> GetObjectAdapter(object data)
        {
            var properties = data.GetType().GetProperties();

            foreach (var property in properties)
            {
                yield return new Entry()
                {
                    Key = property.Name,
                    Value = property.GetValue(data)
                };
            }
        }

        /// <summary>
        ///  Returns a collection of entries that represent items in the collection.
        /// </summary>
        private IEnumerable<Entry> GetArrayAdapter(ICollection collection)
        {
            int i = 0;
            foreach (var item in collection)
            {
                yield return new Entry()
                {
                    Key = i.ToString(),
                    Value = item,
                };
                i++;
            }
        }

        /// <summary>
        ///  Returns a collection of entries that represent items in the dictionary.
        /// </summary>
        private IEnumerable<Entry> GetDictionaryAdapter(IDictionary collection)
        {
            foreach (DictionaryEntry item in collection)
            {
                yield return new Entry()
                {
                    Key = item.Key.ToString(),
                    Value = item.Value,
                };
            }
        }
    }