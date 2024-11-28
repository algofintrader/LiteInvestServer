using System.Runtime.Serialization;
using System.Xml;

namespace LiteInvestServer.Helpers
{
    public class Helper
    {
        public static void SaveXml<T>(T serializableObject, string name)
        {

            var settings = new XmlWriterSettings()
            {
                Indent = true,
                IndentChars = "\t",
            };

            var writer = XmlWriter.Create(name, settings);
            new DataContractSerializer(typeof(T)).WriteObject(writer, serializableObject);
            writer.Close();

        }

        /// <summary>
        /// Чтение из XML
        /// Если такого файла нет, то создает пустой 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <returns></returns>
        public static T? ReadXml<T>(string name)
        {
            try
            {
                var fileStream = new FileStream(name, FileMode.Open);
                var reader = XmlDictionaryReader.CreateTextReader(fileStream, new XmlDictionaryReaderQuotas());
                var serializer = new DataContractSerializer(typeof(T));
                T serializableObject = (T)serializer.ReadObject(reader, true);
                reader.Close();
                fileStream.Close();
                return serializableObject;
            }
            catch (Exception ex)
            {
                //TODO: Крайне жесткое место, если вылетает ошибка, то это пизда.
                //Диспоуза нет и потом запись в это же место не работает лол. 
                Console.WriteLine($"Fatal error reading {name}");
                return (T)Activator.CreateInstance(typeof(T));
            }
        }

        public static System.Timers.Timer CreateTimerAndStart(Action method, int ms, bool repeat = true)
        {
            var timer = new System.Timers.Timer(ms) { AutoReset = repeat };
            timer.Elapsed += (s, e) =>
            {
                method?.Invoke();
            };
            timer.Start();
            return timer;
        }

        public static bool isSimulation()
        {
            if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday || DateTime.Now.DayOfWeek == DayOfWeek.Sunday)
                return true;

            return false;
            //return true;
        }


    }
}
