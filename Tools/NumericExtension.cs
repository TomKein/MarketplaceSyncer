using System;

namespace Selen.Tools
{
    public static class NumericExtension
    {
        public static decimal Round(this decimal value, decimal precision) => Math.Round(value / precision) * precision;
        
        public static double Round(this double value, double precision) => Math.Round(value / precision) * precision;
        
        public static int Round(this int value, int precision) => (int)Math.Round((double)value / precision) * precision;
    }
}