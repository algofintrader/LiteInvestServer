﻿using Microsoft.JSInterop;

namespace BlazorRenderAuto.Client.Services
{
	public class JsInteropService : IDisposable
	{
		public DotNetObjectReference<JsInteropService> ServiceObjectDotNetReference { get; set; } = null;
		public event Action<string, bool, bool> OnKeyUp;
		public event Action<string, string, bool, bool> OnKeyDown;
		public event Action<string, string, string> OnScroll;
		public event Action<string> OnWheel;
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
		[JSInvokable("OnScroll")]
		public void OnScrollJs(string windowId, string firstVisibleRowPrice, string lastVisibleRowPrice)
		{
			OnScroll?.Invoke(windowId, firstVisibleRowPrice, lastVisibleRowPrice);
		}
		[JSInvokable("OnWheel")]
		public void OnWheelJs(string elementId)
		{
			OnWheel?.Invoke(elementId);
		}
		public void Dispose()
		{
			ServiceObjectDotNetReference?.Dispose();
		}
	}
}