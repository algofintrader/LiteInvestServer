using Amazon.Runtime.Internal.Transform;
using Fleck;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using MongoDB.Bson.IO;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Web;

namespace LiteInvestServer.WebScoketFactory
{

    public enum ParameterTypes
    {
        Instrument,
        Key
    }

    public class ParameterKey
    {
        public ParameterKey(string _name, ParameterTypes _parameterTypes)
        {
            Key = _name;
            Type = _parameterTypes;
        }

        /// <summary>
        /// Имя, которое выступает неким ключом
        /// </summary>
        public string Key { get; private set; }
        public ParameterTypes Type { get; private set; }


    }

    public class WebSocketSettings
    {

        public WebSocketSettings(List<ParameterKey> _parameters)
        {

            //NOTE: Нет проверки на то, чтобы маркет ключ был один. 

            if (_parameters == null)
                throw new ArgumentNullException("parameters");

            var keyparamenter = _parameters.FirstOrDefault(p => p.Type == ParameterTypes.Key);

            if (_parameters.Count == 0 || keyparamenter==null)
                throw new Exception("Parameter must have one value, which will be KEY for Sockets!");

            ParameterKeys = _parameters;

          
        }

        /// <summary>
        /// Свободное имя, для логов в основном
        /// </summary>
        public string Name { get; set; }

        public List<ParameterKey> ParameterKeys { get; private set; } = new ();

        /// <summary>
        /// Первый из параметров является ключом
        /// Не стал писать сложную реализацию
        /// </summary>
        public string Key => ParameterKeys.FirstOrDefault(p => p.Type == ParameterTypes.Key).Key;

        public bool CheckAllParameters(IDictionary<string, string> parameters)
        {
            foreach(var parameter in parameters)
            {
                if(!parameters.ContainsKey(parameter.Key))
                {
                    return false;
                }
            }

            return true;
        }

        public ConcurrentDictionary<string,  ConcurrentDictionary<int, IWebSocketConnection>> Sockets { get; private set; } = new();
    

        /// <summary>
        /// В основном используется для проверки регистрации маркет даты
        /// </summary>
        public bool NeedDataRegistration { get => Register_Unregister_MarketData == null ? false : true; }


        public Action<string,bool> Register_Unregister_MarketData { get; set; }
    
    }
    public class WebSocketEngine
    {

        private static string webscoketAdress { get; set; }

        private WebSocketServer server;

        private string _streamKEY = "stream";

        public WebSocketEngine(string _websocketAdress)
        {
            webscoketAdress = _websocketAdress;

            server = new WebSocketServer(webscoketAdress);

            server.RestartAfterListenError = true;
            server.ListenerSocket.NoDelay = true;
        }

        /// <summary>
        /// Возвращает сокеты, где ключ... является основным ключом потока!
        /// </summary>
        /// <param name="streamNameKey"></param>
        /// <returns></returns>
        public ConcurrentDictionary<string, ConcurrentDictionary<int, IWebSocketConnection>> GetSocketsFull(string streamNameKey)
        {
            try
            {
                return Streams[streamNameKey].Sockets;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public ConcurrentDictionary<int, IWebSocketConnection> GetSockets(string streamNameKey,string key)
        {
            try
            {
                var res= Streams[streamNameKey].Sockets[key];
                return res;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public void Start()
        {
            //не самая коненчно лучшая концепция программирования 
            //Если сокет вдруг разоврется во время работы, то он сам его отключит.
            server.Start(ws =>
            {
                //ПРИХОДЯЩИЙ СТРИМ приходит сюда 
                var WsParameters = GetParameters(ws);
                var streamValue = WsParameters[_streamKEY];

                //у нас такой стрим есть

                //КАК ПОЛУЧИТЬ СОКЕТ!!! выдается список. 
                //var listofsocketsforsubcription = Streams["имя стрима"].Sockets["ключ поток"];

                if (Streams.ContainsKey(streamValue))
                {
                    var stream = Streams[streamValue];

                    //проверяем что есть все поля, которые нам нужны. 

                    if (!stream.CheckAllParameters(WsParameters))
                    {
                        ws.Close();
                        return;
                    }

                    var paramName = stream.ParameterKeys.FirstOrDefault(p => p.Type == ParameterTypes.Key);
                    var KEY = WsParameters[paramName.Key];

                    //TODO: Переписать ключ!!!! 

                    if (!stream.Sockets.ContainsKey(KEY))
                        stream.Sockets.TryAdd(KEY, new());

                    var hash = ws.GetHashCode();

                    stream.Sockets[KEY][hash] = ws;

                    //TODO: Дублирующий код 
                    if (stream.NeedDataRegistration)
                    {
                        stream.Register_Unregister_MarketData?.Invoke(KEY,true);
                        Console.WriteLine($"Registering marker data! {stream.Name}  = {KEY} hash = {hash}");
                    }

                    Console.WriteLine($"WebSocket for {stream.Name}  Opened hash = {hash}");

                    ws.OnClose += OnClosingWebSocket;
                }
                else
                {
                    ws.Close();
                    return;
                }
            });
        }

        private void OnClosingWebSocket(IWebSocketConnection ws)
        {
            //Закрывающий СТРИМ приходит сюда 
            var WsParameters = GetParameters(ws);
            var streamValue = WsParameters[_streamKEY];
            var hash = ws.GetHashCode();

            //у нас такой стрим есть
            if (Streams.ContainsKey(streamValue))
            {
                var stream = Streams[streamValue];

                var paramName = stream.ParameterKeys.FirstOrDefault(p => p.Type == ParameterTypes.Key);
                var KEY = WsParameters[paramName.Key];

                stream.Sockets[KEY].Remove(hash, out _);

                //если нет больше подписантов и нет смысла держать маркет дату
                if (stream.NeedDataRegistration && stream.Sockets[KEY].Count ==0)
                {
                    stream.Register_Unregister_MarketData?.Invoke(KEY, false);
                    Console.WriteLine($"Unregistering for {stream.Name} stream = {KEY} hash = {hash}");
                }
                Console.WriteLine($"WebSocket for {stream.Name} hash = {hash}");
            }
        }

        ConcurrentDictionary<string, WebSocketSettings> Streams = new();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="streamNameKey">stream key</param>
        /// <param name="_name">Свободное имя, ни к чему не привязано.</param>
        /// <param name="_parameters"></param>
        public void AddStream(string streamNameKey, string _name, List<ParameterKey> _parameters,Action <string,bool> register_unregister_data=null)
        {
            Streams.TryAdd(streamNameKey, new WebSocketSettings(_parameters) { Name = _name, Register_Unregister_MarketData = register_unregister_data });
        }


        static Dictionary<string, string> GetParameters(IWebSocketConnection websocket)
        {
            Uri myUri = new Uri(webscoketAdress + websocket.ConnectionInfo.Path);
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            var resultParameters = HttpUtility.ParseQueryString(myUri.Query);
            foreach (string item in resultParameters.Keys)
            {
                parameters.Add(item, resultParameters.Get(item));
            };
            return parameters;
        }

        //UseUserName
    }
}
