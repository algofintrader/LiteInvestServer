﻿using Microsoft.JSInterop;
namespace LiteInvestMainFront.Services
{
	public class JsInteropService : IDisposable
	{
		public DotNetObjectReference<JsInteropService> ServiceObjectDotNetReference { get; set; } = null;
		public event Action<string, bool, bool> OnKeyUp;
		public event Action<string, string, bool, bool> OnKeyDown;
		public JsInteropService()
		{
			ServiceObjectDotNetReference = DotNetObjectReference.Create(this);
		}
		[JSInvokable("KeyDown")]
		public void OnKeyDownJS(string keyCode, string windowid, bool isCtrl, bool isShift)
		{
			OnKeyDown?.Invoke(keyCode, windowid, isCtrl, isShift);
		}
		[JSInvokable("KeyUp")]
		public void OnKeyUpJS(string keyCode, bool isCtrl, bool isShift)
		{
			OnKeyUp?.Invoke(keyCode, isCtrl, isShift);
		}
		public void Dispose()
		{
			ServiceObjectDotNetReference?.Dispose();
		}
	}
}