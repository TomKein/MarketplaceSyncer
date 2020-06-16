using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenPop.Mime;
using OpenPop.Pop3;


namespace Selen
{
    class Pop3
    {
        Pop3Client _pop3 = new Pop3Client();

        public string GetPass()
        {
            Thread.Sleep(120000);
            List<Message> list = new List<Message>();
            if (!_pop3.Connected)
            {
                _pop3.Connect("pop.mail.ru", 995, true);
                _pop3.Authenticate("benderrrrrrrrrrrr@mail.ru", "drum122122");
            }
            int count = _pop3.GetMessageCount();
            for (int i = count; i > 0; i--)
            {
                Message message = _pop3.GetMessage(i);
                var texts = message.FindAllTextVersions();

                foreach (var text in texts)
                {
                    var t = text.GetBodyAsText();
                    if (t.Contains("введите код <b>"))
                    {
                        t = t.Replace("введите код <b>", "|").Split('|').Last().Replace("</b>","|").Split('|').First();
                        return t;
                    }
                }
                
                
            }
            return null;
        }

    }
}