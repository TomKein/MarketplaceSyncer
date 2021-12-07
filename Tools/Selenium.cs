using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Selen.Base;


namespace Selen.Tools {

    class Selenium {

        public readonly IWebDriver _drv;

        public Selenium(int waitSeconds=300) {
            ChromeDriverService chromeservice = ChromeDriverService.CreateDefaultService();
            chromeservice.HideCommandPromptWindow = true;
            var chromeOptions = new ChromeOptions();
            if (DB._db.GetParamBool("headlessChrome")) chromeOptions.AddArgument("headless");
            _drv = new ChromeDriver(chromeservice, chromeOptions, TimeSpan.FromSeconds(waitSeconds));
            _drv.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
            _drv.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(waitSeconds);
            Log.Add("_drv.Manage().Timeouts().PageLoad = " + _drv.Manage().Timeouts().PageLoad);
        }

        public void Navigate(string url, string check=null, int tryCount=3) {
            for (int i = 0; i < tryCount; i++) {
                try {
                    if (i > 0) {
                        _drv.Navigate().Refresh();
                        Thread.Sleep(10000);
                    }
                    _drv.Navigate().GoToUrl(url);
                    ConfirmAlert();
                    if (String.IsNullOrEmpty(check) ||
                        GetElementsCount(check) > 0) return;
                    Log.Add("selenium: неудачная попытка загрузки страницы ("+(i+1)+") - "+url+" - элемент не найден!");
                } catch (Exception x) {
                    Log.Add("selenium: неудачная попытка загрузки страницы ("+(i+1)+") - "+url+" - " +x.Message);
                }
            }
            throw new Exception("selenium: ошибка браузера! - не удается загрузить страницу " + url + " - timed out!");
        }

        public async Task NavigateAsync(string url, string check = null, int tryCount = 3) {
            await Task.Factory.StartNew(() => {
                Navigate(url,check,tryCount);
            });
        }

        public void Refresh(string url = "") {
            _drv.Navigate().Refresh();
            Thread.Sleep(10000);
            if (url.Contains("http")) _drv.Navigate().GoToUrl(url);
        }

        public void ButtonClick(string s, int sleep=1000) {
            var el = FindElements(s);
            if (el.Count > 0) {
                Actions a = new Actions(_drv);
                try {
                    a.MoveToElement(el.First()).Perform();
                    Thread.Sleep(sleep/5);
                    el.First().Click();
                    Thread.Sleep(sleep);
                } catch { }
                ConfirmAlert();
            }
        }

        public void ButtonSubmit(string s) {
            var el = FindElements(s);
            if (el.Count>0) el.First().Submit();
            Thread.Sleep(2000);
        }

        public IReadOnlyCollection<IWebElement> FindElements(string s) {
            return s.StartsWith("/")  || 
                   s.StartsWith("./") ||
                   s.StartsWith("(//") ? _drv.FindElements(By.XPath(s))
                                       : _drv.FindElements(By.CssSelector(s));
        }

        public async Task<IReadOnlyCollection<IWebElement>> FindElementsAsync(string s) {
            IReadOnlyCollection<IWebElement> res = null;
            await Task.Factory.StartNew(() => {
                res = FindElements(s);
            });
            return res;
        }


        public void WriteToSelector(string sel, string s = null, List<string> sl = null) {
            try {
                if (!(string.IsNullOrEmpty(s) && sl == null)) {
                    var el = FindElements(sel);
                    if (el.Count > 0) WriteToIWebElement(el.First(), s, sl);
                }
            } catch (Exception x){
                Log.Add(x.Message);
            }
        }

        public void WriteToIWebElement(IWebElement we, string s = null, List<string> sl = null) {
            if (sl != null) {
                Actions a = new Actions(_drv);
                a.MoveToElement(we).Click().Perform();
                Thread.Sleep(500);
                a.KeyDown(Keys.Control).SendKeys("a").KeyUp(Keys.Control).Perform();
                Thread.Sleep(500);
                a.SendKeys(Keys.Backspace).Perform();
                Thread.Sleep(500);
                foreach (var sub in sl) {
                    if (sub.Length > 0) {
                        a.SendKeys(sub);
                    }
                    a.SendKeys(Keys.Enter).Build().Perform();
                    a = new Actions(_drv);
                }
            }
            if (!string.IsNullOrEmpty(s)) {
                we.SendKeys(" ");
                Thread.Sleep(500);
                we.SendKeys(Keys.Control + "a");
                Thread.Sleep(500);
                we.SendKeys(Keys.Backspace);
                Thread.Sleep(500);
                we.SendKeys(s);
            }
            Thread.Sleep(800);
        }

        public void SendKeysToSelector(string s, string k) {
            var el = FindElements(s);
            if (el.Count>0)
                el.First().SendKeys(k);
            Thread.Sleep(1200);
        }

        public void ClickToSelector(string s) {
            var el = FindElements(s);
            if (el.Count > 0) {
                try {
                    Actions a = new Actions(_drv);
                    a.MoveToElement(el.First()).Perform();
                    Thread.Sleep(500);
                    a.Click();
                    Thread.Sleep(500);
                } catch (Exception x) {
                    Log.Add(x.Message);
                }
            }
        }

        public void ConfirmAlert() {
            try {
                _drv.SwitchTo().Alert().Accept();
                Thread.Sleep(2000);
            }
            catch(Exception x) {
                Debug.WriteLine(x.Message);
            }
        }

        public void SwitchToFrame(string s) {
            var frame = _drv.FindElement(By.CssSelector(s));
            _drv.SwitchTo().Frame(frame);
        }

        public void SwitchToMainFrame() {
            _drv.SwitchTo().DefaultContent();
        }

        public string GetUrl() {
            return _drv.Url;
        }

        public int GetElementsCount(string s) {
            return FindElements(s).Count;
        }

        public string GetElementText(string s) {
            var el = FindElements(s);
            return el.Count > 0 ? el.First().Text : "";
        }

        public string GetElementAttribute(string s, string attr) {
            var el = FindElements(s);
            return el.Count > 0 ? el.First().GetAttribute(attr) : "";
        }

        public string SaveCookies(string name=null) {
            var json = "";
            try {
                var ck = _drv.Manage().Cookies.AllCookies;
                json = JsonConvert.SerializeObject(ck);
                if (name != null && json.Length > 20) File.WriteAllText(name, json);
            } catch (Exception x) {
                Log.Add("selenium: ошибка сохранения куки - " + x.Message);
            }
            return json;
        }

        public void LoadCookies(string str) {
            try {
                string json;
                if (str.StartsWith("[") || str.StartsWith("{")) json = str;
                else json = File.ReadAllText(str);
                var cookies = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(json);
                Cookie ck;
                foreach (var c in cookies) {
                    DateTime? dt = null;
                    if (!string.IsNullOrEmpty(c["Expiry"])) {
                        dt = DateTime.Parse(c["Expiry"]);
                    }
                    ck = new Cookie(c["Name"], c["Value"], c["Domain"], c["Path"], dt);
                    _drv.Manage().Cookies.AddCookie(ck);
                }
            } catch (Exception x) {
                Log.Add("selenium: ошибка загрузки куки - " + x.Message);
            }
        }

        public void Quit() {
            try {
                _drv?.Close();
            } catch{}
        }
    }
}
