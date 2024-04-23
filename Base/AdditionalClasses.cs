using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using VkNet.Model;

namespace Selen
{
    //converter: http://json2csharp.com/#

    public class AvitoObject
    {
        public string name { get; set; }
        public string id { get; set; }
        public string status { get; set; }
        public int price { get; set; }
        public string image { get; set; }
        public string Url { get; set; }
    }

    public class DromObject
    {
        public string name { get; set; }
        public string id { get; set; }
        public string status { get; set; }
        public int price { get; set; }
        public string image { get; set; }
    }

    public class IrrObject
    {
        public string name { get; set; }
        public string id { get; set; }
        public string status { get; set; }
        public int price { get; set; }
        public string image { get; set; }
    }

    public class YoulaObject
    {
        public string name { get; set; }
        public int price { get; set; }
        public string url { get; set; }
    }

    public class PriceAndAmountJS
    {
        public string status { get; set; }
        public string price { get; set; }
        public string amount { get; set; }
    }

    public class SalePriceListGoods
    {
        public string id { get; set; }
        public string price_list_id { get; set; }
        public string good_id { get; set; }
        public string measure_id { get; set; }
        public object modification_id { get; set; }
        public string updated { get; set; }
    }


    public class SalePriceListGoodPrices
    {
        public string id { get; set; }
        public string price_list_good_id { get; set; }
        public string price_type_id { get; set; }
        public string price { get; set; }
        public string updated { get; set; }
    }


    public class StoreGoods
    {
        public int id { get; set; }
        public string store_id { get; set; }
        public string good_id { get; set; }
        public string amount { get; set; }
        public string reserved { get; set; }
        public string updated { get; set; }
    }


    public class GoodsMeasures
    {
        public string id { get; set; }
        public string good_id { get; set; }
        public string measure_id { get; set; }
        public bool @base { get; set; }
        public string coefficient { get; set; }
        public string updated { get; set; }
    }

    public class Employees
    {
        public string id { get; set; }
        public string last_name { get; set; }
        public string first_name { get; set; }
        public string middle_name { get; set; }

    }
    
    public class CurrentPrices {
        public string id { get; set; }
        public string good_id { get; set; }
        public string modification_id { get; set; }
        public string measure_id { get; set; }
        public string price_type_id { get; set; }
        public string price { get; set; }
        public string updated { get; set; }
    }

//=============================================================================
    public class AutoObject
    {
        public string name { get; set; }
        public string id { get; set; }
        public bool status { get; set; }
        public int price { get; set; }
        public string image { get; set; }
    }
    public class StrComp
    {
        public int ind { get; set; }
        public double sim { get; set; }
    }
    public class PartnerContactinfoClass
    {
        public int id { get; set; }
        public int partner_id { get; set; }
        public int contact_info_type_id { get; set; }
        public string contact_info { get; set; }
        public string note { get; set; }
    }

    public class PostSuccessAnswer
    {
        public string id { get; set; }
        public string updated { get; set; }
    }

    public class GoodIds
    {
        public string good_id { get; set; }
    }
}
