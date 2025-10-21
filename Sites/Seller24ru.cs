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
            try {
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
                        if (string.IsNullOrEmpty(id) || sheet.Cells[row, 7].Value != null)
                            continue;
                        //ищем карточку и бизнес.ру
                        var good = Class365API.GetGoodById(id);
                        if (good != null) {
                            var price = await GetNetPrice(good.id);
                            if (price != 0) {
                                if (sheet.Name.Contains("Wildberries") && good.GetQuantOfSell() > 1)
                                    price = price * good.GetQuantOfSell();
                                sheet.Cells[row, 7].Value = price.ToString("F2");
                                if (sheet.Cells[row, 6].Value != sheet.Cells[row, 7].Value)
                                    Log.Add($"{_l} {row} - {good.name} новая цена закупки {sheet.Cells[row, 6].Value} => {sheet.Cells[row, 7].Value}");
                                await Task.Delay(10);
                            }
                        }
                        if (row%500==0) _excelPackage.Save();
                    }
                    _excelPackage.Save();
                    Log.Add($"{_l} таблицу {sheet.Name} заполнена и сохранена");
                }
            } catch (Exception x) {
                Log.Add($"{_l}FillPrices: ошибка {x.Message}");
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
        public async Task<float> GetNetPrice(string good_id) {
            string price;
            //проверяем сохраненную цену в коллекции
            if (_goodPrices.Any(a => a.good_id == good_id))
                price = _goodPrices.Find(f => f.good_id == good_id)?.price;
            else {
                //получаем последнюю цену из поступлений
                price = await RequestPrice("supplygoods", good_id, new Dictionary<string, string>());
            }
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
                    { "price_type_id", Class365API.BuyPrice.id }
                });
            }
            //сохраняем цену в коллекцию
            _goodPrices.Add(new CurrentPrice { good_id = good_id, price = price });
            if (price == null)
                return 0;
            return float.Parse(price.Replace(".", ","));
        }

        async Task<string> RequestPrice(string model, string good_id, Dictionary<string, string> dictParams) {
            try {
                dictParams.Add("good_id", good_id);
                dictParams.Add("order_by[id]", "DESC");
                dictParams.Add("limit", "5");

                var s = await Class365API.RequestAsync("get", model, dictParams);
                var priceList = JsonConvert.DeserializeObject<List<CurrentPrice>>(s);
                if (priceList.Count > 0) {
                    var priceListSorted = priceList.Where(w => w.price_type_id == null 
                                             || w.price_type_id == Class365API.BuyPrice.id)
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
