using AvpPlazaExample;

using PlazaEngine.Engine;
using PlazaEngine.Entity;

using RouterLoggerSpace;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AvpPlazaTester
{
    /// <summary>
    /// Тестирование должно описано автоматическим.
    /// Обнулить все позиции до старта(по портфелю)
    /// Сценарий 1.
    /// Выставить 200 заявок по 20 (в течении 1 сек. ) инструментам при условии, что у нас 30 транзакций в секунду.Выставить по ценам на грани исполнения.
    /// Роутер не должен упасть, часть заявок должна перенестись на исполнение в другой промежуток.
    /// Проверить, что все заявки будут обслужены и отправлены.
    /// Сопоставить результаты - вывести их
    /// Автоматом закрыть позы.
    /// </summary>
    internal class Test1
    {
        MainWindow mainWindow;
        public Test1(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
        }

        public bool StartTest(PlazaConnector plaza, List<Security> selectedSecurity, int orderCount, int priceDistance, Side side)
        {

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

            if (result && !ClosePosition(plaza, 1))
            {
                result = false;
            }
            Thread.Sleep(1000);
            if (result && !SendOrders(plaza, selectedSecurity, orderCount, priceDistance, side))
            {
                result = false;
            }
            Thread.Sleep(1000);
            if (result && !CancelOrder(plaza))
            {
                result = false;
            }
            Thread.Sleep(1000);
            if (result && !ClosePosition(plaza, 4))
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

        private bool CancelOrder(PlazaConnector plaza)
        {
            orderCancel = 0;
            SendMessageLog($"Шаг 3. Снимаем Активные заявки.");
            var orders = plaza.Orders.Values.ToList();
            int orderNeedCanceled = 0;
            foreach (var o in orders)
            {
                if (o.State == Order.OrderStateType.Activ)
                {
                    orderNeedCanceled++;
                    plaza.CancelOrder(o.NumberUser);
                    RouterLogger.Log(o.ToString(), "Test_Scenario");
                }
            }
            Thread.Sleep(1000);
            int trySecond = 0;
            while (orderNeedCanceled != orderCancel && trySecond++ < orderNeedCanceled / 30 + 1)
            {
                Thread.Sleep(2000);
            }
            var orders2 = plaza.Orders.Values.ToList();
            int orders2ActiveCount = orders2.Count((o) => o.State == Order.OrderStateType.Activ);
            trySecond = 0;
            while (orders2ActiveCount  > 0 && trySecond < 10)
            {
                Thread.Sleep(1000);
                orders2 = plaza.Orders.Values.ToList();
                orders2ActiveCount = orders2.Count((o) => o.State == Order.OrderStateType.Activ);
            }

            if (orderNeedCanceled == orderCancel && orders2ActiveCount == 0)
            {
                SendMessageLog($"Шаг 3. Все активные заявки сняты {orderNeedCanceled}.");
                return true;
            }
            else
            {
                SendMessageLog($"Шаг 3. FAIL. Ошибка снятия заявок. {orderNeedCanceled}, {orderCancel}, {orders2ActiveCount}.");
                return false;
            }

        }

        private bool SendOrders(PlazaConnector plaza, List<Security> selectedSecurity, int orderCount, int priceDistance, Side side)
        {
            orderPending = 0;
            orderActiv = 0;
            orderDone = 0;
            orderCancel = 0;
            orderFail = 0;

            SendMessageLog($"Шаг 2. Выставляем заявки. {orderCount * selectedSecurity.Count}");
            string portfolio = plaza.Portfolios.First().Value.Number;
            if (selectedSecurity.Count == 0)
            {
                SendMessageLog($"Шаг 2. FAIL. Не выбраны инструменты для тестирования.");
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
            mainWindow.Dispatcher.Invoke(() => mainWindow.ListBoxLogTest1.Items.Insert(0, $"{DateTime.Now.ToLongTimeString()}; {message}"));
        }

        private bool ClosePosition(PlazaConnector plaza, int step)
        {
            int countOpenPosition = plaza.Positions.Count((p) => p.Value.ValueCurrent != 0);
            SendMessageLog($"Шаг {step}. Нашли открытых позиций {countOpenPosition}.  Обнуляем открытые позиции по портфелю...");
            mainWindow.CloseAllPosition();
            
            // проверяем
            Thread.Sleep(1000);
            countOpenPosition = plaza.Positions.Count((p) => p.Value.ValueCurrent != 0);
            int trySecond = 0;
            while (countOpenPosition != 0 && trySecond++<10)
            {
                countOpenPosition = plaza.Positions.Count((p) => p.Value.ValueCurrent != 0);
                Thread.Sleep(1000);
            }
            if (countOpenPosition == 0)
            {
                SendMessageLog($"Шаг {step}. Открытых позиций больше нет.");
                return true;
            }
            else
            {
                SendMessageLog($"Шаг {step}. FAIL. Не смогли закрыть позиции: {countOpenPosition}");
                return false;
            }
        }


    }
}
