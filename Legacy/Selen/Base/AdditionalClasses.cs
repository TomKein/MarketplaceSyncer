using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using Selen.Tools;
using VkNet.Model;

//converter: http://json2csharp.com/#

namespace Selen {

    public static class Extantions {
        public static bool Replace(this GoodObject b, string pattern, string value = "") {
            if (b.description?.Contains(pattern) ?? false) {
                b.description = b.description.Replace(pattern, value);
                Log.Add($"Erase: {b.name} - замена {pattern} -> {value}");
                return true;
            }
            return false;
        }
    }
    public class PartnerContactinfoClass {
        public int id { get; set; }
        public int partner_id { get; set; }
        public int contact_info_type_id { get; set; }
        public string contact_info { get; set; }
        public string note { get; set; }
    }
}