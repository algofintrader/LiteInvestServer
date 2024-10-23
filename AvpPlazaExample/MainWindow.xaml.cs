using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection.Metadata;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;
using System.Xml.Serialization;

using AvpPlazaTester;

using PlazaEngine;
using PlazaEngine.Engine;
using PlazaEngine.Entity;
using RouterLoggerSpace;

namespace AvpPlazaExample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            logMessage = new LogMessage();
            logMessage.Show();


            Test1 = new(this);
            Test2 = new(this);
            LogMessage("Тестер запущен, подключение к PLaza не выполнено");
        }
        Test1 Test1;
        Test2 Test2;
        LogMessage logMessage;
        Ticks ticksWindow;
        public BindingList<Level> Levels = new();
        public BindingList<Security> Securitys = new();
        public BindingList<Security> SelectedSecuritys = new();
        public BindingList<Order> SendedOrders2 = new();
        public BindingList<PositionOnBoard> PositionsOnBoard = new();

        PlazaConnector plaza;

        public bool Online
        {
            get
            {
                return plaza.Status == ServerConnectStatus.Connect && depthLoaded && securityLoaded && orderLoaded;
            }
        }

        private void PlazaConnect()
        {
            LogMessage("Выполняем подключение к PLaza");

            if (RadioTest.IsChecked ?? true)
            {
                /*Test Connection*/
                plaza = new PlazaConnector("11111111", test: true)
                {
                    Limit = 30,
                    LoadTicksFromStart = false,
                };
            }
            else if (RadioReal.IsChecked ?? false)
            {
                /* Real Trade Connection*/
                plaza = new PlazaConnector("02mMLX144T2yxnfzEUrCjUKzXKciQKJ", test: false, appname: "osaApplication",2)
                {
                    Limit = 30,
                    LoadTicksFromStart = false,
                };
            }
            else
            {
                MessageBox.Show("Выберите один из режимов, Real или Test ");
            }
            try
            {
                plaza.Connect();
                LogMessage("Ожидаем выход всех потоков Plaza в онлайн: инструменты, стаканы, ордера...");

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            plaza.MarketDepthChangeEvent += Plaza_MarketDepthChangeEvent;
            plaza.MarketDepthLoadedEvent += Plaza_MarketDepthLoadedEvent;
            plaza.SecuritiesLoadedEvent += Plaza_UpdateSecurity;
            plaza.OrderChangedEvent += Plaza_OrderChangedEvent;
            plaza.OrderChangedEvent += Plaza_OrderCancel;
            plaza.OrderLoadedEvent += Plaza_OldOrderLoadedEvent;
            plaza.UpdatePosition += Plaza_UpdatePosition;
            plaza.NewTickCollectionEvent += Plaza_NewTickCollectionEvent;
            plaza.TicksLoadedEvent += Plaza_TicksLoadedEvent;

            timerListViewOrderUpdate = new System.Timers.Timer();
            timerListViewOrderUpdate.Elapsed += TimerListViewOrderUpdate_Elapsed;
            timerListViewOrderUpdate.Interval = 2000;

            Thread threadDepthUpdate = new Thread(ThreadDepthUpdate);
            threadDepthUpdate.IsBackground = true;
            threadDepthUpdate.Start();

            RadioReal.IsEnabled = false;
            RadioTest.IsEnabled = false;
        }

        private void Plaza_TicksLoadedEvent()
        {
            LogMessage("Тики загружены и вышли в режим Оналйн");
        }

        private void Plaza_NewTickCollectionEvent(Dictionary<string, List<Trade>> ticks)
        {
            if (ticksWindow != null)
            {
                
                foreach (var item in ticks)
                {
                    
                    foreach (var item1 in item.Value)
                    {
                        ticksWindow.Dispatcher.Invoke(() => ticksWindow.ListBoxTicks.Items.Insert(0, $"{item1}"));
                    }
                }
            }
        }

        bool depthLoaded = false;
        private void Plaza_MarketDepthLoadedEvent()
        {
            depthLoaded = true;
            LogMessage("Стаканы загружены и вышли в режим Онлайн");
        }

        public void SaveParametrs()
        {
            using (StreamWriter writer = new StreamWriter(@"SelectedSecuritys.txt", false))
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
            if (File.Exists(@"SelectedSecuritys.txt") && ListSelectedSec2.Items.Count == 0)
            {
                using (StreamReader reader = new StreamReader(@"SelectedSecuritys.txt"))
                {
                    while (!reader.EndOfStream)
                    {
                        var l = reader.ReadLine();
                        if (plaza.Securities.ContainsKey(l))
                        {
                            SelectedSecuritys.Add(plaza.Securities[l]);
                            plaza.RegisterMarketDepth(plaza.Securities[l], false);
                        }
                    }
                }
                ListSelectedSec2.Dispatcher.Invoke(() => ListSelectedSec2.ItemsSource = SelectedSecuritys);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            SaveParametrs();
            logMessage?.Close();
            ticksWindow?.Close();

        }

        private void Plaza_UpdatePosition(PositionOnBoard position)
        {
            var p = plaza.Positions.Values.ToList();
            Dispatcher.Invoke(() => 
            {
                PositionsOnBoard.Clear();
                foreach (var item in p)
                {
                    PositionsOnBoard.Add(item);
                }
                ListViewPosition.ItemsSource = PositionsOnBoard;
            });

        }

        private void ThreadDepthUpdate()
        {
            bool oldOnline = false;
            while (true)
            {
                try
                {
                    if (oldOnline != Online)
                    {
                        oldOnline = Online;
                        if (Online)
                        {
                            LogMessage("Все потоки PLAZA вышли в режим Онлайн. Можно работать.");
                        }
                    }
                    Thread.Sleep(100);
                    while (!depthQueue.IsEmpty)
                    {
                        if (depthQueue.TryDequeue(out var depth))
                        {
                            if (selectedSec.Id == depth.SecurityId)
                            {
                                Dispatcher.Invoke(() => UpdateGlass(depth));
                            }
                            else
                            {
                                Dispatcher.Invoke(() => Levels.Clear());
                            }
                        }
                    }
                }
                catch (Exception ex)
                {

                }
            }

        }

        

        private void UpdateGlass(MarketDepth depth)
        {
            try
            {
                Levels = new BindingList<Level>();
                for (int i = 0; i < depth.Asks.Count; i++)
                {
                    MarketDepthLevel dep = depth.Asks[i];
                    if (dep != null)
                    {
                        Level l = new Level(dep.Price, dep.Ask, 1);
                        Levels.Add(l);
                    }
                }
                for (int i = 0; i < depth.Bids.Count; i++)
                {
                    MarketDepthLevel dep = depth.Bids[i];
                    if (dep != null)
                    {
                        Level l = new Level(dep.Price, dep.Bid, 2);
                        Levels.Add(l);
                    }
                }
                GlassView.ItemsSource = Levels;
                LabelTime.Dispatcher.Invoke(() => LabelTime.Content = depth.Time);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        bool orderLoaded = false;
        private void Plaza_OldOrderLoadedEvent()
        {
            orderLoaded = true;
            LogMessage("Ордера загружены и вышли в режим Оналйн");
            timerListViewOrderUpdate.Stop();
            timerListViewOrderUpdate.Start();
        }

        private void TimerListViewOrderUpdate_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            timerListViewOrderUpdate.Stop();
            Dispatcher.Invoke(() =>
            {
                ListViewOrder.Items.Clear();
                ListViewOrders2.Items.Clear();
                plaza.Orders.All(o => 
                {
                    ListViewOrder.Items.Add(o.Value); 
                    if (o.Value.TimeCreate > TimeSendOrders)
                    {
                        ListViewOrders2.Items.Add(o.Value);
                    }
                    return true;
                });

                ListViewOrder.Items.SortDescriptions.Add(new System.ComponentModel.SortDescription("NumberUser", System.ComponentModel.ListSortDirection.Ascending));
                ListViewOrders2.Items.SortDescriptions.Add(new System.ComponentModel.SortDescription("NumberUser", System.ComponentModel.ListSortDirection.Ascending));
            });
            
        }

        System.Timers.Timer timerListViewOrderUpdate;

        private void LogMessage(string m)
        {
            logMessage.Dispatcher.Invoke(() => logMessage.ListBoxLog.Items.Insert(0,$"{DateTime.Now.ToLongTimeString()}; {m}"));
        }

        private void Plaza_OrderChangedEvent(Order order, string? message)
        {
            timerListViewOrderUpdate.Stop();
            timerListViewOrderUpdate.Start();

            LogMessage($"{order.NumberUserOrderId}; {order.State}; {message}");
            
        }

        List<Security> securitis = new List<Security>();
        bool securityLoaded = false;
        private void Plaza_UpdateSecurity()
        {
            securityLoaded = true;
            LogMessage("Инструменты загружены");
            securitis = plaza.Securities.Values.ToList();
            securitis.Sort((x, y) => x.Name.CompareTo(y.Name));

            Dispatcher.Invoke(() =>
            {
                SecuritiBox.Items.Clear();
                foreach (var sec in securitis)
                {
                    SecuritiBox.Items.Add(sec.Name);
                    Securitys.Add(sec);
                }
                ListSec2.ItemsSource = Securitys;
                ListSec3.ItemsSource = Securitys;
            }
            );
            LoadParametrs();
        }

        ConcurrentQueue<MarketDepth> depthQueue = new ConcurrentQueue<MarketDepth>();

        private void Plaza_MarketDepthChangeEvent(PlazaEngine.Entity.MarketDepth depth)
        {
            if (selectedSec is null) return;



            if (depth.SecurityId == selectedSec.Id || depth.SecurityId == selectedSec.Name)
            {
                depthQueue.Enqueue(depth);
            }
        }



        private void Button_Click(object sender, RoutedEventArgs e)
        {
            PlazaConnect();
        }



        Security selectedSec = null;
        private void SecuritiBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selSec = (string)SecuritiBox.SelectedValue;
            selectedSec = securitis.Find(s => s.Name == selSec);
            Levels.Clear();
            if (selectedSec != null)
            {
                plaza.RegisterMarketDepth(selectedSec, EmulatorCheckBox.IsChecked ?? false);
            }
            //GlassView.Items.Clear();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (selectedSec != null)
            {
                plaza.RegisterMarketDepth(selectedSec, EmulatorCheckBox.IsChecked ?? false);
            }

        }

        private void Button_Close_Connection(object sender, RoutedEventArgs e)
        {
            plaza.Stop();
            //   plaza.Dispose();

        }

        private async void Button_SendMultiOrder(object sender, RoutedEventArgs e)
        {
            if (selectedSec is null) return;

            Side side = ComboBoxSide.SelectedIndex == 0 ? Side.Buy : Side.Sell;
            int c = int.Parse(TextBoxCount.Text);
            for (int i = 1; i <= c; i++)
            {
                decimal price = decimal.Parse(TextBoxPrice.Text.Replace(',', '.'), new CultureInfo("en-EN"))
                    + decimal.Parse(TextBoxShift.Text.Replace(",", "."), new CultureInfo("en-EN")) * i;
                Order o = new Order(selectedSec, side, decimal.Parse(TextBoxVolume.Text.Replace(',', '.'), new CultureInfo("en-EN"))
                    , price
                    , plaza.Portfolios.First().Value.Number
                    , $"Id{i}"
                    );
                o.Comment = TextBoxComment.Text;

                await plaza.ExecuteOrderAsync(o);
                RouterLogger.Log(o.ToString(), "Test_Scenario");
            }
        }

        private async void Button_CancelSelectedOrder(object sender, RoutedEventArgs e)
        {
            Order[] orders = new Order[ListViewOrder.SelectedItems.Count];
            ListViewOrder.SelectedItems.CopyTo(orders,0);
            foreach (var o in orders)
            {
                await plaza.CancelOrder(((Order)o).NumberUserOrderId);
                RouterLogger.Log(o.ToString(), "Test_Scenario");
            }

        }

        public class Level
        {
            public decimal Price { get; set; }
            public decimal Volume { get; set; }

            public int TypeLevel { get; set; }
            public Level(decimal price, decimal vol, int typeLevel)
            {
                Price = price;
                Volume = vol;
                TypeLevel = typeLevel;
            }
        }

        private void ListSec2_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Security s = ((FrameworkElement)e.OriginalSource).DataContext as Security;
            SelectedSecuritys.Add(s);
            ListSelectedSec2.ItemsSource = SelectedSecuritys;
            plaza.RegisterMarketDepth(s,false);
            
        }

        private async void ButtonCloseAllPosition_Click(object sender, RoutedEventArgs e)
        {
            test2Started = false;
            await CloseAllPosition();
        }

        public async Task CloseAllPosition()
        {
            var allPosition = PositionsOnBoard.ToList();
            int i = 0;
            foreach (var p in allPosition)
            {
                if (p.XPosValueCurrent != 0)
                {
                    Order o = new Order(p.SecurityId, p.XPosValueCurrent > 0 ? Side.Sell : Side.Buy, Math.Abs(p.XPosValueCurrent), p.PortfolioName, $"Close-Client{i++} ");
                    RouterLogger.Log(o.ToString(), "Test_Scenario");
                    await plaza.ExecuteOrderAsync(o);
                }
            }
        }

        DateTime TimeSendOrders = PLazaHelper.GetTimeMoscowNow();

        private async void ButtonMultiOrder2_Click(object sender, RoutedEventArgs e)
        {
            test2Started = false;
            //await SendMultiOrder();
            Side side = ComboBoxSide2.SelectedIndex == 0 ? Side.Buy : Side.Sell;
            var s = SelectedSecuritys.ToList();
            int c = int.Parse(TextOrderCount2.Text);
            int d = int.Parse(TextOrderDistance.Text);
            await Task.Factory.StartNew(() => Test1.StartTest(plaza,s , c, d, side));

        }

        

        private async void ButtonCancelOrder2_Click(object sender, RoutedEventArgs e)
        {
            await CancelAllOrders();

        }

        private async Task CancelAllOrders()
        {
            var orders = plaza.Orders.Values.ToList();
            foreach (var o in orders)
            {
                if (o.State == Order.OrderStateType.Activ)
                {
                    await plaza.CancelOrder(o.NumberUserOrderId);
                    RouterLogger.Log(o.ToString(), "Test_Scenario");
                }
            }
        }

      

        private void ListSelectedSec2_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Security s = ((FrameworkElement)e.OriginalSource).DataContext as Security;
            SelectedSecuritys.Remove(s);
            ListSelectedSec2.ItemsSource = SelectedSecuritys;
        }

        bool test2Started = false;
        private async void ButtonMultiOrder2Test2_Click(object sender, RoutedEventArgs e)
        {
            //test2Started = true;
            //await SendMultiOrder();
        }

        private async void Plaza_OrderCancel(Order o, string? arg2)
        {
            if (test2Started 
            && o.State == Order.OrderStateType.Activ)
            {
                await plaza.CancelOrder(o.NumberUserOrderId);
                RouterLogger.Log(o.ToString(), "Test_Scenario");
            }
        }

        private void ButtonMultiOrder3_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ListSec3_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Security s = ((FrameworkElement)e.OriginalSource).DataContext as Security;
            Test2.SelectedSecuritys.Add(s);
            ListSelectedSec3.ItemsSource = Test2.SelectedSecuritys;
            plaza.RegisterMarketDepth(s, false);
        }

        private void Button_Click_ShowTicks(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ticksWindow is null)
                {
                    ticksWindow = new Ticks();
                    ticksWindow.Show();
                }
                else if (ticksWindow.IsVisible == false)
                {
                    ticksWindow.Show();
                }
            }
            catch
            {
                ticksWindow = new Ticks();
                ticksWindow.Show();
            }



            if (selectedSec != null)
            {
                plaza.TryRegisterTicks(selectedSec);
            }
        }
    }
}