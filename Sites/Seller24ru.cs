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
        string _xlsxFile;
        ExcelPackage _excelPackage;
        List<CurrentPrice> _goodPrices = new List<CurrentPrice>();

        public async Task FillPrices() {
            //открываем файл шаблон
            if (!OpenFile())
                return;
            //для каждого листа в шаблоне (для каждого сайта)
            for (int i = 0; i < _excelPackage.Workbook.Worksheets.Count; i++) {
                //выбираем лист для заполнения
                var sheet = _excelPackage.Workbook.Worksheets[i];
                Log.Add($"{_l} заполняю цены в таблицу {sheet.Name}");
                //колонка с id в таблицах разная - для маркета это 3й столбец, для остальных 4й
                var idCol = sheet.Name.Contains("Yandex") ? 3 : 4;
                //перебираем строчки в таблице, пока есть данные в первом столбце
                for (int row = 2; sheet.Cells[row, 1].Value.ToString() == ""; row++) {
                    string id = sheet.Cells[row, idCol].Value.ToString();
                    //если id не найден - пропускаем 
                    if (id == "")
                        continue;
                    //ищем карточку и бизнес.ру
                    var good = Class365API.FindGood(id);
                    if (good != null) {
                        var price = await GetPrice(good.id);
                        if (price != null) {
                            sheet.Cells[row,7].Value = price;
                            Log.Add($"{_l} {good.name} заполнена цена {price}");
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
                    if (_excelPackage != null) {
                        Log.Add($"{_l} файл успешно загружен");
                        return true;
                    }
                } catch (Exception x) {
                    MessageBox.Show(x.Message, "Ошибка открытия файла!");
                }
            return false;
        }
        public async Task<string> GetPrice(string good_id) {
            //проверяем цену в коллекции
            var price = _goodPrices.Find(f => f.good_id == good_id)?.price;
            //получаем последнюю цену из поступлений
            var dictParams1 = new Dictionary<string, string>() {
                { "good_id", good_id },
                { "price_type_id", "75523" },
            };
            if (price == null) {
                price = await RequestPrice("supplygoods", dictParams1);
            }
            //если цена не найдена, проверяем оприходования
            var dictParams2 = new Dictionary<string, string>() {
                { "good_id", good_id },
            };
            if (price == null) {
                price = await RequestPrice("postinggoods", dictParams2);
            }
            //если цена не найдена, проверяем ввод остатков на склад
            if (price == null) {
                price = await RequestPrice("remaingoods", dictParams2);
            }
            //если цена не найдена, проверяем цену в карточке
            if (price == null) {
                price = await RequestPrice("currentprices", dictParams1);
            }
            //сохраняем цену в коллекцию и возвращаем, без копеек
            _goodPrices.Add(new CurrentPrice { good_id = good_id, price = price });
            return price?.Split('.').First();
        }

        async Task<string> RequestPrice(string model, Dictionary<string, string> dictParams) {
            try {
                var s = await Class365API.RequestAsync("get", model, dictParams);
                var priceList = JsonConvert.DeserializeObject<List<CurrentPrice>>(s);
                if (priceList != null) {
                    var testSorted = priceList.OrderByDescending(f => f.updated).ToList();
                    return priceList.OrderByDescending(g => g.updated).First().price;
                }
            } catch (Exception x) {
                Log.Add($"{_l} ошибка запроса цен в модели {model} => {x.Message}");
            }
            return null;
        }
    }
}
