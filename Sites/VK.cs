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
    class VK {
        DB _db;
        public List<MarketAlbum> vkAlb = new List<MarketAlbum>();
        public List<Market> vkMark = new List<Market>();
        VkApi _vk = new VkApi();
        long _marketId;
        int _pageLimitVk;
        string _url;
        string[] _dopDesc;
        string[] _dopDesc2;
        int _addCount;
        List<RootObject> _bus = null;


        public async Task VkSyncAsync(List<RootObject> bus) {
            _bus = bus;
            await GetParamsAsync();
            await IsVKAuthorizatedAsync();
            await GetAlbumsVKAsync();
            Log.Add("vk.com: получено " + vkAlb.Count + " альбомов");
            if (vkAlb.Count > 0) {
                await GetVKAsync();
                await EditVKAsync();
                await AddVKAsync();
            }
        }

        async Task GetParamsAsync() {
            await Task.Factory.StartNew(() => {
                _db = DB._db;
                _marketId = _db.GetParamLong("vk.marketId");
                _pageLimitVk = _db.GetParamInt("vk.pageLimit");
                _url = _db.GetParamStr("vk.url");
                _dopDesc = JsonConvert.DeserializeObject<string[]>(_db.GetParamStr("vk.dopDesc"));
                _dopDesc2 = JsonConvert.DeserializeObject<string[]>(_db.GetParamStr("vk.dopDesc2"));
                _addCount = _db.GetParamInt("vk.addCount");
            });
        }

        private async Task AddVKAsync() {
            for (int i = 0; i < _bus.Count && _addCount > 0; i++) {
                if (_bus[i].images.Count > 0 && _bus[i].tiu.Contains("http")
                    && _bus[i].price > 0 && _bus[i].amount > 0) {
                    int indVk = 0;
                    await Task.Factory.StartNew(() => {
                        indVk = vkMark.FindIndex(t => _bus[i].vk.Contains(t.Id.ToString()));
                    });
                    if (indVk < 0) {
                        //если индекс не найден значит надо выложить
                        var task = Task.Factory.StartNew(() => {
                            //количество фото ограничиваем до 5
                            int numPhotos = _bus[i].images.Count > 5 ? 5 : _bus[i].images.Count;
                            int vkAlbInd = vkAlb.FindIndex(a => a.Title == _bus[i].GroupName());
                            //переменные для хранения индексов фото, загруженных на сервер вк
                            long mainPhoto = 0;
                            List<long> dopPhotos = new List<long>();
                            //отправляем каждую фотку на сервер вк
                            WebClient cl = new WebClient();
                            cl.Encoding = Encoding.UTF8;
                            for (int u = 0; u < numPhotos; u++) {
                                for (int x = 0; ; x++) {
                                    try {
                                        byte[] bts = cl.DownloadData(_bus[i].images[u].url);
                                        File.WriteAllBytes("vk_" + u + ".jpg", bts);
                                        Thread.Sleep(200);
                                        break;
                                    } catch (Exception ex) {
                                        Log.Add("vk.com: ошибка при загрузке фото! - " + ex.Message);
                                        if (x == 3) throw;
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
                                    if (u == 0) //first photo
                                    {
                                        mainPhoto = photo.FirstOrDefault().Id.Value;
                                    } else {
                                        dopPhotos.Add(photo.FirstOrDefault().Id.Value);
                                    }
                                } catch (Exception ex) {
                                    Log.Add("vk.com: ошибка при загрузке фото! - " + ex.Message);
                                }
                            }
                            //меняем доп описание
                            string desc = _bus[i].DescriptionList(dop: _dopDesc).Aggregate((a,b)=>a+"\n"+b)+"\n";
                            desc += _dopDesc2.Aggregate((a, b) => a + "\n" + b);
                            //выкладываем товар вк!
                            //создаем товар
                            long itemId = _vk.Markets.Add(new MarketProductParams {
                                OwnerId = -_marketId,
                                Name = _bus[i].name,
                                Description = desc,
                                CategoryId = 404,
                                Price = _bus[i].price,
                                MainPhotoId = mainPhoto,
                                PhotoIds = dopPhotos,
                                Deleted = false
                            });
                            //привяжем к альбому если есть индекс
                            if (vkAlbInd > -1) {
                                List<long> sList = new List<long>();
                                sList.Add((long)vkAlb[vkAlbInd].Id);
                                _vk.Markets.AddToAlbum(-_marketId, itemId, sList);
                            }
                            _bus[i].vk = "https://vk.com/market-23570129?w=product-23570129_" + itemId;
                        });
                        try {
                            _addCount--;
                            await task;
                            await Class365API.RequestAsync("put", "goods", new Dictionary<string, string> {
                                                    {"id", _bus[i].id},
                                                    {"name", _bus[i].name},
                                                    {_url, _bus[i].vk}
                                        });
                            Log.Add("vk.com: "+ _bus[i].name + " - добавлено");
                        } catch (Exception ex) {
                            Log.Add("vk.com: " + _bus[i].name + " - ошибка при добавлении! " + ex.Message);
                        }
                    }
                }
            }
        }

        private async Task GetVKAsync() {
            await Task.Factory.StartNew(() => {
                vkMark.Clear();
                for (int v = 0; ; v++) {
                    int num = vkMark.Count;
                    try {
                        vkMark.AddRange(_vk.Markets.Get(-_marketId, count: _pageLimitVk, offset: v * _pageLimitVk, extended:true));
                        if (num == vkMark.Count) break;
                        Log.Add("vk.com: получено " + vkMark.Count + " товаров");
                    } catch (Exception ex) {
                        Log.Add("vk.com: ошибка при запросе товаров! " + ex.Message);
                        Thread.Sleep(10000);
                        v--;
                    }
                }
                Log.Add("vk.com: получено " + vkMark.Count + " товаров");
            });
        }
        private async Task EditVKAsync() {
            await Task.Factory.StartNew(() => {
                for (int i = 0; i < vkMark.Count; i++) {
                    //для каждого товара поищем индекс в базе карточек товаров
                    int b = _bus.FindIndex(t => t.vk.Split('_').Last()== vkMark[i].Id.ToString());
                    //если не найден индекс или нет на остатках, количество фото не совпадает или есть дубли - удаляю объявление
                    if (b == -1 ||
                        _bus[b].amount <= 0 ||
                        vkMark[i].Photos.Count != (_bus[b].images.Count > 5?5: _bus[b].images.Count) ||
                        vkMark.Count(c => c.Title == vkMark[i].Title) > 1) {
                        try {
                            _vk.Markets.Delete(-_marketId, (long)vkMark[i].Id);
                            Log.Add("vk.com: "+ vkMark[i].Title + " - удалено!");
                            vkMark.Remove(vkMark[i]);
                            Thread.Sleep(1000);
                            i--;
                        } catch (Exception x) {
                            Log.Add("vk.com: " + vkMark[i].Title + " - ошибка удаления! - "+x.Message);
                        }
                    //если изменилась цена, наименование или карточка товара - редактирую
                    } else if (_bus[b].price != vkMark[i].Price.Amount / 100 ||
                        _bus[b].name != vkMark[i].Title ||
                        _bus[b].IsTimeUpDated()) {
                        try {
                            var param = new MarketProductParams();
                            param.Name = _bus[b].name;
                            var desc = _bus[b].DescriptionList(dop: _dopDesc);
                            desc.AddRange(_dopDesc2);
                            param.Description = desc.Aggregate((a1,a2)=>a1+"\n"+a2);
                            param.CategoryId = (long)vkMark[i].Category.Id;
                            param.ItemId = vkMark[i].Id;
                            param.OwnerId = (long)vkMark[i].OwnerId;
                            param.MainPhotoId = (long)vkMark[i].Photos[0].Id;
                            param.PhotoIds = vkMark[i].Photos.Skip(1).Select(s=>(long)s.Id);
                            param.Price = (decimal) _bus[b].price;
                            _vk.Markets.Edit(param);
                            Log.Add("vk.com: " + vkMark[i].Title + " - обновлено");
                            Thread.Sleep(1000);
                        } catch (Exception x) {
                            Log.Add("vk.com: " + vkMark[i].Title + " - ошибка редактирования! - " + x.Message);
                        }
                    }
                }
            });
        }

        private async Task GetAlbumsVKAsync() {
            await Task.Factory.StartNew(() => {
                //2. проверяем соответствие групп вк с базой
                bool f; //флаг было добавление группы в цикле - нужно пересканировать
                do {
                    vkAlb.Clear();
                    try {
                        vkAlb.AddRange(_vk.Markets.GetAlbums(-_marketId));
                    } catch (Exception ex) {
                        throw ex;
                    }
                    f = false;
                    //проверяем только группы бу запчасти
                    foreach (var group in RootObject.Groups.Where(w=>w.parent_id == "205352").Select(s=>s.name)) {
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
            });
        }

        private async Task IsVKAuthorizatedAsync() {
            //получить новый ключ авторизации по ссылке
            //https://oauth.vk.com/authorize?client_id=5820993&display=page&scope=offline%2Cphotos%2Cmarket&redirect_uri=https://oauth.vk.com/blank.html&response_type=token&v=5.131
            //v = версия протокола
            //client_id = ApplicationId
            await Task.Factory.StartNew(() => {
                _vk.Authorize(new ApiAuthParams {
                    ApplicationId = ulong.Parse(_db.GetParamStr("vk.applicationId")),
                    Login = _db.GetParamStr("vk.login"),
                    Password = _db.GetParamStr("vk.password"),
                    Settings = Settings.All,
                    AccessToken = _db.GetParamStr("vk.accessToken")
                });
            });
        }
    }
}


// https://vk.com/market-23570129?w=product-23570129_552662
// 23570129 - id группы
// 552662 - id товара