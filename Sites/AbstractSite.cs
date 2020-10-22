//пока не используем абстратный тип


using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Selen.Sites {
    public abstract class AbstractSite {
        protected string _login { get; set; }
        protected string _password { get; set; }

        protected IWebDriver _wd;
        protected bool _isAuthorized {get;set;}
        internal List<Offer> Offers { get; set; }

        protected AbstractSite(IWebDriver webDriver) {
            _wd = webDriver;
            _isAuthorized = false;
            Offers = new List<Offer>();
        }

        public abstract void Authorization();
        public abstract void OffersCheck();
        public abstract void OfferAdd();
        public abstract void OfferUpdate();
        public abstract void OfferClose();

        protected void WriteToIWebElement(IWebElement we, string s = null, List<string> sl = null) {
            Actions a = new Actions(_wd);
            if (sl != null) {
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
                    a.SendKeys(Keys.Enter);
                }
                a.Perform();
            }
            if (!string.IsNullOrEmpty(s)) {
                we.SendKeys(" ");
                Thread.Sleep(200);
                we.SendKeys(Keys.Control + "a");
                Thread.Sleep(200);
                we.SendKeys(Keys.Backspace);
                Thread.Sleep(200);
                we.SendKeys(s);
            }
            Thread.Sleep(500);
        }


    }
}
