using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlazaEngine.Entity
{
    internal static class MessageUnicodeConverter
    {
        internal static string asUnicodeString(this ru.micexrts.cgate.message.Value value)
        {
            string s = "";
            try
            {
                var t = value.GetType();
                if (t.Name == "ValueCXX")
                {
                    var b = value.asBytes();
                    s = Encoding.UTF8.GetString(Encoding.Convert(Encoding.GetEncoding(1251), Encoding.GetEncoding(65001), b));
                }
                else
                {
                    s = value.asString();
                    Debug.WriteLine(t.Name);
                }
            }
            catch 
            {
                s = value.asString();
            }
            return s;
        }
    }
}
