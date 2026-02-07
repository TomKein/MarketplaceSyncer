using System.Diagnostics;
using System.Threading.Tasks;
using Selen.Base;
using System.Windows.Forms;
using System.IO;
using System.Threading;

namespace Selen.Sites {
    public class SyncWorker {
        public Wildberries _wb;
        public OzonApi _ozon;
        public YandexMarket _ym;
        public Avito _avito;
        public Drom _drom;
        public MegaMarket _mm;
        public VK _vk;
        public Izap24 _izap24;

        public static event SyncEventDelegate updatePropertiesEvent = null;
        public SyncWorker() {
            _drom = new Drom();
            _izap24 = new Izap24();
            _ozon = new OzonApi();
            _avito = new Avito();
            _vk = new VK();
            _mm = new MegaMarket();
            _wb = new Wildberries();
            _ym = new YandexMarket();
            if (IsUpdated()) return;
            Class365API.StartSync();
            Class365API.syncAllEvent += SyncAll;        
        }
        async Task SyncAll() {
            await _ozon.MakeReserve();
            await _wb.MakeReserve();
            await _ym.MakeReserve();
            await _avito.MakeReserve();
            //await _mm.MakeReserve();  //TODO mm reserve?
            await _drom.MakeReserve();

            _wb.SyncAsync();
            _drom.Sync();
            _avito.Sync();
            _ym.Sync();
            _mm.Sync();
            _ozon.Sync();
            _izap24.Sync();
            _vk.Sync();

            await Class365API.AddPartNums();
            await Class365API.CheckArhiveStatus();
            await Class365API.ClearOldUrls();
            await Class365API.CheckGrammarOfTitlesAndDescriptions();
            if (Class365API.SyncStartTime.Minute < Class365API._checkIntervalMinutes) {
                await Class365API.CheckReserve();
                await Class365API.CheckDubles();
                await Class365API.CheckMultipleApostrophe();
                await Class365API.PartsCorrection();
                await Class365API.GroupsMoveAsync();                //проверка групп
                await Class365API.PhotoClearAsync();                //удаление старых фото
                await Class365API.ArchivateAsync();                 //архивирование карточек
            }
            await Class365API.CheckRealisationsAsync();             //проверка реализаций, добавление расходов
            //await Class365API.ChangePostingsPrices();               //корректировка цены закупки в оприходованиях


            await WaitSitesSync();
            Class365API.LastScanTime = Class365API.SyncStartTime;
        }
        async Task WaitSitesSync() {
            while (
                _wb.IsSyncActive ||
                _ozon.IsSyncActive ||
                _ym.IsSyncActive ||
                _avito.IsSyncActive ||
                _drom.IsSyncActive ||
                _vk.IsSyncActive ||
                _mm.IsSyncActive ||
                _izap24.IsSyncActive
            ) {
                updatePropertiesEvent?.Invoke();
                await Task.Delay(5000);
            }
        }

        private bool IsUpdated() {
            var actualVersion = DB.GetParamInt("version");
            if (int.Parse(Class365API.VERSION) < actualVersion) {
                var url = DB.GetParamStr("newVersionUrl");
                var res = MessageBox.Show($"Необходимо обновление программы! Нажмите ОК для скачивания!",
                                          $"Доступна новая версия 1.{actualVersion}", MessageBoxButtons.OKCancel);
                if (res == DialogResult.OK) {
                    var ps = new ProcessStartInfo(url) {
                        //UseShellExecute = false,
                        UseShellExecute = true,
                        Verb = "open"
                    };
                    Process.Start(ps);
                    Thread.Sleep(10000);
                }
                return true;
            }
            return false;
        }
        public void Stop() {
            ClearTempFiles();
            _drom?.Quit();
        }
        void ClearTempFiles() {
            foreach (var file in Directory.EnumerateFiles(System.Windows.Forms.Application.StartupPath, "*.jpg")) {
                File.Delete(file);
            }
        }
    }
}
