using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace Selen.Tools {

    class Selenium {

        public readonly IWebDriver _drv;

        public Selenium() {
            _drv = new ChromeDriver();
            _drv.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
        }

        public void Navigate(string url) {
            _drv.Navigate().GoToUrl(url);
            ConfirmAlert();
        }

        public async Task NavigateAsync(string url) {
            await Task.Factory.StartNew(() => {
                Navigate(url);
            });
        }

        public void Refresh() {
            _drv.Navigate().Refresh();
            Thread.Sleep(10000);
        }

        public void ButtonClick(string s) {
            var el = FindElements(s);
            if (el.Count > 0) {
                Actions a = new Actions(_drv);
                try {
                    a.MoveToElement(el.First()).Perform();
                    Thread.Sleep(500);
                    el.First().Click();
                    Thread.Sleep(3000);
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
            return s.StartsWith("/") || s.StartsWith("./") ? _drv.FindElements(By.XPath(s))
                                                           : _drv.FindElements(By.CssSelector(s));
        }

        public void WriteToSelector(string sel, string s = null, List<string> sl = null) {
            if (s != null || sl != null) {
                var el = FindElements(sel);
                if(el.Count>0) WriteToIWebElement(el.First(), s, sl);
            }
        }

        public void WriteToIWebElement(IWebElement we, string s = null, List<string> sl = null) {
            if (sl != null) {
                Actions a = new Actions(_drv);
                a.MoveToElement(we).Click().Perform();
                Thread.Sleep(1000);
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
            Thread.Sleep(2000);
        }

        public void SendKeysToSelector(string s, string k) {
            var el = FindElements(s);
            if (el.Count>0)
                el.First().SendKeys(k);
            Thread.Sleep(1000);
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
                    Debug.WriteLine(x.Message);
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

        public void SaveCookies(string name) {
            try {
                var ck = _drv.Manage().Cookies.AllCookies;
                var json = JsonConvert.SerializeObject(ck);
                File.WriteAllText(name, json);
            } catch (Exception x) {
                Debug.WriteLine("Ошибка сохранения куки " + name + "\n" + x.Message);
            }
        }

        public void LoadCookies(string name) {
            try {
                var json = File.ReadAllText(name);
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
                Debug.WriteLine("Ошибка загрузки куки " + name + "\n" + x.Message);
            }
        }

        public void Quit() {
            _drv?.Close();
        }
    }
}
