using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Selen
{
    class PhpKSort : IComparer<string>
    {
        private const int OFFSET = 1000000;
        private int Value(char c)
        {
            if ('0' <= c && c <= '9')
            {
                return 3 * OFFSET + c;
            }
            else if ('A' <= c && c <= 'Z')
            {
                return 2 * OFFSET + c;
            }
            else if ('a' <= c && c <= 'z')
            {
                return 1 * OFFSET + c;
            } else if (c == '_')
            {
                return 1 * OFFSET + c + 1000;
            } else if (c == '[' || c==']')
            {
                return 1 * OFFSET + c + 2000;
            } else
            {
                throw new ArgumentException(c + " does not have an associated value");
            }
        }
        public int Compare(string x, string y)
        {
            int ix = 0;
            while (ix < x.Length || ix < y.Length)
            {
                if (ix >= x.Length) return -1;
                if (ix >= y.Length) return 1;
                int xval = Value(x[ix]);
                int yval = Value(y[ix]);
                if (xval < yval)
                {
                    return -1;
                }
                else if (xval > yval)
                {
                    return 1;
                }
                else
                {
                    ix++;
                }
            }
            return 0;
        }
    }
}
