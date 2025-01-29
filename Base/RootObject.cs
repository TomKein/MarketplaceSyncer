using Newtonsoft.Json;
using Selen.Base;
using Selen.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Selen {

    public class reservations {
        public string id { get; set; }
        public string number { get; set; }
        public string comment { get; set; }
        public string updated { get; set; }
        public string responsible_employee_id { get; set; }
        public string author_employee_id { get; set; }
    }

    public class realizationgoods {
        public string id { get; set; }
        public string good_id { get; set; }
        public string updated { get; set; }
    }

    public class Goodsattributes {
        public string id { get; set; }
        public string good_id { get; set; }
        public string attribute_id { get; set; }
        public string value_id { get; set; }
        public string value { get; set; }
        public string updated { get; set; }

    }
    public class Attribute {
        public string id { get; set; }
        public string model { get; set; }
        public string name { get; set; }

    }
    public class Value {
        public string id { get; set; }
        public string model { get; set; }
        public string name { get; set; }
        public string value { get; set; }

    }
    public class Attributes {
        public Attribute Attribute { get; set; }
        public Value Value { get; set; }

    }
    public class GoodGroupsObject {
        public string id { get; set; }
        public string name { get; set; }
        public string parent_id { get; set; }
        public string updated { get; set; }
    }

    public class Store {
        public string id { get; set; }
        public string model { get; set; }
        public string name { get; set; }
    }

    public class Amount {
        public string total { get; set; }
        public string reserved { get; set; }
    }

    public class Remains {
        public Store store { get; set; }
        public Amount amount { get; set; }
    }

    public class RemainGoods {
        public string id { get; set; }
        public string remain_id { get; set; }
        public string good_id { get; set; }
        public string amount { get; set; }
        public string measure_id { get; set; }
        public string price { get; set; }
        public string sum { get; set; }
        public string default_order { get; set; }
        public string updated { get; set; }

        public float FloatAmount {
            get {
                return string.IsNullOrEmpty(amount) ? 0
                    : float.Parse(amount.Replace(".", ","));
            }
        }
        public float FloatPrice {
            get {
                return string.IsNullOrEmpty(price) ? 0
                    : float.Parse(price.Replace(".", ","));
            }
        }
    }

    public class Postings {
        public Store store { get; set; }
        public Amount amount { get; set; }
    }

    public class PostingGoods {
        public string id { get; set; }
        public string posting_id { get; set; }
        public string good_id { get; set; }
        public string amount { get; set; }
        public string measure_id { get; set; }
        public string price { get; set; }
        public string sum { get; set; }
        public string updated { get; set; }

        public float Amount {
            get {
                return string.IsNullOrEmpty(amount) ? 0
                    : float.Parse(amount.Replace(".", ","));
            }
        }
        public float Price {
            get {
                return string.IsNullOrEmpty(price) ? 0
                    : float.Parse(price.Replace(".", ","));
            }
        }
    }

    public class BusImage {
        public string name { get; set; }
        public string url { get; set; }
    }

    public class Price_type {
        public string id { get; set; }
        public string model { get; set; }
        public string name { get; set; }
    }

    public class Prices {
        public Price_type price_Type { get; set; }
        public string price { get; set; }
    }

    public class GoodObject {
        public string id { get; set; }
        public string name { get; set; }
        public string full_name { get; set; }
        public string group_id { get; set; }
        public string part { get; set; }
        public string store_code { get; set; }
        public bool archive { get; set; }
        public string description { get; set; }
        public string updated { get; set; }
        public string updated_remains_prices { get; set; }
        public List<BusImage> images { get; set; }
        public List<Remains> remains { get; set; }
        public List<Prices> prices { get; set; }
        public List<Attributes> attributes { get; set; }
        public string drom { get; set; }
        public string vk { get; set; }
        public string ozon { get; set; }
        public string wb;
        [JsonIgnore]
        public string WB { 
            get {
                if (wb == null)
                    return GetWB();
                return wb;
            } 
            set { 
                if (wb != null) wb = value;
                else SetWB(value);
            } }
        public string measure_id { get; set; }
        public float? weight { get; set; }
        public float? volume { get; set; }
        public string length { get; set; }
        public string width { get; set; }
        public string height { get; set; }
        public string hscode_id { get; set; }
        public static List<GoodGroupsObject> Groups { get; set; }

        static bool useReserve = DB.GetParamBool("useReserve");

        [JsonIgnore]
        static readonly string _autoMarks = @"..\data\auto.txt";

        [JsonIgnore]
        public DateTime Updated {
            get {
                try {
                    var update = new DateTime[]{
                        DateTime.Parse(updated),
                        DateTime.Parse(updated_remains_prices??updated)};
                    return update.Max();
                } catch (Exception x) {
                    Log.Add($"GoodObject.Updated: ошибка! id = {id}, updated = {updated}, updated_remains = {updated_remains_prices}, {x.Message}");
                }
                return DateTime.Now;
            }
        }
        [JsonIgnore]
        public int Price {
            get {
                if (prices.Count > 0)
                    return prices.Select(s => string.IsNullOrEmpty(s.price) ? 0 :
                                                (int) float.Parse(s.price.Replace(".", ","))).Max();
                return 0;
            }
            set {
                prices = new List<Prices> { new Prices { price = value.ToString() } };
            }
        }
        [JsonIgnore]
        public float Amount {
            get {
                float total = remains.Select(s => string.IsNullOrEmpty(s.amount.total) ?
                                                0 :
                                                float.Parse(s.amount.total.Replace(".", ","))).Sum();
                var am = total - Reserve;
                return am < 0 ? 0 : am;
            }
            set {
                remains = new List<Remains> {
                    new Remains { amount = new Amount{
                        total = value.ToString("F0")
                    } }
                };
            }
        }

        [JsonIgnore]
        public float Reserve {
            get {
                float reserved = 0;
                if (useReserve) {
                    reserved = remains.Select(s => string.IsNullOrEmpty(s.amount.reserved) ?
                                                    0 :
                                                    float.Parse(s.amount.reserved.Replace(".", ","))).Sum();
                }
                return reserved;
            }
            set {
                if (!remains.Any()) {
                    remains = new List<Remains> {
                        new Remains { amount = new Amount() }
                    };
                }
                remains[0].amount.reserved = value.ToString("F0");
            }
        }
        //Артикул
        [JsonIgnore]
        public string Part => part?.Split('\\')?.First()
                                   .Split('/').First()
                                   .Split(',').First();
        //Название единиц измерения
        [JsonIgnore]
        public string MeasureName => measure_id == "11" ? "пара"
                                                        : measure_id == "13" ? "компл."
                                                                             : "шт.";
        public string MeasureNameCorrect => measure_id == "11"
                                                ? Amount % 10 == 1 && Amount != 11
                                                    ? "пара"
                                                    : Amount % 10 >= 1 && Amount % 10 <= 4 && (Amount > 20 || Amount < 10)
                                                        ? "пары"
                                                        : "пар"
                                            : measure_id == "13"
                                                ? "компл."
                                                : "шт.";
        //вес товара по умолчанию
        static float defaultWeight;
        public static void UpdateDefaultWeight() {
            defaultWeight = DB.GetParamFloat("defaultWeigth");
            if (defaultWeight == 0) {
                Log.Add("defaultWeigth: ошибка - значение в настройках 0! установлено значение 1 кг");
                defaultWeight = 1f;
            }
        }
        [JsonIgnore]
        public string WeightString => Weight.ToString("F1").Replace(",", ".");
        [JsonIgnore]
        public float Weight => (float) ((weight == null || weight == 0) ? defaultWeight : weight);
        //объем товара по умолчанию
        static float defaultVolume;
        public static void UpdateDefaultVolume() {
            defaultVolume = DB.GetParamFloat("defaultVolume");
            if (defaultVolume == 0) {
                Log.Add("defaultVolume: ошибка - значение в настройках 0! установлено значение 0.02 м3");
                defaultVolume = 0.02f;
            }
        }
        [JsonIgnore]
        public float SumDimentions => GetDimentions().Sum();
        [JsonIgnore]
        public float MaxDimention => GetDimentions().Max();
        [JsonIgnore]
        public float MultiDimentions => GetDimentions().Aggregate((x1, x2) => x1 * x2);
        [JsonIgnore]
        public float VolumeWeight => MultiDimentions / 5000;
        //срок годности по умолчанию
        static string defaultValidity;
        public int SizeMM(string direction, int MinSize) {
            string stringValue;
            if (direction == "length")
                stringValue = length;
            else if (direction == "width")
                stringValue = width;
            else if (direction == "height")
                stringValue = height;
            else
                stringValue = "0";
            var intValue = (int) (float.Parse(stringValue.Replace(".", ",")) * 10);
            if (intValue < MinSize)
                return MinSize;
            return intValue;
        }
        public int SizeSM(string direction, int MinSize) {
            return SizeMM(direction, MinSize*10) / 10;
        }
        public static void UpdateDefaultValidity() {
            var validity = DB.GetParamStr("defaultValidity");
            if (string.IsNullOrEmpty(validity)) {
                Log.Add("defaultValidity: ошибка - значение в настройках 0! установлено значение 1 год");
                defaultValidity = "P1Y";
            } else
                defaultValidity = "P" + validity + "Y";
        }
        //Атрибут WB
        public string GetWB() {
            var atrId = Class365API._attributesForGoods.First(a => a.name.StartsWith("WB.RU"))?.id;
            if (atrId != null) {
                var atribute = attributes?.Find(f => f.Attribute.id == atrId);
                if (atribute != null && atribute.Value.value != "")
                    return atribute.Value.value;
            }
            return null;
        }
        public async Task<bool> SetWB(string value) {
            //id атрибута
            var atrId = Class365API._attributesForGoods.First(a => a.name.StartsWith("WB.RU"))?.id;
            //если атрибут существует
            if (atrId != null) {
                //проверим, заполнен ли уже в карточе такой атрибут
                var goodAttr = attributes?.Find(f => f.Attribute.id == atrId);
                //если атрибута нет - создаем привязку

                //добавляю атрибут в карточку
                var s = await Class365API.RequestAsync("post", "goodsattributes", new Dictionary<string, string>() {
                    {"good_id", id},
                    {"attribute_id", atrId},
                    {"value", value}
                });
                if (s != null && s.Contains("updated")) {
                    attributes.Add(
                    new Attributes() {
                        Attribute = new Attribute() { id = atrId },
                        Value = new Value() { value = value }
                    });
                    Log.Add($"SetWB: {name} - добавлена характеристика WB.RU: {value}");
                    return true;
                }
            }
            //если нет такого атрибута, то ничего не делаем
            return false;
        }

        //Атрибут Срок годности, лет
        public string GetValidity() {
            var validity = attributes?.Find(f => f.Attribute.id == "2283760");
            if (validity != null && validity.Value.name != "") {
                return validity.Value.name;
            } else
                return defaultValidity;
        }
        //Атрибут Количество в упаковке, шт
        public string GetPackQuantity() {
            var quantity = attributes?.Find(f => f.Attribute.id == "2597286");
            if (quantity != null && quantity.Value.value != "") {
                return quantity.Value.value;
            } else
                return "1";
        }
        //Атрибут Комплектация
        public string GetComplectation() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2543016");
            if (attribute != null && attribute.Value.value != "") {
                return attribute.Value.value;
            } else
                return null;
        }
        //Атрибут Гарантия
        public string GetGaranty() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2539132");
            if (attribute != null && attribute.Value.value != "") {
                return attribute.Value.value;
            } else
                return null;
        }
        //Атрибут Альтернативные артикулы
        public string GetAlternatives() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2543012");
            if (attribute != null && attribute.Value.value != "") {
                return attribute.Value.value;
            } else
                return null;
        }
        //Атрибут Количество заводских упаковок
        public string GetFabricBoxCount() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2543424");
            if (attribute != null && attribute.Value.value != "") {
                return attribute.Value.value;
            } else
                return null;
        }
        //Атрибут Цвет товара
        public string GetColor() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2543422");
            if (attribute != null && attribute.Value.value != "") {
                return attribute.Value.name;
            } else
                return null;
        }
        //Атрибут Вид техники
        public string GetTechType() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2543335");
            if (attribute != null && attribute.Value.value != "") {
                return attribute.Value.name;
            } else
                return null;
        }
        //Атрибут Класс опасности товара
        public string GetDangerClass() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2604819");
            if (attribute != null && attribute.Value.value != "") {
                return attribute.Value.value;
            } else
                return null;
        }
        //Атрибут OEM-номер
        public string GetOEM() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2535412");
            if (attribute != null && attribute.Value.value != "") {
                return attribute.Value.value;
            } else
                return null;
        }
        //Атрибут Страна-изготовитель
        public string GetManufactureCountry() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2543306");
            if (attribute != null && attribute.Value.name != "") {
                return attribute.Value.name;
            } else
                return null;
        }
        //Атрибут Тип двигателя
        public string GetMotorType() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2543313");
            if (attribute != null && attribute.Value.name != "") {
                return attribute.Value.name;
            } else
                return null;
        }
        //Атрибут Место установки
        public string GetPlacement() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2543347");
            if (attribute != null && attribute.Value.name != "") {
                return attribute.Value.name;
            } else
                return null;
        }
        //Атрибут Материал
        public string GetMaterial() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2543138");
            if (attribute != null && attribute.Value.name != "") {
                return attribute.Value.name;
            } else
                return null;
        }
        //Атрибут Срок годности
        public string GetExpirationDays() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2283760");
            if (attribute != null && attribute.Value.name != "") {
                var val = float.Parse(attribute.Value.name.Split(' ').First().Replace(".", ","));
                if (attribute.Value.name.Contains("недел"))
                    return (val * 7).ToString("F0");
                return (val * 365).ToString("F0");
            } else
                return null;
        }
        //Атрибут Расположение детали
        public string GetPlace() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2543152");
            if (attribute != null && attribute.Value.name != "") {
                return attribute.Value.name;
            } else
                return null;
        }
        //Атрибут Сторона установки
        public string GetSide() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2627738");
            if (attribute != null && attribute.Value.name != "") {
                return attribute.Value.name;
            } else
                return null;
        }
        //Атрибут Кратность покупки
        public string GetMultiplicity() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2299154");
            if (attribute != null && attribute.Value.value != "") {
                return attribute.Value.value;
            } else
                return null;
        }
        //Атрибут Ключевые слова
        public string GetKeywords() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2543336");
            if (attribute != null && attribute.Value.value != "") {
                return attribute.Value.value;
            } else
                return null;
        }
        //Атрибут Толщина, мм
        public string GetThickness() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2543314");
            if (attribute != null && attribute.Value.value != "") {
                return (float.Parse(attribute.Value.value.Replace(".", ",")) * 10).ToString("F0"); //см => мм
            } else
                return null;
        }
        //Атрибут Высота, мм
        public string GetHeight() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2543149");
            if (attribute != null && attribute.Value.value != "") {
                return (float.Parse(attribute.Value.value.Replace(".", ",")) * 10).ToString("F0"); //см => мм
            } else
                return null;
        }
        //Атрибут Длина, мм
        public string GetLengthAttr() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2543150");
            if (attribute != null && attribute.Value.value != "") {
                return (float.Parse(attribute.Value.value.Replace(".", ",")) * 10).ToString("F0"); //см => мм
            } else
                return null;
        }
        //Атрибут Объем, л
        public string GetVolume() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2614266");
            if (attribute != null && attribute.Value.value != "") {
                return attribute.Value.value.Replace(".", ",");
            } else
                return null;
        }
        //Атрибут Объем, мл
        public string GetVolumeML() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2627783");
            if (attribute != null && attribute.Value.value != "") {
                return attribute.Value.value.Replace(".", ",").Split(',').First();
            } else
                return null;
        }
        //Атрибут Количество в упаковке
        public string GetCountInBox() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2597286");
            if (attribute != null && attribute.Value.value != "") {
                return attribute.Value.value;
            } else
                return null;
        }
        //Атрибут Количество отверстий
        public string GetCountOfHoles() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2543014");
            if (attribute != null && attribute.Value.value != "") {
                return attribute.Value.value;
            } else
                return null;
        }
        //Атрибут Внешний диаметр, мм
        public string GetDiameterOut() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2543013");
            if (attribute != null && attribute.Value.value != "") {
                return (float.Parse(attribute.Value.value.Replace(".", ",")) * 10).ToString("F0"); //см => мм
            } else
                return null;
        }
        //Атрибут Внутренний диаметр, мм
        public string GetDiameterIn() {
            var attribute = attributes?.Find(f => f.Attribute.id == "2543147");
            if (attribute != null && attribute.Value.value != "") {
                return (float.Parse(attribute.Value.value.Replace(".", ",")) * 10).ToString("F0"); //см => мм
            } else
                return null;
        }
        //Номера из описания
        public List<string> GetDescriptionNumbers() { //todo переделать с помощью регулярок
            return description.Split('№')
                              .Skip(1)
                              .Select(s => s.Replace("&nbsp;", " ")
                                            .Replace("&thinsp;", " ")
                                            .Trim(' ')
                                            .Replace("<", " ")
                                            .Replace(")", " ")
                                            .Replace("(", " ")
                                            .Replace("]", " ")
                                            .Replace("/", "")
                                            .Replace("\\", " ")
                                            .Replace("</p>", " ")
                                            .Replace("\n", " ")
                                            .Replace(",", " ")
                                            .Split(' ')[0])
                              .ToList();
        }

        //проверяем, нужно ли к товару данной группы прикреплять доп описание про другие запчасти, гарантию и установку
        public bool IsGroupSolidParts() {
            if (group_id == "169326" || //Корневая группа
                group_id == "168723" || //Аудио-видеотехника
                group_id == "168807" || //Шины, диски, колеса
                group_id == "289732" || //Автохимия
                group_id == "460974" || //Аксессуары
                group_id == "430926" || //Масла
                group_id == "2281135" || //Инструменты (аренда)
                group_id == "530058") { //Инструменты
                return false;
            }
            return true;
        }
        public string NameLimit(int length, string specDesc = null) {
            string t;
            //если указан параметр - ищем альтернативное название в описании
            if (specDesc != null) {
                //var pattern = @"\{([A-Z_.]+)\}\s?([\s|\S]*)\{\/\1\}";  //паттерн для поиска всех тегов
                //подставляем нужный тег в регулярное выражение
                var pattern = @"\{(" + specDesc + @")\}\s?([\s|\S]*)\{\/\1\}";
                var regex = new Regex(pattern);
                var match = regex.Match(description);
                t = match.Success ? match.Groups[2].Value.Trim() : name;
            } else {
                t = name;
            }
            t = t.Replace("(копия)", ""); //удаление признака Копии
            t = Regex.Replace(t, "([7-9]\\d{9,10})", string.Empty); //удаляем номера, похожие на телефон
            //todo удалить фразы типа сделать фото и т.п.
            while (t.Length > length) {
                t = t.Remove(t.LastIndexOf(' '));
            }
            return t;
        }

        public string HtmlDecodedDescription() =>
            Regex.Replace(
                HttpUtility.HtmlDecode(description)
                           .Replace("\n", "|")
                           .Replace("|", " "),
                           "<[^>]+>", " ").Trim();

        public List<string> DescriptionList(int b = 3000, List<string> dop = null, bool removeSpec = false, 
                                            bool removePhone = false, string specDesc = null) {
            string d;
            //ищем специальное описание для маркетплейса
            if (specDesc!= null) {
                //var pattern = @"\{([A-Z_.]+)\}\s?([\s|\S]*)\{\/\1\}";  //для поиска всех тегов
                //подставляем нужный тег в регулярное выражение
                var pattern = @"\{(" + specDesc + @")\}\s?([\s|\S]*)\{\/\1\}";
                var regex = new Regex(pattern);
                var match = regex.Match(description);
                d = match.Success ? match.Groups[2].Value?.Trim() : "";
                //d = match.Success ? match.Groups[2].Value?.Trim() : description.Split('{').First(); //вариант
            } else {
                //иначе используем текст до открывающей фигурной скобки
                d = description.Split('{').First();
            }
            if (removeSpec)
                d = d
                    .Replace("/", " ")
                    .Replace("\\", " ")
                    .Replace("(", " ")
                    .Replace(")", " ")
                    .Replace("[", " ")
                    .Replace("]", " ")
                    .Replace("!", " ")
                    .Replace("#", " ")
                    .Replace("*", " ")
                    .Replace("%", " ")
                    .Replace("+", " ");
            if (removePhone)
                d = Regex.Replace(d, "([7-9]\\d{9,10})", string.Empty); //удаляем номера, похожие на телефон
            var s = Regex.Replace(d
                                .Replace("\n", "|")
                                .Replace("<br />", "|")
                                .Replace("<br>", "|")
                                .Replace("</p>", "|")
                                .Replace("&nbsp;", " ")
                                .Replace("&quot;", "")
                                .Replace("&gt;", "")
                                .Replace(" &gt", ""),
                            "<[^>]+>", string.Empty)
                    .Split(new[] { "|" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(ta => ta.Trim())
                    .Where(tb => tb.Length > 1)
                    .ToList();
            if (IsGroupSolidParts() && dop != null) {
                s.AddRange(dop);
            }
            //контролируем длину описания
            var newSLength = 0;
            for (int u = 0; u < s.Count; u++) {
                if (s[u].Length + newSLength > b) {
                    s.Remove(s[u]);
                    u = u - 1;
                } else {
                    newSLength = newSLength + s[u].Length;
                }
            }
            return s;
        }

        public bool IsTimeUpDated() {
            var t = DateTime.Parse(updated);
            return t.CompareTo(Class365API.LastScanTime) > 0;
        }

        public string GroupName() => Groups.Count(c => c.id == group_id) > 0 ?
                                    Groups.First(f => f.id == group_id).name : "";
        public static string GroupName(string group_id) => Groups.Any(c => c.id == group_id) ?
                                                          Groups.First(f => f.id == group_id).name : "";

        public string GetDiskSize() {
            try {
                return name.ToLower()
                         .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                         .First(f => f.StartsWith("r"))
                         .TrimStart('r');
            } catch { }
            return "14";
        }

        [JsonIgnore]
        public bool New {
            get {
                if (group_id == "289732" || //Автохимия
                    group_id == "430926")  //Масла
                    return true;
                var nameAndDesc = (name + ":" + description);
                return !(Regex.IsMatch(nameAndDesc, @"(?:[^а-яА-Я_ёЁ]|^)([Бб]\s*[\/\\.\,]*\s*[Уу])[^a-zA-Zа-яА-Я0-9_ёЁ]"));
            }
        }
        [JsonIgnore]
        public bool Origin {
            get {
                var low = description.ToLowerInvariant();
                if (low.Contains("оригинал") &&
                    !low.Replace(" ", "").Contains("неоригинал"))
                    return true;
                return false;
            }
        }

        public string DiskType() {
            var n = name.ToLower() + description.ToLower();
            return n.Contains("штамп") ? "Штампованные"
                                       : n.Contains("кован") ? "Кованые"
                                                             : "Литые";
        }

        public string GetNumber() {
            if (!String.IsNullOrEmpty(description) && description.Contains("№")) {
                var pattern = @"№\s*([а-яa-z0-9]+)\s*";
                var number = Regex.Match(description, pattern).Groups[1].Value;
                if (string.IsNullOrEmpty(number))
                    number = "";
                return number;
                //return description
                //                .Split('№').Last()
                //                .Replace("\u00A0", " ")
                //                .Replace("\u2009", " ")
                //                .Replace("&nbsp;", " ")
                //                .Replace("&thinsp;", " ")
                //                .Replace("<", " ")
                //                .Replace("(", " ")
                //                .Replace("[", " ")
                //                .Replace("/", " ")
                //                .Replace(",", " ")
                //                .Replace("\n", " ")
                //                .Split(' ').First()
                //                .Trim(',');
            }
            return "";
        }
        //тип акустики
        public string GetAudioType() {
            var d = (name + " " + description).ToLowerInvariant();
            if (d.Contains("коаксиал"))
                return "Коаксиальная";
            else if (d.Contains("твитер") || d.Contains("высокочас") || d.Contains("пищалк"))
                return "Твитер";
            else if (d.Contains("сабвуфер"))
                return "Сабвуфер";
            return "Среднечастотная";
        }
        //количество полос динамика, шт
        public string GetVoiceCoil() {
            var d = (name + " " + description).ToLowerInvariant();
            var pattern = @"полос\D*([0-9]+[.,]*[0-9]*)";
            var number = Regex.Match(d, pattern).Groups[1].Value;
            if (string.IsNullOrEmpty(number))
                number = "1";
            return number;
        }
        //размер динамика, см
        public string GetAudioSize() {
            var d = (name + " " + description).ToLowerInvariant();
            //ищем размер в см
            var pattern = @"([0-9]+[.,]*[0-9]*)\s*см";
            var number = Regex.Match(d, pattern)?.Groups[1]?.Value;
            if (!string.IsNullOrEmpty(number))
                return number.Replace(",",".");
            //ищем размер в мм
            pattern = @"([0-9]+[.,]*[0-9]*)\s*мм";
            number = Regex.Match(d, pattern)?.Groups[1]?.Value;
            if (!string.IsNullOrEmpty(number)) {
                float fn = float.Parse(number)/10;
                return fn.ToString().Replace(",",".");
            }
            return "10";
        }
        //мощность динамика, вт
        public string GetRms() {
            var d = (name + " " + description).ToLowerInvariant();
            var pattern = @"([0-9]+[.,]*[0-9]*)\s*(?:вт|w|ват)";
            var number = Regex.Match(d, pattern).Groups[1].Value;
            if (string.IsNullOrEmpty(number))
                number = "20";
            return number;
        }
        //сопротивление динамика, ом
        public string GetImpedance() {
            var d = (name + " " + description).ToLowerInvariant();
            var pattern = @"([0-9]+[.,]*[0-9]*)\s*(?:ом|om)";
            var number = Regex.Match(d, pattern).Groups[1].Value;
            if (string.IsNullOrEmpty(number))
                number = "4";
            return number;
        }

        //получаем колчество отверстий на диске из описания
        public string GetNumberOfHoles() {
            var pattern = @"(?:.+)\s*(\d)(?:\*|x|х)\s*(?:[0-9]+)";
            var number = Regex.Match(description.ToLowerInvariant(), pattern).Groups[1].Value;
            if (string.IsNullOrEmpty(number))
                number = "4";
            return number;
        }
        //получаем диаметр отверстий на диске из описания
        public string GetDiameterOfHoles() {
            var pattern = @"\d\s*(?:\*|x|х)\s*([0-9]+\.*[0-9]*)\s*(?:<|\ |m|м|.|,)";
            var number = Regex.Match(description.ToLowerInvariant(), pattern).Groups[1].Value;
            if (string.IsNullOrEmpty(number))
                number = "100";
            return number;
        }
        //получаем диаметр ступицы из описания
        public string GetDiaStup() {
            var pattern = @"(?>пица|dia|Dia|DIA)\D*([0-9]+[.,]*[0-9]*)\s*";
            var number = Regex.Match(description.ToLowerInvariant(), pattern).Groups[1].Value;
            if (string.IsNullOrEmpty(number))
                number = "57.1";
            return number.Replace(",", ".");
        }
        //производители
        private static string[] manufactures;
        public string GetManufacture(bool ozon = false) {
            //проверяю сперва характеристику для озона
            Attributes manufacture = null;
            if (ozon)
                manufacture = attributes?.Find(f => f.Attribute.id == "2583395"); //Бренд для озон
            //использую основную характеристику в карточке
            if (manufacture == null)
                manufacture = attributes?.Find(f => f.Attribute.id == "75579"); //Производитель
            if (manufacture != null && manufacture.Value.name != "") {
                return manufacture.Value.name;
            }
            //если характеристика не указана, то пытаюсь определить из названия и описания
            ResetManufactures();
            var n = (name + " | " + Regex.Replace(description ?? "", "<[^>]+>", " "))
                .ToUpperInvariant()
                .Replace("\n", " ").Replace("\r", " ")
                .Replace(".", " ").Replace(",", " ")
                .Replace("\\", " ").Replace("/", " ")
                .Replace("(", " ").Replace(")", " ")
                .Replace(":", " ").Replace(")", " ")
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).ToList();
            return manufactures.LastOrDefault(                             //ищем производителя, для этого
                                    man => man.Split(':')                   //каждого делим на массив синонимов по ':'
                                              .Select(s => n.Contains(s))   //проверяем каждый синоним на вхождение в описание
                                              .Any(w => w))                 //есть совпадение? подходит
                              ?.Split(':')                                  //если нашли строку с производителем, снова делим по ':'
                               .First();                                    //берем первый элемент
        }
        //определяю название запчасти, марку и модель из описания
        private static string[] _autos;
        //марка и модель асинхронный метод
        public async Task<string[]> GetNameMarkModelAsync() =>
            await Task.Factory.StartNew(() => {
                return GetNameMarkModel();
            });
        //марка и модель синхронный метод
        public string[] GetNameMarkModel() {
            //склеиваю название и описание
            var desc = (name + " " + HtmlDecodedDescription()).ToLowerInvariant();
            //новый словарь для учета совпадений
            var dict = new Dictionary<string, int>();
            ResetAutos();
            //проверяю похожесть на каждый элемент списка
            for (int i = 0; i < _autos.Length; i++) {
                dict.Add(_autos[i], 0);
                foreach (var word in _autos[i].Split(';'))
                    if (desc.Contains(word))
                        dict[_autos[i]]++;
            }//== dict.Values.Max()
            //определяю лучшие совпадения
            var best = dict.OrderByDescending(o => o.Value).Where(w => w.Value >= 3).Select(s => s.Key).ToList();
            //если они есть
            if (best.Count > 0) {
                //определяю запчасть
                var n = new StringBuilder();
                foreach (var part in name.Split(' ')) {
                    //если слово не содержится в марке и не является номером запчасти, то берем его
                    if (!best[0].Contains(part.ToLowerInvariant()) && part != this.Part)
                        n.Append(part).Append(" ");
                    //иначе завершаем проверку
                    else
                        break;
                }
                //проверяю длину названия, если она больше 0, то ок
                if (n.Length > 0) {
                    //разбираю строку с маркой
                    var s = best[0].Split(';');
                    //возвращаю результат: 1-название запчасти, 2-марка, 3-модель
                    return new string[] { n.ToString().Trim(), s[0], s[1], s[2] };
                }
            }
            //Log.Add("business.ru: " + name + " пропущен - не удалось определить марку или модель");
            //обнуляю список моделей для загрузки исправленного в новом проходе
            return null;
        }
        //перечитать марки и модели
        public static void ResetAutos() {
            if (_autos == null || File.GetLastWriteTime(_autoMarks) > Class365API.LastScanTime) {
                _autos = File.ReadAllLines(_autoMarks);
                File.SetLastWriteTime(_autoMarks, Class365API.LastScanTime);
            }
        }
        //перечитать из таблицы настроек
        public static void ResetManufactures() {
            if (manufactures == null || DateTime.Now.Ticks % 10000 == 0)
                manufactures = DB.GetParamStr("manufactures").Split(',');
        }
        //метод определения размеров
        public string GetDimentionsString() {
            var d = GetDimentions();
            return (d[0].ToString("F1") + "/" + d[1].ToString("F1") + "/" + d[2].ToString("F1")).Replace(",", ".");
        }
        //массив размеров сторон (длина, ширина, высота), см
        public float[] GetDimentions() {
            float[] dim = new float[3];
            //сперва проверяю поля карточки
            if (!string.IsNullOrWhiteSpace(this.width) &&
                !string.IsNullOrWhiteSpace(this.height) &&
                !string.IsNullOrWhiteSpace(this.length)) {
                try {
                    dim[0] = float.Parse(this.length.Replace(".", ","));
                    dim[1] = float.Parse(this.width.Replace(".", ","));
                    dim[2] = float.Parse(this.height.Replace(".", ","));
                    for(int i = 0; i < 3; i++) 
                        if (dim[i] < 5) dim[i] = 5;
                    Array.Sort(dim);
                    Array.Reverse(dim);
                    return dim;
                } catch (Exception x) {
                    Log.Add("GetDimentions: " + name + " - ошибка! неверно заполнен размер в полях: \nдлина " +
                        this.length + " \nширина " + this.width + " \nвысота " + this.height + "\n" + x.Message);
                }
            }
            //если характеристики не указаны, либо указаны неверно,
            //рассчитываю размеры из параметра Объем (м3)
            if (volume == null || volume == 0)
                volume = defaultVolume;
            //средняя длина стороны = кубический корень из объема
            var dimention = Math.Pow((double) volume, 1.0 / 3.0);
            //первую округляю в большую сторону
            dim[0] = (float) Math.Ceiling(dimention * 20) * 5;
            //вторую - в меньшую
            dim[1] = (float) Math.Floor(dimention * 20) * 5;
            //третью вычисляю от первых двух и округляю до целых
            dim[2] = (float) Math.Round((double) (100 * volume / (dim[0] * 0.01 * dim[1] * 0.01)));
            //строка с размерами
            return dim;
        }
        //суммарная длина сторон
        public float GetLength() {
            var d = GetDimentions();
            return d[0] + d[1] + d[2];
        }
        //квант продажи
        public int GetQuantOfSell() {
            int p;
            var atrId = Class365API._attributesForGoods.First(a => a.name.StartsWith("Квант продажи")).id;
            if (int.TryParse((attributes?.Find(f => f.Attribute.id == atrId)?.Value.value.ToString()) ?? "1", out p))
                return p;
            return 1;
        }
    }
    public class ApplicationItem {
        public string id { get; set; }
        public string name { get; set; }
        public string attribute_id { get; set; }
    }

    //////////////////////////////////

    public class Realization {
        public string id { get; set; }
        public string number { get; set; }
        public string date { get; set; }
        //public Author_Employee author_employee { get; set; }
        //public Responsible_Employee responsible_employee { get; set; }
        //public Organization organization { get; set; }
        //public Partner partner { get; set; }
        public Status status { get; set; }
        //public string contract_id { get; set; }
        public Currency currency { get; set; }
        public string currency_value { get; set; }
        //public int nds_include { get; set; }
        public bool held { get; set; }
        public string comment { get; set; }
        //public string customer_order_id { get; set; }
        //public string delivery_order_id { get; set; }
        public string reservation_id { get; set; }
        //public Shipper shipper { get; set; }
        //public Consignee consignee { get; set; }
        public string sum { get; set; }
        public string updated { get; set; }
        //public bool deleted { get; set; }
        //public int[] departments_ids { get; set; }
        public Good[] goods { get; set; }
    }

    public class Author_Employee {
        public string id { get; set; }
        public string model { get; set; }
        public string last_name { get; set; }
        public string first_name { get; set; }
        public string middle_name { get; set; }
        //public string email { get; set; }
        //public bool active { get; set; }
        //public bool active_retail { get; set; }
        //public int department_id { get; set; }
        //public int role_id { get; set; }
        //public string birthday { get; set; }
        //public string phoneip { get; set; }
        //public int sex { get; set; }
        //public string updated { get; set; }
        //public bool deleted { get; set; }
    }

    public class Responsible_Employee {
        public string id { get; set; }
        public string model { get; set; }
        public string last_name { get; set; }
        public string first_name { get; set; }
        public string middle_name { get; set; }
        //public string email { get; set; }
        //public bool active { get; set; }
        //public bool active_retail { get; set; }
        //public int department_id { get; set; }
        //public int role_id { get; set; }
        //public string birthday { get; set; }
        //public string phoneip { get; set; }
        //public int sex { get; set; }
        //public string updated { get; set; }
        //public bool deleted { get; set; }
    }

    public class Organization {
        public string id { get; set; }
        public string model { get; set; }
        //public string organization_type_id { get; set; }
        //public string legacy_type_id { get; set; }
        //public string name { get; set; }
        //public string full_name { get; set; }
        //public string inn { get; set; }
        //public object kpp { get; set; }
        //public string ogrn { get; set; }
        //public string okpo { get; set; }
        //public object okved { get; set; }
        //public object oktmo { get; set; }
        //public string default_buypricetype_id { get; set; }
        //public string default_salepricetype_id { get; set; }
        //public int vat_accounting { get; set; }
        //public bool archive { get; set; }
        //public object suz_oms_id { get; set; }
        //public object suz_oms_connection { get; set; }
        //public object suz_client_token { get; set; }
        //public object suz_client_token_timeout { get; set; }
        //public string supervisor_first_name { get; set; }
        //public string supervisor_second_name { get; set; }
        //public string supervisor_last_name { get; set; }
        //public string accountant_first_name { get; set; }
        //public string accountant_second_name { get; set; }
        //public string accountant_last_name { get; set; }
        //public string cashier_first_name { get; set; }
        //public string cashier_second_name { get; set; }
        //public string cashier_last_name { get; set; }
        //public string employees_status_id { get; set; }
        //public string address_legal { get; set; }
        //public string address_actual { get; set; }
        //public object kbk { get; set; }
        //public object edo_id { get; set; }
        //public object address_legal_gar_guid { get; set; }
        //public object address_actual_gar_guid { get; set; }
        //public string updated { get; set; }
    }

    public class Partner {
        public string id { get; set; }
        public string model { get; set; }
        //public string organization_type_id { get; set; }
        public string legacy_type_id { get; set; }
        public string name { get; set; }
        //public string full_name { get; set; }
        //public bool customer { get; set; }
        //public bool supplier { get; set; }
        //public bool competitor { get; set; }
        //public bool partner { get; set; }
        //public bool potential { get; set; }
        //public object inn { get; set; }
        //public object kpp { get; set; }
        //public object ogrn { get; set; }
        //public object okpo { get; set; }
        //public object address_legal { get; set; }
        //public object address_actual { get; set; }
        //public object note { get; set; }
        //public string status_id { get; set; }
        //public string responsible_employee_id { get; set; }
        //public object category_id { get; set; }
        //public object kind_id { get; set; }
        //public object region_id { get; set; }
        //public object size_id { get; set; }
        //public int code { get; set; }
        //public bool shared { get; set; }
        //public object address_legal_gar_guid { get; set; }
        //public object address_actual_gar_guid { get; set; }
        //public string updated { get; set; }
        //public bool deleted { get; set; }
        //public int[] departments_ids { get; set; }
    }

    public class Status {
        public string id { get; set; }
        public string model { get; set; }
        public string name { get; set; }
        public int point_group_id { get; set; }
        //public string color { get; set; }
        //public bool _default { get; set; }
        //public int sort { get; set; }
        //public string updated { get; set; }
        //public bool deleted { get; set; }
    }

    public class Currency {
        public string id { get; set; }
        public string model { get; set; }
        public string name { get; set; }
        //public string short_name { get; set; }
        //public string name_iso { get; set; }
        //public string code_iso { get; set; }
        //public string symbol_iso { get; set; }
        //public string okv { get; set; }
        //public bool _default { get; set; }
        //public bool user { get; set; }
        //public object user_value { get; set; }
        //public string name1 { get; set; }
        //public string name2 { get; set; }
        //public string name3 { get; set; }
    }

    public class Shipper {
        public string id { get; set; }
        public string model { get; set; }
        public string organization_type_id { get; set; }
        public string legacy_type_id { get; set; }
        public string name { get; set; }
        public string full_name { get; set; }
        //public string inn { get; set; }
        //public object kpp { get; set; }
        //public string ogrn { get; set; }
        //public string okpo { get; set; }
        //public object okved { get; set; }
        //public object oktmo { get; set; }
        //public string default_buypricetype_id { get; set; }
        //public string default_salepricetype_id { get; set; }
        //public int vat_accounting { get; set; }
        //public bool archive { get; set; }
        //public object suz_oms_id { get; set; }
        //public object suz_oms_connection { get; set; }
        //public object suz_client_token { get; set; }
        //public object suz_client_token_timeout { get; set; }
        //public string supervisor_first_name { get; set; }
        //public string supervisor_second_name { get; set; }
        //public string supervisor_last_name { get; set; }
        //public string accountant_first_name { get; set; }
        //public string accountant_second_name { get; set; }
        //public string accountant_last_name { get; set; }
        //public string cashier_first_name { get; set; }
        //public string cashier_second_name { get; set; }
        //public string cashier_last_name { get; set; }
        //public string employees_status_id { get; set; }
        //public string address_legal { get; set; }
        //public string address_actual { get; set; }
        //public object kbk { get; set; }
        //public object edo_id { get; set; }
        //public object address_legal_gar_guid { get; set; }
        //public object address_actual_gar_guid { get; set; }
        //public string updated { get; set; }
    }

    public class Consignee {
        public string id { get; set; }
        public string model { get; set; }
        //public object organization_type_id { get; set; }
        public string legacy_type_id { get; set; }
        public string name { get; set; }
        public string full_name { get; set; }
        //public bool customer { get; set; }
        //public bool supplier { get; set; }
        //public bool competitor { get; set; }
        //public bool partner { get; set; }
        //public bool potential { get; set; }
        //public object inn { get; set; }
        //public object kpp { get; set; }
        //public object ogrn { get; set; }
        //public object okpo { get; set; }
        //public object address_legal { get; set; }
        //public object address_actual { get; set; }
        //public object note { get; set; }
        //public string status_id { get; set; }
        //public string responsible_employee_id { get; set; }
        //public object category_id { get; set; }
        //public object kind_id { get; set; }
        //public object region_id { get; set; }
        //public object size_id { get; set; }
        //public int code { get; set; }
        //public bool shared { get; set; }
        //public object address_legal_gar_guid { get; set; }
        //public object address_actual_gar_guid { get; set; }
        //public string updated { get; set; }
        //public bool deleted { get; set; }
        //public int[] departments_ids { get; set; }
    }

    public class Good {
        public string id { get; set; }
        public Good1 good { get; set; }
        public string amount { get; set; }
        public string price { get; set; }
        public Measure measure { get; set; }
        public Price_Type price_type { get; set; }
        //public object discount_type { get; set; }
        //public object discount_value { get; set; }
        public string sum { get; set; }
        public object modification_id { get; set; }
        public Store store { get; set; }
        public Nds nds { get; set; }
        public string updated { get; set; }
        public object[] marking_number { get; set; }
        public object[] serial_number { get; set; }
    }



    public class Good1 {
        public string id { get; set; }
        //public string model { get; set; }
        public string name { get; set; }
        //public string full_name { get; set; }
        //public int nds_id { get; set; }
        //public string group_id { get; set; }
        //public string part { get; set; }
        //public string store_code { get; set; }
        //public string type { get; set; }
        //public bool archive { get; set; }
        //public string description { get; set; }
        //public string country_id { get; set; }
        //public bool allow_serialnumbers { get; set; }
        //public bool allow_serialnumbers_unique { get; set; }
        //public string weight { get; set; }
        //public string volume { get; set; }
        //public string code { get; set; }
        //public string store_box { get; set; }
        //public string remains_min { get; set; }
        //public string partner_id { get; set; }
        //public string responsible_employee_id { get; set; }
        //public string feature { get; set; }
        //public string weighing_plu { get; set; }
        //public string cost { get; set; }
        //public string measure_id { get; set; }
        //public string good_type_code { get; set; }
        //public int payment_subject_sign { get; set; }
        //public string marking_type { get; set; }
        //public bool allow_marking { get; set; }
        //public string taxation { get; set; }
        //public bool require_marking_for_sale { get; set; }
        //public string length { get; set; }
        //public string width { get; set; }
        //public string height { get; set; }
        //public bool oversized { get; set; }
        //public string pack { get; set; }
        //public string hscode_id { get; set; }
        //public string updated_remains_prices { get; set; }
        //public string updated { get; set; }
        //public bool deleted { get; set; }
        //public int[] departments_ids { get; set; }
        //public string[] images { get; set; }
    }

    public class Measure {
        public string id { get; set; }
        public string model { get; set; }
        public string name { get; set; }
        //public string short_name { get; set; }
        //public bool _default { get; set; }
        //public string okei { get; set; }
        //public bool archive { get; set; }
        //public string updated { get; set; }
        //public bool deleted { get; set; }
    }

    public class Price_Type {
        public string id { get; set; }
        public string model { get; set; }
        public string name { get; set; }
        //public string responsible_employee_id { get; set; }
        //public string organization_id { get; set; }
        //public string currency_id { get; set; }
        //public string owner_employee_id { get; set; }
        //public bool archive { get; set; }
        //public string updated { get; set; }
        //public bool deleted { get; set; }
        //public object departments_ids { get; set; }
    }

    //public class Store {
    //    public string id { get; set; }
    //    public string model { get; set; }
    //    public string name { get; set; }
    //    public string responsible_employee_id { get; set; }
    //    public int debit_type { get; set; }
    //    public bool deny_negative_balance { get; set; }
    //    public string updated { get; set; }
    //    public bool deleted { get; set; }
    //}

    public class Nds {
        public int id { get; set; }
        //public string model { get; set; }
        public string name { get; set; }
        public string value { get; set; }
    }

    public class Response {
        public string id { get; set; }
        public string updated { get; set; }
    }

    public class CurrentPrice {
        public string id { get; set; }
        public string good_id { get; set; }
        public string price { get; set; }
        public string updated { get; set; }
        public string price_type_id { get; set; }

        [JsonIgnore]
        public DateTime Updated {
            get {
                return DateTime.Parse(updated);
            }
            set {
                updated = value.ToString();
            }
        }
    }

    public class AttributesForGoods { 
        public string id { get; set; }
        public string name { get; set; }
        public string updated { get; set; }
        public bool selectable { get; set; }
        public bool archive { get; set; }
        public bool deleted { get; set; }
    
    //  'description' => NULL,
    //  'sort' => 77145,
    }


    //public class SupplyGoods { 
    //    public string id { get; set; }
    //    public string good_id { get; set; }
    //    public string price { get; set; }
    //    public DateTime updated { get; set; }
    //}
}
