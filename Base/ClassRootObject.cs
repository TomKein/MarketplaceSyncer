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
using System.Windows.Forms;

namespace Selen
{
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
    public class RootGroupsObject
    {
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

    public class Image{
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

    public class RootObject {
        public string id { get; set; }
        public string name { get; set; }
        public string full_name { get; set; }
        public string group_id { get; set; }
        public string part { get; set; }
        public string store_code { get; set; }
        public bool archive { get; set; }
        public string description { get; set; }
        public string updated { get; set; }
        public List<Image> images { get; set; }
        public List<Remains> remains { get; set; }
        public List<Prices> prices { get; set; }
        public List<Attributes> attributes { get; set; }
        public string avito { get; set; }
        public string drom { get; set; }
        public string youla { get; set; }
        public string vk { get; set; }
        public string kp { get; set; }
        public string gde { get; set; }
        public string measure_id { get; set; }
        public float? weight { get; set; }
        public float? volume { get; set; }
        public static DateTime ScanTime { get; set; }
        public static List<RootGroupsObject> Groups { get; set; }
        public int price {
            get {
                if (prices.Count > 0)
                    return prices.Select(s => string.IsNullOrEmpty(s.price) ? 0 :
                                                (int)float.Parse(s.price.Replace(".", ","))).Max();
                return 0;
            }
            set {
                prices = new List<Prices> { new Prices { price = value.ToString() } };
            }
        }
        public float amount {
            get {
                return remains.Select(s => string.IsNullOrEmpty(s.amount.total) ?
                                                0 :
                                                float.Parse(s.amount.total.Replace(".", ","))).Sum();
            }
            set {
                remains = new List<Remains> {
                    new Remains { amount = new Amount{
                        total = value.ToString("F0")
                    } }
                };
            }
        }
        //вес товара по умолчанию
        static float defaultWeight;
        public static void UpdateDefaultWeight() {
            defaultWeight = DB._db.GetParamFloat("defaultWeigth");
            if (defaultWeight == 0) {
                Log.Add("defaultWeigth: ошибка - значение в настройках 0! установлено значение 1 кг");
                defaultWeight = 1f;
            }
        }
        public string GetWeight() {
            if (weight == null || weight == 0)
                weight = defaultWeight;
            return weight?.ToString("F1").Replace(",",".");
        }
        //объем товара по умолчанию
        static float defaultVolume;
        public static void UpdateDefaultVolume() {
            defaultVolume = DB._db.GetParamFloat("defaultVolume");
            if (defaultVolume == 0) {
                Log.Add("defaultVolume: ошибка - значение в настройках 0! установлено значение 0.02 м3");
                defaultVolume = 0.02f;
            }
        }
        //срок годности по умолчанию
        static string defaultValidity;
        public static void UpdateDefaultValidity() {
            var validity = DB._db.GetParamStr("defaultValidity");
            if (string.IsNullOrEmpty(validity)) {
                Log.Add("defaultValidity: ошибка - значение в настройках 0! установлено значение 1 год");
                defaultValidity = "P1Y";
            }else
                defaultValidity = "P" + validity + "Y";
        }
        public string GetValidity() {
            //использую характеристику в карточке
            var validity = attributes?.Find(f => f.Attribute.id == "2283760"); //Срок годности, лет
            if (validity != null && validity.Value.name != "") {
                return validity.Value.name;
            }else
                return defaultValidity;
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
        public string NameLength(int length) {
            var t = name;
            while (t.Length > length) {
                t = t.Remove(t.LastIndexOf(' '));
            }
            return t;
        }

        public string HtmlDecodedDescription() =>
            Regex.Replace(
                HttpUtility.HtmlDecode(description)
                           .Replace("Есть и другие", "|")
                           .Split('|')[0]
                           .Replace("\n", "|")
                           .Replace("|", " "),
                           "<[^>]+>", string.Empty).Trim();

        public List<string> DescriptionList(int b = 3000, string[] dop = null,bool removeSpec=false) {
            string d = description;
            if (removeSpec) d = d
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

            var s = Regex.Replace(d
                                    .Replace("Есть и другие", "|")
                                    .Split('|')[0]
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
            if (IsGroupSolidParts() && dop!=null) {
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
            return t.CompareTo(ScanTime) > 0;
        }

        public string GroupName() => Groups.Count(c => c.id == group_id) > 0 ?
                                    Groups.First(f => f.id == group_id).name : "";
        public static string GroupName(string group_id) => Groups.Count(c => c.id == group_id) > 0 ? 
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

        public bool IsNew() {
            var low = (name + ":" + description).ToLowerInvariant();
            if (group_id == "289732" || //Автохимия
                group_id == "430926" )  //Масла
                return true;
            return !(Regex.IsMatch(low, @"(б[\/\\.]у)") || low.Contains("бу "));
        }
        public static bool IsNew(string s) {
            var low = s.ToLowerInvariant();
            return !(Regex.IsMatch(low, @"(б[\/\\.]у)") || low.Contains("бу "));
        }

        public bool IsOrigin() {
            var low = description.ToLowerInvariant();
            if (low.Contains("оригинал") &&
                !low.Replace(" ", "").Contains("неоригинал"))
                return true;
            return false;
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
                if (string.IsNullOrEmpty(number)) number = "";
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
        //получаем колчество отверстий на диске из описания
        public string GetNumberOfHoles() {
            var pattern = @"(?:.+)\s*(\d)(?:\*|x|х)\s*(?:[0-9]+)";
            var number = Regex.Match(description.ToLowerInvariant(), pattern).Groups[1].Value;
            if (string.IsNullOrEmpty(number)) number = "4";
            return number;
        }
        //получаем диаметр отверстий на диске из описания
        public string GetDiameterOfHoles() {
            var pattern = @"\d\s*(?:\*|x|х)\s*([0-9]+\.*[0-9]*)\s*(?:<|\ |m|м|.|,)";
            var number = Regex.Match(description.ToLowerInvariant(), pattern).Groups[1].Value;
            if (string.IsNullOrEmpty(number)) number = "100";
            return number;
        }
        //производители
        private static string[] manufactures;
        public string GetManufacture() {
            //использую характеристику в карточке
            var manufacture = attributes?.Find(f => f.Attribute.id == "75579"); //Производитель
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
                    if (desc.Contains(word)) dict[_autos[i]]++;
            }//== dict.Values.Max()
            //определяю лучшие совпадения
            var best = dict.OrderByDescending(o => o.Value).Where(w => w.Value >= 3).Select(s => s.Key).ToList();
            //если они есть
            if (best.Count > 0) {
                //определяю запчасть
                var n = new StringBuilder();
                foreach (var part in name.Split(' ')) {
                    //если слово не содержится в марке и не является номером запчасти, то берем его
                    if (!best[0].Contains(part.ToLowerInvariant()) && part!=this.part) n.Append(part).Append(" ");
                    //иначе завершаем проверку
                    else break;
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
            if (_autos == null || File.GetLastWriteTime(@"..\auto.txt") > ScanTime) {
                _autos = File.ReadAllLines(@"..\auto.txt");
                File.SetLastWriteTime(@"..\auto.txt", ScanTime);
            }
        }
        //перечитать из таблицы настроек
        public static void ResetManufactures() {
            if (manufactures == null || DateTime.Now.Ticks%10000 == 0)
                manufactures = DB._db.GetParamStr("manufactures").Split(',');
        }
        //метод определения размеров
        public string GetDimentions() {
            //сперва проверяю характеристики товара, если они указаны, использую их в первую очередь
            var width = attributes?.Find(f => f.Attribute.id == "2283757"); //Ширина
            var heigth = attributes?.Find(f => f.Attribute.id == "2283758"); //Высота
            var length = attributes?.Find(f => f.Attribute.id == "2283759"); //Длина
            if (width != null && width.Value.value != "" && width.Value.value != "0" &&
                heigth != null && heigth.Value.value != "" && heigth.Value.value != "0" &&
                length != null && length.Value.value != "" && length.Value.value != "0") {
                var d = new StringBuilder();
                d.Append(length.Value.value.Replace(",", ".").Split('.').First());
                d.Append(".0/");
                d.Append(width.Value.value.Replace(",", ".").Split('.').First());
                d.Append(".0/");
                d.Append(heigth.Value.value.Replace(",", ".").Split('.').First());
                d.Append(".0");
                return d.ToString();
            }
            //если характеристики не указаны, либо указаны неверно,
            //рассчитываю размеры из параметра Объем (м3)
            if (volume ==null || volume == 0)
                volume = defaultVolume;
            //средняя длина стороны = кубический корень из объема
            var dimention = Math.Pow((double) volume, 1.0 / 3.0);
            //первую округляю в большую сторону
            var x1 = Math.Ceiling(dimention * 20) * 5;
            //вторую - в меньшую
            var x2 = Math.Floor(dimention * 20) * 5;
            //третью вычисляю от первых двух и округляю до целых
            var x3 = Math.Round((double) (100 * volume / (x1 * 0.01 * x2 * 0.01)));
            //строка с размерами
            return (x1.ToString("F1") + "/" + x2.ToString("F1") + "/" + x3.ToString("F1")).Replace(",", ".");
        }
    }
}
