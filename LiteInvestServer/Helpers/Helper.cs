using System.Runtime.Serialization;
using System.Xml;

namespace LiteInvestServer.Helpers
{
    public class Helper
    {
        public static void SaveXml<T>(T serializableObject, string name)
        {
            var serializer = new DataContractSerializer(typeof(T));
            var settings = new XmlWriterSettings()
            {
                Indent = true,
                IndentChars = "\t",
            };

            var writer = XmlWriter.Create(name, settings);
            serializer.WriteObject(writer, serializableObject);
            writer.Close();
        }
        public static T? ReadXml<T>(string name)
        {
            var fileStream = new FileStream(name, FileMode.Open);
            var reader = XmlDictionaryReader.CreateTextReader(fileStream, new XmlDictionaryReaderQuotas());
            var serializer = new DataContractSerializer(typeof(T));
            T serializableObject = (T)serializer.ReadObject(reader, true);
            reader.Close();
            fileStream.Close();
            return serializableObject;
        }


    }
}
