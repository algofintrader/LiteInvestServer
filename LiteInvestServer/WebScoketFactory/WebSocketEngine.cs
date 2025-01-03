﻿
using Fleck;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Claims;
using System.Web;
using LiteInvest.Entity.ServerEntity;

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

        private string _authname;
        private static string webscoketAdress { get; set; }
        private JwtOptions jwtoptions { get; set; }
        private ConcurrentDictionary<string, User> UsersContext { get; set; }

        private WebSocketServer server;

        private string _streamKEY = "stream";

        public WebSocketEngine(string _websocketAdress, string nameForAuth,JwtOptions _jwtoptions, 
            ConcurrentDictionary<string, User> usersContext)
        {
            webscoketAdress = _websocketAdress;
            _authname = nameForAuth;
            jwtoptions = _jwtoptions;
            UsersContext = usersContext;

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
                
                Streams[streamNameKey].Sockets.TryGetValue(key,out var res);
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
                try
                {

                    void CloseSocket()
                    {
                        ws.Close();
                    }

                    Console.Out.WriteLineAsync($"{DateTime.Now} incoming websocket {ws.ConnectionInfo.Path}").ConfigureAwait(false);

                    //ПРИХОДЯЩИЙ СТРИМ приходит сюда 
                    var WsParameters = GetParameters(ws);
                    var streamValue = WsParameters[_streamKEY];

                    //TODO:
                    // ------------ рефакторить этот кошмар------//

                    /*
                    if (!ws.ConnectionInfo.Headers.ContainsKey(_authname))
                    { 
                        CloseSocket();
                        return;
                    }*/


                    if(!WsParameters.ContainsKey("liteinvest"))
                    {
                        CloseSocket();
                        return;
                    }

                    //var authtoken = ws.ConnectionInfo.Headers[_authname];
                    var authtoken = WsParameters["liteinvest"];

                    if (authtoken == null || authtoken.Length == 0)
                    {
                        CloseSocket();
                        return;
                    }

                    var token = JwtProvider.ValidateToken(authtoken, jwtoptions);

                    if (token == null)
                    {
                        CloseSocket();
                        return;
                    }

                    var dt = token.ValidTo.ToLocalTime();
                   
                    if (DateTime.Now>dt)
                    {
                        CloseSocket();
                        return;
                    }

                    string user;

                    try
                    {

                        user = token.Claims.FirstOrDefault(x => x.Type == JwtHelper.LoginKey).Value;
                     
                        if(!UsersContext.ContainsKey(user))
                        {
                            CloseSocket() ; return; 
                        }    

                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Problems with validating user");
                    }
                    //----------------------------------------------------//

                    if (Streams.ContainsKey(streamValue))
                    {
                        var stream = Streams[streamValue];

                        //проверяем что есть все поля, которые нам нужны. 

                        /*
                        if (!stream.CheckAllParameters(WsParameters))
                        {
                            CloseSocket();
                            return;
                        }*/
                        

                        var paramName = stream.ParameterKeys.FirstOrDefault(p => p.Type == ParameterTypes.Key);

                       

                        //если ключ юзер, то берем его из токена
                        if (paramName.Key == WebSocketKeys.user.ToString())
                           ws.Key = user;
                        else
                           ws.Key = WsParameters[paramName.Key];

                        string KEY = ws.Key;

                        if (!stream.Sockets.ContainsKey(KEY))
                            stream.Sockets.TryAdd(KEY, new());

                        var hash = ws.GetHashCode();

                        stream.Sockets[KEY][hash] = ws;

                        //TODO: Дублирующий код 
                        if (stream.NeedDataRegistration)
                        {
                            stream.Register_Unregister_MarketData?.Invoke(KEY, true);
                            Console.WriteLine($"Registering marker data! {stream.Name}  = {KEY} hash = {hash}");
                        }

                        Console.WriteLine($"WebSocket for {stream.Name}  Opened hash = {hash}");

                        ws.OnClose += OnClosingWebSocket;
                    }
                    else
                    {
                        CloseSocket();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.Out.WriteLineAsync($" {ex.Message}").ConfigureAwait(false);

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
                var KEY = ws.Key;

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
