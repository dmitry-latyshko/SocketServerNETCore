using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SocketServerNETCore
{
	internal class Program
	{
		private static List<HandlerConnection> Connections = new List<HandlerConnection>();

		private static void Main(string[] args)
		{
			Console.Title = "Socker Server";

			var port = 8002;

			if (args.Length != 1) {
				Console.WriteLine("Incorrect number of arguments passed.");

				Environment.ExitCode = 500;

				return;
			}

			if (int.TryParse(args[0], out port) == false) {
				Console.WriteLine($"Incorrect port number entered, {args[0]}.");

				Environment.ExitCode = 500;

				return;
			}

			var ipEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), port);

			try {
				using (var sListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)) {
					sListener.Bind(ipEndPoint);
					sListener.Listen(10);

					Console.WriteLine($"Server is up. Waiting for connections on {ipEndPoint}...");

					Task.Run(() => AcceptWorker(sListener));

					Console.WriteLine("Enter command 'list' to show all connections.");
					Console.WriteLine("Enter command 'exit' to shutdown server.");

					while (true) {
						var command = Console.ReadLine();

						if (command == "list") {
							if (Connections.Count == 0) {
								Console.WriteLine("There are no connections yet.");

								continue;
							}

							Console.WriteLine("Current connections:");

							foreach (var conn in Connections) {
								Console.WriteLine($"{conn.RemoteEndPoint} | {conn.Count}");
							}
						} else if (command == "exit") {
							break;
						}
					}

					var msg = Encoding.UTF8.GetBytes("Thank you for your connection. Server disconnected.");

					foreach (var h in Connections) {
						h.Handler.Send(msg);

						h.Handler.Shutdown(SocketShutdown.Both);
						h.Handler.Close();
						h.Handler.Dispose();

						Console.WriteLine($"Successful close connection for user with remote endpoint: {h.RemoteEndPoint}.");
					}

					Console.WriteLine("Server disconnected.");
				}
			} catch (Exception ex) {
				Console.WriteLine($"Program error: {ex.Message}");

				Environment.ExitCode = 500;
			} finally {
				foreach (var h in Connections) {
					h.Handler.Close();
					h.Handler.Dispose();
				}
			}

			Console.Write("Press any key to exit...");
			Console.ReadKey();
		}

		private static void AcceptWorker(Socket socket)
		{
			using (var listener = socket) {
				try {
					while (true) {
						var handler = socket.Accept();

						var newUser = new HandlerConnection(handler);

						Connections.Add(newUser);

						Console.WriteLine($"User connected, remote endpoint: {newUser.RemoteEndPoint}.");

						handler.Send(Encoding.UTF8.GetBytes("Thank you for your connection.\r\n"));
						handler.Send(Encoding.UTF8.GetBytes("Press 'ESC' for exit.\r\n"));

						Task.Run(() => ReceiveWorker(newUser));
					}
				} catch (Exception ex) {
					Console.WriteLine($"AcceptWorker error: {ex.Message}");
				}
			}
		}

		private static void ReceiveWorker(HandlerConnection connection)
		{
			using (var socket = connection.Handler) {
				try {
					var bytes = new byte[1024];

					var receiveValue = string.Empty;

					while (true) {
						socket.Send(Encoding.UTF8.GetBytes($"\r\nEnter your value:\r\n"));

						receiveValue = string.Empty;

						while (true) {
							var bytesRec = socket.Receive(bytes);

							var data = Encoding.UTF8.GetString(bytes, 0, bytesRec);

							if (data == "\u001b") {
								throw new SocketException((int)SocketError.Disconnecting);
							} else if (data == "\r\n") {
								break;
							} else {
								receiveValue += data.Trim();
							}
						}

						receiveValue.Trim();

						if (int.TryParse(receiveValue, out var number) == true) {
							connection.Count += number;

							socket.Send(Encoding.UTF8.GetBytes($"Your progress are: {connection.Count}."));
						} else if (receiveValue == "list") {
							if (Connections.Count == 0) {
								socket.Send(Encoding.UTF8.GetBytes("There are no connections yet."));
								break;
							}

							socket.Send(Encoding.UTF8.GetBytes("Current connections:"));

							foreach (var conn in Connections) {
								socket.Send(Encoding.UTF8.GetBytes($"\r\n{conn.RemoteEndPoint} | {conn.Count}"));
							}
						} else {
							socket.Send(Encoding.UTF8.GetBytes($"\r\nYou enter wrong value. Please enter number value.\r\n"));
						}
					}
				} catch (SocketException) {
					socket.Shutdown(SocketShutdown.Both);
					socket.Close();
					socket.Dispose();

					if (Connections.Contains(connection)) {
						Console.WriteLine($"User disconnected, remote endpoint: {connection.RemoteEndPoint}.");

						Connections.Remove(connection);
					}
				} catch (Exception ex) {
					Console.WriteLine($"ReceiveWorker error: {ex.Message}");
				}
			}
		}

		private class HandlerConnection
		{
			public HandlerConnection(Socket handler)
			{
				RemoteEndPoint = handler.RemoteEndPoint.ToString();
				Handler = handler;
				Count = 0;
			}

			public string RemoteEndPoint { get; set; }

			public Socket Handler { get; set; }

			public int Count { get; set; }
		}
	}
}