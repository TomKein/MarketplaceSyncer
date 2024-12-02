using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OfficeOpenXml;
using System.Windows.Forms;
using Selen.Tools;
using Newtonsoft.Json;

namespace Selen.Sites {
    public class Seller24ru {

        string _l = "Seller24ru";
        ExcelPackage _excelPackage;
        List<CurrentPrice> _goodPrices = new List<CurrentPrice>();

        public async Task FillPrices() {
            //открываем файл шаблон
            if (!OpenFile())
                return;
            //для каждого листа в шаблоне (для каждого сайта)
            for (int i = 1; i <= _excelPackage.Workbook.Worksheets.Count; i++) {
                //выбираем лист для заполнения
                var sheet = _excelPackage.Workbook.Worksheets[i];
                Log.Add($"{_l} заполняю цены в таблицу {sheet.Name}");
                //колонка с id в таблицах разная - для маркета это 3й столбец, для остальных 4й
                var idCol = sheet.Name.Contains("Yandex") ? 3 : 4;
                //перебираем строчки в таблице, пока есть данные в наименованиях
                for (int row = 2; sheet.Cells[row, 2].Value != null; row++) {
                    string id = sheet.Cells[row, idCol].Value?.ToString();
                    //если id не найден - пропускаем 
                    if (string.IsNullOrEmpty(id))
                        continue;
                    //ищем карточку и бизнес.ру
                    var good = Class365API.FindGood(id);
                    if (good != null) {
                        var price = await GetPrice(good.id);
                        if (price != null) {
                            if (sheet.Name.Contains("Wildberries") && good.GetQuantOfSell()>1)
                                price = (float.Parse(price) * good.GetQuantOfSell()).ToString();
                            sheet.Cells[row, 7].Value = price;
                            Log.Add($"{_l} {row} - {good.name} заполнена цена {price}");
                            await Task.Delay(10);
                        }
                    }
                }
                _excelPackage.Save();
                Log.Add($"{_l} таблицу {sheet.Name} заполнена и сохранена");
            }
        }
        public bool OpenFile() {
            Log.Add($"{_l} Выберите файл с сайта Seller24.ru, в который нужно вписать себестоимость товаров");
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Excel file (*.xlsx)|*.xlsx";
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (openFileDialog.ShowDialog() == DialogResult.OK)
                try {
                    File.Copy(openFileDialog.FileName, openFileDialog.FileName + ".bak", overwrite: true);
                    var fileInfo = new FileInfo(openFileDialog.FileName);
                    _excelPackage = new ExcelPackage(fileInfo);
                    //_excelPackage
                    if (_excelPackage != null && _excelPackage.Workbook.Worksheets.Count > 0) {
                        Log.Add($"{_l} файл {openFileDialog.FileName} успешно загружен, " +
                            $"количество таблиц {_excelPackage.Workbook.Worksheets.Count}");
                        return true;
                    }
                } catch (Exception x) {
                    MessageBox.Show(x.Message, "Ошибка открытия файла!");
                }
            return false;
        }
        public async Task<string> GetPrice(string good_id) {
            //проверяем цену в коллекции
            if (_goodPrices.Any(a => a.good_id == good_id))
                return _goodPrices.Find(f => f.good_id == good_id)?.price;
            //получаем последнюю цену из поступлений
            var price = await RequestPrice("supplygoods", good_id, new Dictionary<string, string>());
            //если цена не найдена, проверяем оприходования
            if (price == null) {
                price = await RequestPrice("postinggoods", good_id, new Dictionary<string, string>());
            }
            //если цена не найдена, проверяем ввод остатков на склад
            if (price == null) {
                price = await RequestPrice("remaingoods", good_id, new Dictionary<string, string>());
            }
            //если цена не найдена, проверяем цену в карточке
            if (price == null) {
                price = await RequestPrice("currentprices", good_id, new Dictionary<string, string>() {
                    { "price_type_id", "75523" }
                });
            }
            price = price?.Replace(".", ",");
            //сохраняем цену в коллекцию
            _goodPrices.Add(new CurrentPrice { good_id = good_id, price = price });
            return price;
        }

        async Task<string> RequestPrice(string model, string good_id, Dictionary<string, string> dictParams) {
            try {
                dictParams.Add("good_id", good_id);
                dictParams.Add("order_by[id]", "DESC");
                dictParams.Add("limit", "5");

                var s = await Class365API.RequestAsync("get", model, dictParams);
                var priceList = JsonConvert.DeserializeObject<List<CurrentPrice>>(s);
                if (priceList.Count > 0) {
                    var priceListSorted = priceList.Where(w => w.price_type_id == null || w.price_type_id == "75523")
                                              .OrderByDescending(f => f.Updated).ToList();
                    return priceListSorted.First().price;
                }
            } catch (Exception x) {
                Log.Add($"{_l} ошибка запроса цен в модели {model} => {x.Message}");
            }
            return null;
        }
    }
}
