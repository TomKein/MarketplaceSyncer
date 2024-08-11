using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VkNet.Model.Attachments;
using VkNet;
using VkNet.Model;
using System.Net;
using System.IO;
using System.Threading;
using Application = System.Windows.Forms.Application;
using VkNet.Model.RequestParams.Market;
using VkNet.Enums.Filters;
using Selen.Tools;
using Selen.Base;
using Newtonsoft.Json;

namespace Selen.Sites {
    public class VK {
        public List<MarketAlbum> vkAlb = new List<MarketAlbum>();
        public List<Market> vkMark = new List<Market>();
        VkApi _vk = new VkApi();
        long _marketId;
        int _pageLimitVk;
        string _url;
        string[] _dopDesc;
        string[] _dopDesc2;
        int _creditPriceMin;        //цены для рассрочки
        int _creditPriceMax;
        string _creditDescription;  //описание для рассрочки
        int _addCount;                                              //добавлять X объявлений в час
        int _catalogCheckInterval;                                 //проверять каталог на сайте каждые Х часов
        public int MarketCount;
        public int UrlsCount;
        int _nameLimit = 200;
        List<GoodObject> _bus;
        //главный метод синхронизации вк
        public async Task VkSyncAsync() {
            while (Class365API.Status == SyncStatus.NeedUpdate)
                await Task.Delay(20000);
            _bus=Class365API._bus;
            await GetParamsAsync();
            await IsVKAuthorizatedAsync();
            await EditAsync();
            if (Class365API.SyncStartTime.Minute < 55 || Class365API.SyncStartTime.Hour % _catalogCheckInterval != 0)
                return;
            await GetAlbumsVKAsync();
            if (vkAlb.Count == 0)
                return;
            await AddVKAsync();
            await GetVKAsync();
            await CheckVKAsync();
            await CheckBusAsync();
        }
        //проверка изменений в бизнес.ру
        async Task EditAsync() => await Task.Factory.StartNew(() => {
            for (int b = 0; b < Class365API._bus.Count; b++) {
                if (Class365API._bus[b].IsTimeUpDated() &&
                    Class365API._bus[b].vk != null &&
                    Class365API._bus[b].vk.Contains("http")) {
                    if (Class365API._bus[b].Amount <= 0) {
                        var id = long.Parse(Class365API._bus[b].vk.Split('_').Last());
                        _vk.Markets.Delete(-_marketId, id);
                        Class365API._bus[b].vk = "";
                        Class365API.RequestAsync("put", "goods", new Dictionary<string, string> {
                                                    {"id", Class365API._bus[b].id},
                                                    {"name", Class365API._bus[b].name},
                                                    {_url, Class365API._bus[b].vk} });
                        Log.Add("vk.com: " + Class365API._bus[b].name + " - удалено!");
                        Thread.Sleep(1000);
                    } else
                        Edit(b);
                }
            }
        });

        //запрос параметров из настроек
        async Task GetParamsAsync() => await Task.Factory.StartNew(() => {
            _marketId = DB.GetParamLong("vk.marketId");
            _pageLimitVk = DB.GetParamInt("vk.pageLimit");
            _url = DB.GetParamStr("vk.url");
            _dopDesc = JsonConvert.DeserializeObject<string[]>(DB.GetParamStr("vk.dopDesc"));
            _dopDesc2 = JsonConvert.DeserializeObject<string[]>(DB.GetParamStr("vk.dopDesc2"));
            _addCount = DB.GetParamInt("vk.addCount");
            _catalogCheckInterval = DB.GetParamInt("vk.catalogCheckInterval");
            //рассрочка 
            _creditPriceMin = DB.GetParamInt("creditPriceMin");
            _creditPriceMax = DB.GetParamInt("creditPriceMax");
            _creditDescription = DB.GetParamStr("creditDescription");
        });
        //редактирую объявление
        private void Edit(int b) {
            try {
                //получаю id объявления в вк из ссылки в карточке товара
                var id = "-" + Class365API._bus[b].vk.Split('-').Last();
                //запрашиваю товар из маркета
                var vk = _vk.Markets.GetById(new[] { id }, extended: true).First();
                //готовлю параметры
                var param = new MarketProductParams();
                param.Name = Class365API._bus[b].NameLimit(_nameLimit);
                var desc = Class365API._bus[b].DescriptionList(dop: _dopDesc);
                if (Class365API._bus[b].Price >= _creditPriceMin && Class365API._bus[b].Price <= _creditPriceMax)
                    desc.Insert(0, _creditDescription);
                desc.AddRange(_dopDesc2);
                param.Description = desc.Aggregate((a1, a2) => a1 + "\n" + a2);
                param.CategoryId = (long) vk.Category.Id;
                param.ItemId = vk.Id;
                param.OwnerId = (long) vk.OwnerId;
                param.MainPhotoId = (long) vk.Photos[0].Id;
                param.PhotoIds = vk.Photos.Skip(1).Select(s => (long) s.Id);
                param.Price = (decimal) Class365API._bus[b].Price;
                param.OldPrice = (decimal) Class365API._bus[b].Price;
                _vk.Markets.Edit(param);
                Log.Add("vk.com: " + Class365API._bus[b].name + " - обновлено");
            } catch (Exception x) {
                Log.Add("vk.com: " + Class365API._bus[b].name + " - ошибка редактирования! - " + x.Message);
            }
            Thread.Sleep(10000);
        }
        //добавляю объявления на ВК
        private async Task AddVKAsync() {
            for (int b = 0; b < Class365API._bus.Count && _addCount > 0; b++) {
                //если есть фотографии, цена, количество и нет привязки на вк
                if (Class365API._bus[b].images.Count > 0
                    && !Class365API._bus[b].GroupName().Contains("ЧЕРНОВИК")
                    && Class365API._bus[b].Price > 0
                    && Class365API._bus[b].Amount > 0
                    && !Class365API._bus[b].vk.Contains("http")) {
                    try {
                        _addCount--;
                        await AddAsync(b);
                        await Class365API.RequestAsync("put", "goods", new Dictionary<string, string> {
                                                      {"id", Class365API._bus[b].id},
                                                      {"name", Class365API._bus[b].name},
                                                      {_url, Class365API._bus[b].vk} });
                        Log.Add("vk.com: " + Class365API._bus[b].name + " - добавлено");
                    } catch (Exception ex) {
                        Log.Add("vk.com: " + Class365API._bus[b].name + " - ошибка при добавлении! " + ex.Message);
                    }

                }
            }
        }
        //добавляю объявление
        async Task AddAsync(int b) => await Task.Factory.StartNew(() => {
            //переменные для хранения индексов фото, загруженных на сервер вк
            long mainPhoto = 0;
            List<long> dopPhotos = new List<long>();
            UploadPhotos(b, ref mainPhoto, ref dopPhotos);
            //меняем доп описание
            string desc = Class365API._bus[b].DescriptionList(dop: _dopDesc).Aggregate((a1, a2) => a1 + "\n" + a2) + "\n";
            desc += _dopDesc2.Aggregate((a1, a2) => a1 + "\n" + a2);
            if (Class365API._bus[b].Price >= _creditPriceMin && Class365API._bus[b].Price <= _creditPriceMax)
                desc.Insert(0, _creditDescription);
            //создаем объявление
            long itemId = _vk.Markets.Add(new MarketProductParams {
                OwnerId = -_marketId,
                Name = Class365API._bus[b].NameLimit(_nameLimit),
                Description = desc,
                CategoryId = 404,
                Price = Class365API._bus[b].Price,
                OldPrice = Class365API._bus[b].Price,
                MainPhotoId = mainPhoto,
                PhotoIds = dopPhotos,
                Deleted = false,

            });
            //сохраняю ссылку
            Class365API._bus[b].vk = "https://vk.com/market-23570129?w=product-23570129_" + itemId;
            Thread.Sleep(7000);
            AddToAlbum(b, itemId);
            Thread.Sleep(1000);
        });

        //привяжем объявление к альбому если есть индекс
        private void AddToAlbum(int b, long itemId) {
            int vkAlbInd = vkAlb.FindIndex(a => a.Title.ToLowerInvariant() == Class365API._bus[b].GroupName().ToLowerInvariant());
            if (vkAlbInd > -1) {
                List<long> sList = new List<long>();
                sList.Add((long) vkAlb[vkAlbInd].Id);
                try {
                    if (!_vk.Markets.AddToAlbum(-_marketId, itemId, sList))
                        throw new Exception("метод вернул false");
                } catch (Exception x) {
                    Log.Add("ошибка добавления в альбом" + x.Message);
                }
            }
        }
        //отгружаю фото на сервер вк
        private void UploadPhotos(int b, ref long mainPhoto, ref List<long> dopPhotos) {
            //количество фото ограничиваем до 5
            int numPhotos = Class365API._bus[b].images.Count > 5 ? 5 : Class365API._bus[b].images.Count;
            //отправляем каждую фотку на сервер вк
            WebClient cl = new WebClient();
            cl.Encoding = Encoding.UTF8;
            for (int u = 0; u < numPhotos; u++) {
                for (int x = 0; ; x++) {
                    try {
                        byte[] bts = cl.DownloadData(Class365API._bus[b].images[u].url);
                        File.WriteAllBytes("vk_" + u + ".jpg", bts);
                        Thread.Sleep(200);
                        break;
                    } catch (Exception ex) {
                        Log.Add("vk.com: ошибка при загрузке фото! - " + ex.Message);
                        if (x >= 3)
                            throw;
                        Thread.Sleep(30000);
                    }
                }
                try {
                    //запросим адрес сервера для загрузки фото
                    var uplSever = _vk.Photo.GetMarketUploadServer(_marketId, true, 0, 0);
                    //аплодим файл асинхронно
                    var respImg = Encoding.ASCII.GetString(
                            cl.UploadFile(uplSever.UploadUrl, Application.StartupPath + "\\" + "vk_" + u + ".jpg"));
                    //сохраняем фото в маркете
                    var photo = _vk.Photo.SaveMarketPhoto(_marketId, respImg);
                    //проверим, главное ли фото, или дополнительное
                    if (u == 0)
                        mainPhoto = photo.FirstOrDefault().Id.Value;
                    else
                        dopPhotos.Add(photo.FirstOrDefault().Id.Value);
                } catch (Exception ex) {
                    Log.Add("vk.com: " + Class365API._bus[b].name + " - ошибка загрузки фото! - " + ex.Message);
                    throw;
                }
            }
        }
        //запрос всех объявлений на ВК
        private async Task GetVKAsync() => await Task.Factory.StartNew(() => {
            int checkCount = DB.GetParamInt("vk.checkCount");
            vkMark.Clear();
            for (int v = 0; ; v++) {
                int num = vkMark.Count;
                try {
                    vkMark.AddRange(
                        _vk.Markets.Get(-_marketId, count: _pageLimitVk, offset: v * _pageLimitVk, extended: true));
                    if (num == vkMark.Count) {
                        Log.Add("vk.com: получено объявлений " + vkMark.Count);
                        DB.SetParam("vk.checkCount", num.ToString());
                        MarketCount = num;
                        Log.Add("vk.com: получено товаров " + num);
                        break;
                    }
                } catch (Exception ex) {
                    Log.Add("vk.com: ошибка при запросе товаров! - " + ex.Message);
                    break;
                }
                if (num % 1000 == 0)
                    Log.Add("vk.com: получено товаров " + num);
            }
            if (vkMark.Count != 0 && Math.Abs(checkCount - vkMark.Count) < 10)
                return;
            throw new Exception("ошибка запроса объявлений (vkMark.Count = " + vkMark.Count + ", checkCount = " + checkCount);
        });
        //проверка каталога объявлений на вк
        async Task CheckVKAsync() => await Task.Factory.StartNew(() =>  {
            var ccl = DB.GetParamInt("vk.catalogCheckLimit");
            for (int i = 0; i < vkMark.Count && ccl > 0; i++) {
                //ищем индекс в карточках товаров
                var id = vkMark[i].Id.ToString();
                int b = Class365API._bus.FindIndex(t => t.vk.Split('_').Last() == id);
                //если не найден индекс или нет на остатках, количество фото не совпадает - удаляю объявление
                if (b == -1 ||
                    Class365API._bus[b].Amount <= 0 ||
                    vkMark[i].Photos.Count != (Class365API._bus[b].images.Count > 5 ? 5 : Class365API._bus[b].images.Count)
                    //|| vkMark.Count(c => c.Title == vkMark[i].Title) > 1
                    ) {
                    try {
                        _vk.Markets.Delete(-_marketId, (long) vkMark[i].Id);
                        Log.Add("vk.com: " + b + " " + vkMark[i].Title + " - удалено! (ост. " + --ccl + ")");
                        Thread.Sleep(2000);
                    } catch (Exception x) {
                        Log.Add("vk.com: " + vkMark[i].Title + " - ошибка удаления! - " + x.Message);
                        Thread.Sleep(2000);
                    }
                //если изменилась цена, наименование или карточка товара - редактирую
                } else if (Class365API._bus[b].Price != vkMark[i].Price.Amount / 100 ||
                    Class365API._bus[b].NameLimit(_nameLimit) != vkMark[i].Title || //не совпадает название

                    Class365API._bus[b].Price >= _creditPriceMin && //цена в диапазоне
                    Class365API._bus[b].Price <= _creditPriceMax && //но описание не содержит информацию о рассрочке - обновляем
                    !vkMark[i].Description.Contains(_creditDescription) ||

                    (vkMark[i].Description.Contains("Наложенный платёж")&& ccl-- > 0) ||

                    Class365API._bus[b].IsTimeUpDated()) {
                    Edit(b);
                }
            }
        });
        //проверка актуальности ссылок в карточках бизнес.ру
        async Task CheckBusAsync() {
            UrlsCount = Class365API._bus.Count(b => b.vk.Contains("http"));
            Log.Add("vk.com: ссылок в карточках товаров " + UrlsCount);
            var ccl = DB.GetParamInt("vk.catalogCheckLimit");
            //для каждой карточки в бизнес.ру
            for (int b = 0; b < Class365API._bus.Count && ccl > 0; b++) {
                //если нет ссылки перехожу к следующей
                if (!Class365API._bus[b].vk.Contains("http"))
                    continue;
                try {
                    //получаю id объявления в вк из ссылки в карточке товара
                    var id = long.Parse(Class365API._bus[b].vk.Split('_').Last());
                    //поиск объявления в маркете
                    var vk = vkMark.Find(f => f.Id == id);
                    //если нету, удаляю ссылку
                    if (vk == null) {
                        Class365API._bus[b].vk = "";
                        await Class365API.RequestAsync("put", "goods", new Dictionary<string, string> {
                                                      {"id", Class365API._bus[b].id},
                                                      {"name", Class365API._bus[b].name},
                                                      {_url, Class365API._bus[b].vk} });
                        Log.Add("vk.com: " + Class365API._bus[b].name + " - объявление не найдено, ссылка удалена (ост. " + --ccl + ")");
                        await Task.Delay(1000);
                    }
                } catch (Exception x) {
                    Log.Add(x.Message);
                }
            }
        }
        //получаю альбомы из ВК
        private async Task GetAlbumsVKAsync() => await Task.Factory.StartNew(() => {
            //проверяем соответствие групп вк с базой
            bool f; //флаг было добавление группы в цикле - нужно пересканировать
            do {
                vkAlb.Clear();
                try {
                    vkAlb.AddRange(_vk.Markets.GetAlbums(-_marketId));
                } catch (Exception ex) {
                    throw ex;
                }
                if (vkAlb.Count == 0)
                    throw new Exception("vk.com: ошибка - группы не получены");
                f = false;
                //проверяем только группы бу запчасти
                foreach (var group in GoodObject.Groups.Where(w => w.parent_id == "205352").Select(s => s.name)) {
                    if (group != "Корневая группа")//Корневая группа не нужна в вк
                    {
                        //ищем группы из базы в группах вк
                        int ind = vkAlb.FindIndex(a => a.Title == group);
                        //если индекс -1 значит нет такой в списке
                        if (ind == -1) {
                            //добавляем группу товаров вк
                            try {
                                _vk.Markets.AddAlbum(-_marketId, group);
                                f = true;
                            } catch (Exception ex) {
                                throw ex;
                            }
                        }
                    }
                }
            } while (f);
            Log.Add("vk.com: получено " + vkAlb.Count + " альбомов");
        });
        //авторизация вк
        //получить новый ключ
        //https://oauth.vk.com/authorize?client_id=5820993&display=page&scope=offline%2Cphotos%2Cmarket&redirect_uri=https://oauth.vk.com/blank.html&response_type=token&v=5.131
        //v = версия протокола
        //client_id = ApplicationId
        private async Task IsVKAuthorizatedAsync() => await Task.Factory.StartNew(() => {
            _vk.VkApiVersion.SetVersion(5, 131);
            _vk.Authorize(new ApiAuthParams {
                ApplicationId = ulong.Parse(DB.GetParamStr("vk.applicationId")),
                Login = DB.GetParamStr("vk.login"),
                Password = DB.GetParamStr("vk.password"),
                Settings = Settings.All,
                AccessToken = DB.GetParamStr("vk.accessToken")
            });
        });
    }
}


// https://vk.com/market-23570129?w=product-23570129_552662
// 23570129 - id группы
// 552662 - id товара