using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using wos.collections;
using System.Net.Sockets;
using System.Threading;

namespace ime.mail.Net
{
	class RequestData
	{
		public HttpWebRequest WebRequest;
		public HttpListenerContext Context;

		public RequestData(HttpWebRequest request, HttpListenerContext context)
		{
			WebRequest = request;
			Context = context;
		}
	}
	class HttpListenerProxy
	{
		public delegate void StartedHandler();
		public event StartedHandler Started;

		public string ServerAddress;
		public Map<string, string> AddedFiles = new Map<string, string>();
		public Map<string, string> AddedInnerFiles = new Map<string, string>();
		public int port = 8080;

		private HttpListener listener;
		private bool running;
		private Thread thread;

		public void Start()
		{
			running = true;
			thread = new Thread(Run);
			thread.Start();
		}
		private void Run()
		{
			listener = new HttpListener();
			listener.Prefixes.Add("http://*:" + port + "/");

			listener.Start();
			if (Started != null)
				Started();
			try
			{
				while (true)
				{
					try
					{
						// Note: The GetContext method blocks while waiting for a request.
						HttpListenerContext context = listener.GetContext();
						string requestString = context.Request.RawUrl;

						if (AddedFiles.ContainsKey(requestString))
						{
							FileStream fs = new FileStream(AddedFiles[requestString], FileMode.Open);
							CopyStream(fs, context.Response.OutputStream);
							context.Response.OutputStream.Close();
							fs.Close();
							fs.Dispose();
						}
						else if (AddedInnerFiles.ContainsKey(requestString))
						{
							string content = AddedInnerFiles[requestString];
							byte[] bytes = Encoding.UTF8.GetBytes(content);
							context.Response.OutputStream.Write(bytes, 0, bytes.Length);
							context.Response.OutputStream.Close();
						}
						else
						{
							HttpWebRequest request = (HttpWebRequest)WebRequest.Create(ServerAddress + requestString);
							request.KeepAlive = true;
							request.UserAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)";
							request.Timeout = 200000;

							RequestData requestData = new RequestData(request, context);
							IAsyncResult result = (IAsyncResult)request.BeginGetResponse(new AsyncCallback(RespCallback), requestData);
						}
					}
					catch (Exception e)
					{
						if (!running)
							break;
					}
				}
			}
			catch (Exception)
			{
			}

			listener.Stop();
		}
		public void Stop()
		{
			try
			{
				running = false;
				if (listener != null)
					listener.Stop();
				thread.Interrupt();
			}
			catch (Exception)
			{
			}
		}
		
		private void RespCallback(IAsyncResult asynchronousResult)
		{
			try
			{
				// State of request is asynchronous.
				RequestData requestData = (RequestData)asynchronousResult.AsyncState;

				using (HttpWebResponse response = (HttpWebResponse)requestData.WebRequest.EndGetResponse(asynchronousResult))
				using (Stream receiveStream = response.GetResponseStream())
				{
					HttpListenerResponse responseOut = requestData.Context.Response;

					if( response.ContentLength > 0 )
						responseOut.ContentLength64 = response.ContentLength;
					int bytesCopied = CopyStream(receiveStream, responseOut.OutputStream);
					responseOut.OutputStream.Close();
				}
			}
			catch (Exception e)
			{
				System.Diagnostics.Debug.WriteLine(e.Message);
			}

		}

		private int CopyStream(Stream input, Stream output)
		{
			byte[] buffer = new byte[32768];
			int bytesWritten = 0;
			while (true)
			{
				int read = input.Read(buffer, 0, buffer.Length);
				if (read <= 0)
					break;
				output.Write(buffer, 0, read);
				bytesWritten += read;
			}
			return bytesWritten;
		}
	}
}
