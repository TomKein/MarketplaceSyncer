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

namespace Selen.Sites {
    class VK {
        public List<MarketAlbum> vkAlb = new List<MarketAlbum>();
        public List<Market> vkMark = new List<Market>();
        VkApi _vk = new VkApi();
        long marketId = 23570129;
        int pageLimitVk = 200;
        public int AddCount { get; set; }
        List<RootObject> _bus = null;

        string dopDescVk = "Дополнительные фотографии по запросу\n" +
                   "Есть и другие запчасти на данный автомобиль!\n" +
                   "Вы можете установить данную деталь на нашем АвтоСервисе с доп.гарантией и со скидкой!\n" +
                   "Перед выездом обязательно уточняйте наличие запчастей по телефону - товар на складе!\n" +
                   "Время на проверку и установку - 2 недели!\n" +
                   "Отправляем наложенным платежом ТК СДЭК (только негабаритные посылки)\n" +
                   "Бесплатная доставка до транспортной компании!\n";

        string dopDescVk2 = "АДРЕС: г.Калуга, ул. Московская, д. 331\n" +
                            "ТЕЛ: 8-920-899-45-45, 8-910-602-76-26, Viber/WhatsApp 8-962-178-79-15\n" +
                            "Звонить строго с 9-00 до 19-00 (воскресенье-выходной)";

        public async Task VkSyncAsync(List<RootObject> bus) {
            _bus = bus;
            await IsVKAuthorizatedAsync();
            await GetAlbumsVKAsync();
            //ToLog("получено альбомов вк " + vkAlb.Count);
            if (vkAlb.Count > 0) {
                //3. получим товары из вк
                await GetVKAsync();
                //label_vk.Text = vkMark.Count.ToString();
                //ToLog("Получено товаров вк " + vkMark.Count);
                //4. удаляем объявления, которых нет в базе товаров, или они обновлены
                await DeleteVKAsync();
                //5. выкладываем товары вк из тех, которые имеют фото, есть ссылка тиу, цена и кол-во
                await AddVKAsync();
            }
        }

        private async Task AddVKAsync() {
            for (int i = 0; i < _bus.Count && AddCount > 0; i++) {
                if (_bus[i].images.Count > 0 && _bus[i].tiu.Contains("http")
                    && _bus[i].price > 0 && _bus[i].amount > 0) {
                    int indVk = 0;
                    await Task.Factory.StartNew(() => { indVk = vkMark.FindIndex(t => _bus[i].vk.Contains(t.Id.ToString())); });
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
                                for (int x = 0; x < 3; x++) {
                                    try {
                                        byte[] bts = cl.DownloadData(_bus[i].images[u].url);
                                        File.WriteAllBytes("vk_" + u + ".jpg", bts);
                                        break;
                                    } catch (Exception ex) {
                                        if (x == 3) throw;
                                        Thread.Sleep(30000);
                                    }
                                }
                                try {
                                    //запросим адрес сервера для загрузки фото
                                    var uplSever = _vk.Photo.GetMarketUploadServer(marketId, true, 0, 0);
                                    //аплодим файл асинхронно
                                    var respImg = Encoding.ASCII.GetString(
                                            cl.UploadFile(uplSever.UploadUrl, Application.StartupPath + "\\" + "vk_" + u + ".jpg"));
                                    //сохраняем фото в маркете
                                    var photo = _vk.Photo.SaveMarketPhoto(marketId, respImg);
                                    //проверим, главное ли фото, или дополнительное
                                    if (u == 0) //first photo
                                    {
                                        mainPhoto = photo.FirstOrDefault().Id.Value;
                                    } else {
                                        dopPhotos.Add(photo.FirstOrDefault().Id.Value);
                                    }
                                } catch {
                                    //throw;
                                }
                            }
                            //меняем доп описание
                            string desc = _bus[i].description.Replace("&nbsp;", " ")
                                        .Replace("&quot;", "")
                                        .Replace("</p>", "\n")
                                        .Replace("<p>", "")
                                        .Replace("<br>", "\n")
                                        .Replace("<br />", "\n")
                                        .Replace("<strong>", "")
                                        .Replace("</strong>", "")
                                        .Replace(" &gt", "")
                                        .Replace("Есть", "|")
                                        .Split('|')[0];
                            //если группа не автохимия и не колеса, добавляем описсание есть и другие запчасти на данный автомобиль...
                            if (_bus[i].IsGroupValid()) {
                                desc += dopDescVk;
                            }
                            desc += dopDescVk2;
                            //выкладываем товар вк!
                            //создаем товар
                            long itemId = _vk.Markets.Add(new MarketProductParams {
                                OwnerId = -marketId,
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
                                _vk.Markets.AddToAlbum(-marketId, itemId, sList);
                            }
                            _bus[i].vk = "https://vk.com/market-23570129?w=product-23570129_" + itemId;
                            _bus[i].updated = DateTime.Now.AddMinutes(-224).ToString();

                        });
                        try {
                            AddCount--;
                            await task;
                            await Class365API.RequestAsync("put", "goods", new Dictionary<string, string> {
                                                    {"id", _bus[i].id},
                                                    {"name", _bus[i].name},
                                                    {"209360", _bus[i].vk}
                                        });
                            //ToLog("вк добавлено успешно!! " + newVk + "\n" + bus[i].name);
                            //label_vk.Text = (vkMark.Count + ++newVk).ToString();
                        } catch (Exception ex) {
                            //ToLog("вк ошибка добавления товара\n" + bus[i].name + "\n" + ex.Message);
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
                        vkMark.AddRange(_vk.Markets.Get(-marketId, count: pageLimitVk, offset: v * pageLimitVk));
                        Thread.Sleep(600);
                        if (num == vkMark.Count) break;
                    } catch (Exception ex) {
                        //ToLog("вк ошибка запроса товаров\n" + ex.Message);
                        Thread.Sleep(10000);
                        v--;
                    }
                }
            });
        }

        private async Task DeleteVKAsync() {
            await Task.Factory.StartNew(() => {
                for (int i = 0; i < vkMark.Count; i++) {

                    //для каждого товара поищем индекс в базе карточек товаров
                    int iBus = _bus.FindIndex(t => t.vk.EndsWith(vkMark[i].Id.ToString()));
                    //если не найден - значит удаляю объявление
                    var vkDate = vkMark[i].Date;
                    //var busDate = bus[iBus].updated;
                    if (iBus == -1 ||
                        _bus[iBus].price != vkMark[i].Price.Amount / 100 ||
                        _bus[iBus].amount <= 0 ||
                        _bus[iBus].name != vkMark[i].Title ||
                        DateTime.Parse(_bus[iBus].updated).AddMinutes(-223).CompareTo(vkDate) > 0
                        //|| vkMark.Count(c => c.Title == vkMark[i].Title) > 1
                        //|| vkMark[i].Description.Contains("70-70-97")
                        ) {
                        //удаляем из вк
                        try {
                            _vk.Markets.Delete(-marketId, (long)vkMark[i].Id);
                            Thread.Sleep(330);
                            vkMark.Remove(vkMark[i]);
                            //ToLog("удалено из вк:\n" + vkMark[i].Title);
                            //label_vk.Text = vkMark.Count.ToString();
                            i--;
                        } catch {
                            //ToLog("вк не удалось удалить\n" + bus[iBus].name);
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
                        vkAlb.AddRange(_vk.Markets.GetAlbums(-marketId));
                    } catch (Exception ex) {
                        throw ex;
                    }
                    f = false;
                    foreach (var group in RootObject.Groups.Select(s=>s.name)) {
                        if (group != "Корневая группа") //Корневая группа не нужна в вк
                        {
                            //ищем группы из базы в группах вк
                            int ind = vkAlb.FindIndex(a => a.Title == group);
                            //если индекс -1 значит нет такой в списке
                            if (ind == -1) {
                                //добавляем группу товаров вк
                                try {
                                    _vk.Markets.AddAlbum(-marketId, group);
                                    Thread.Sleep(1000);
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
            // https://vk.com/market-23570129?w=product-23570129_552662
            // 23570129 - id группы
            // 552662 - id товара
            await Task.Factory.StartNew(() => {
                _vk.Authorize(new ApiAuthParams {
                    ApplicationId = 5820993,
                    Login = "rogachev.aleksey@gmail.com",
                    Password = "$drum122",
                    Settings = Settings.All,
                    AccessToken = "ba600fc83b526e417526749b992277da60ac125353644a2dd32f4eceb1527e130a75ce872ec706555a6dc"
                });
            });
        }
    }
}