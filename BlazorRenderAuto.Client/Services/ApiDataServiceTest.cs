using System.Collections.ObjectModel;
using BlazorRenderAuto.Client.Entity;
using Microsoft.AspNetCore.Components;
using Timer = System.Timers.Timer;

namespace BlazorRenderAuto.Client.Services
{

	public class QuoteChange
	{
		public int section { get; set; }
		public Quote Quote { get; set; }
	}
	public class ApiDataServiceTest:IDisposable
	{
		public Action <QuoteChange> NewQuoteChange { get; set; }

		private System.Timers.Timer timer;

		public Task SubscribeForQuotes(string secid)
		{
			timer = new Timer(200);
			timer.Elapsed += Timer_Elapsed;
			timer.Start();

			return Task.CompletedTask;
		}

		private void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
		{
			var random = new Random().Next(1, 60);
			NewQuoteChange?.Invoke(new QuoteChange(){section = random,Quote = new Quote(){Price = (decimal)random,Volume = new Random().Next(0, 30) } });
		}

		public ObservableCollection<Quote> GetQuotes()
		{
			var collection = new ObservableCollection<Quote>();
			for(int i=20;i<80;i++)
			{
				collection.Add(new Quote(){Price = i,Volume = new Random().Next(0,30)});
			}
			return collection;
		}

		public void Dispose()
		{
			if(timer!=null)
				timer.Dispose();
		}
	}
}
