using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SPBackend
{
	static class HTTPServerThread
	{
		static object mutex;
		static Queue<string> atomic_queue;

		public static void Start(int HTTP_PORT, Queue<string> q, object m)
		{
			mutex = m;
			atomic_queue = q;

			HttpListener server = new HttpListener();
			server.Prefixes.Add($"http://*:{HTTP_PORT}/receive/");
			server.Start();

			while (true)
				server.BeginGetContext(new AsyncCallback(AsyncCallback), server).AsyncWaitHandle.WaitOne();
		}

		static void AsyncCallback(IAsyncResult _server)
		{
			HttpListener server = (HttpListener)_server.AsyncState;

			HttpListenerContext context = server.EndGetContext(_server);
			HttpListenerRequest request = context.Request;

			if (request.HasEntityBody)
			{
				string data;
				using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
				{
					data = reader.ReadToEnd();
				}

				lock (mutex)
					atomic_queue.Enqueue(data);
			}

			HttpListenerResponse response = context.Response;
			response.StatusCode = 200;
			response.Close();
		}
	}
}
