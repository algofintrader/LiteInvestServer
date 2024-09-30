using AvpPlazaExample;

using PlazaEngine.Engine;
using PlazaEngine.Entity;

using RouterLoggerSpace;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/*
 Сценарий 2.

Выставить 200 заявок по 20 (в течении 1 сек. ) инструментам при условии, что у нас 30 транзакций в секунду. Выставить по ценам на грани исполнения.
Сразу же попытаться отменить заявки прям в момент выставления. Пачку молниеносно при выставлении, вторую пачку скажем через 1 сек. Задача прям навредить системе.
Проверить, что все заявки будут обслужены, отправлены, отменены.
Роутер не упал, все ок.
Сопоставить результаты, вывести их
Автоматом закрыть позиции.
 */

namespace AvpPlazaTester
{
    internal class Test2
    {
        MainWindow mainWindow;
        public BindingList<Security> SelectedSecuritys = new();
        public Test2(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
            LoadParametrs();
            mainWindow.Closed += (x,y) => SaveParametrs();
        }

        PlazaConnector plaza;
        public bool StartTest(PlazaConnector plaza, List<Security> selectedSecurity, int orderCount, int priceDistance, Side side)
        {
            this.plaza = plaza;
            bool result = true;
            if (plaza == null || plaza.Status == ServerConnectStatus.Disconnect || !mainWindow.Online)
            {
                SendMessageLog("Нет соединения роутера с PLAZA или с биржей или не все потоки вышли в режим Онлайн");
                return false;
            }
            else
            {
                SendMessageLog("Старт тест");
            }
            plaza.OrderChangedEvent += Plaza_OrderChangedEvent;

            if (result && !SendAndCancelOrders(plaza, selectedSecurity, orderCount, priceDistance, side))
            {
                result = false;
            }



            plaza.OrderChangedEvent -= Plaza_OrderChangedEvent;
            if (result)
            {
                SendMessageLog($"Тест успешно завершен");
            }
            else
            {
                SendMessageLog($"FAIL.Тест завершен с ошибками");
            }
            return result;
        }

        private bool SendAndCancelOrders(PlazaConnector plaza, List<Security> selectedSecurity, int orderCount, int priceDistance, Side side)
        {
            orderPending = 0;
            orderActiv = 0;
            orderDone = 0;
            orderCancel = 0;
            orderFail = 0;

            SendMessageLog($"Шаг 1. Выставляем и сразу отменяем заявки. {orderCount * selectedSecurity.Count}");
            string portfolio = plaza.Portfolios.First().Value.Number;
            if (selectedSecurity.Count == 0)
            {
                SendMessageLog($"Шаг 1. FAIL. Не выбраны инструменты для тестирования.");
                return false;
            }
            foreach (var sec in selectedSecurity)
            {
                for (int i = 0; i < orderCount; i++)
                {
                    decimal price = side == Side.Buy ? sec.BestBid - sec.PriceStep * priceDistance : sec.BestAsk + sec.PriceStep * priceDistance;
                    Order o = new Order(sec.Id, side, 1, price, portfolio, $"{sec.ShortName}-Client{i}");
                    RouterLogger.Log(o.ToString(), "Test_Scenario");
                    plaza.ExecuteOrder(o);
                }
            }
            Thread.Sleep(1000);
            int trySecond = 0; 
            while (orderPending != orderCount * selectedSecurity.Count && trySecond++ < orderCount * selectedSecurity.Count / 30 + 1)
            {
                Thread.Sleep(2000);
            }
            Thread.Sleep(2000);
            DateTime t;
            lock (timeLastOrderChangedLocker)
            {
                t = timeLastOrderChanged;
            }

            while (DateTime.Now.Subtract(t) < TimeSpan.FromSeconds(2))
            {
                lock (timeLastOrderChangedLocker)
                {
                    t = timeLastOrderChanged;
                }
                Thread.Sleep(1000);
            }

            if (orderPending == orderCount * selectedSecurity.Count)
            {
                SendMessageLog($"Шаг 2. Все заявки выставлены {orderPending}");
                SendMessageLog($"Шаг 2. Заявки исполнены {orderDone}");
                SendMessageLog($"Шаг 2. Заявки отменены{orderCancel}");
                return true;
            }
            else
            {
                SendMessageLog($"Шаг 2. Не смогли выставить все заявки: {orderPending}");
                return false;
            }

        }



        int orderPending;
        int orderActiv;
        int orderDone;
        int orderCancel;
        int orderFail;
        DateTime timeLastOrderChanged = DateTime.MinValue;
        object timeLastOrderChangedLocker = new();

        private void Plaza_OrderChangedEvent(Order o, string? m)
        {
            if (plaza != null)
            {
                plaza.CancelOrder(o.NumberUser);
            }
            lock (timeLastOrderChangedLocker)
            {
                timeLastOrderChanged = DateTime.Now;
                switch (o.State)
                {
                    case Order.OrderStateType.None:
                        break;
                    case Order.OrderStateType.Activ:
                        orderActiv++;
                        break;
                    case Order.OrderStateType.Pending:
                        orderPending++;
                        break;
                    case Order.OrderStateType.Done:
                        orderDone++;
                        break;
                    case Order.OrderStateType.Partial:
                        break;
                    case Order.OrderStateType.Fail:
                        orderFail++;
                        break;
                    case Order.OrderStateType.Cancel:
                        orderCancel++;
                        break;
                    default:
                        break;
                }
            }
        }

        private void SendMessageLog(string message)
        {
            mainWindow.Dispatcher.Invoke(() => mainWindow.ListBoxLogTest3.Items.Insert(0, $"{DateTime.Now.ToLongTimeString()}; {message}"));
        }

        public void SaveParametrs()
        {
            using (StreamWriter writer = new StreamWriter(@"SelectedSecuritys2.txt", false))
            {
                for (int i = 0; i < SelectedSecuritys.Count; i++)
                {
                    writer.WriteLine(SelectedSecuritys[i].Id);
                }

                writer.Close();
            }
        }

        private void LoadParametrs()
        {
            if (File.Exists(@"SelectedSecuritys2.txt") && mainWindow.ListSelectedSec3.Items.Count == 0)
            {
                using (StreamReader reader = new StreamReader(@"SelectedSecuritys2.txt"))
                {
                    while (!reader.EndOfStream)
                    {
                        var l = reader.ReadLine();
                        SelectedSecuritys.Add(plaza.Securities[l]);
                        plaza.RegisterMarketDepth(plaza.Securities[l], false);
                    }
                }
                mainWindow.ListSelectedSec3.Dispatcher.Invoke(() => mainWindow.ListSelectedSec3.ItemsSource = SelectedSecuritys);
            }
        }

    }
}
