using System;
using System.Threading.Tasks;

namespace PlazaEngine.Entity
{
    /// <summary>
    /// number generator for deals and orders inside the robot
    /// генератор номеров для сделок и ордеров внутри робота
    /// </summary>
    public static class PLazaHelper
    {
        

        /// <summary>
        /// current number of the last order
        /// текущий номер последнего ордера
        /// </summary>
        private static int _numberOrderForRealTrading =0;


        private static object _locker = new object();


        /// <summary>
        /// take the order number
        /// Получить уникальный номер для ордера
        /// </summary>
        public static int CreateHashId()
        {
            return GetNumberOrderForRealTrading();
        }

        private static int lastDayGetNumber = 0;


        //NOTE: До сих пор не понял, насколько этот номер уникальный 
        //Может его просто сохранять
        private static int GetNumberOrderForRealTrading()
        {
            lock (_locker)
            {
                int dayNow = DateTime.UtcNow.AddHours(3).Day;
                int numberOrderForRealTrading = dayNow * 64 * 1000000 + (int)Math.Floor(DateTime.UtcNow.AddHours(3).TimeOfDay.TotalMilliseconds);
                if (numberOrderForRealTrading <= _numberOrderForRealTrading && lastDayGetNumber <= dayNow)
                {
                    numberOrderForRealTrading = _numberOrderForRealTrading + 1;
                }
                lastDayGetNumber = dayNow;
                _numberOrderForRealTrading = numberOrderForRealTrading;
            }
            return _numberOrderForRealTrading;
        }

        /// <summary>
        /// Текущее московское время
        /// </summary>
        /// <returns></returns>
        public static DateTime GetTimeMoscowNow()
        {
            return TimeZoneInfo.ConvertTimeFromUtc
            (DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time"));
        }
    }
}
