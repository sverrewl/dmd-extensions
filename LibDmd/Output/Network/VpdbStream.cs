﻿using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Windows.Controls;
using System.Windows.Media;
using LibDmd.Common;
using Newtonsoft.Json.Linq;
using NLog;
using Quobject.SocketIoClientDotNet.Client;

namespace LibDmd.Output.Network
{
	public class VpdbStream : IGray2Destination, IGray4Destination, IColoredGray2Destination, IColoredGray4Destination, IResizableDestination
	{
		public string Name { get; } = "VPDB Stream";
		public bool IsAvailable { get; } = true;

		public string ApiKey { get; set; }
		//public string EndPoint { get; set; } = "https://api-test.vpdb.io/";
		public string EndPoint { get; set; } = "http://127.0.0.1:3000/";
		public string AuthUser { get; set; }
		public string AuthPass { get; set; }

		private readonly Socket _socket;
		private bool _connected;
		private int _width;
		private int _height;
		private Color _color = RenderGraph.DefaultColor;
		private Color[] _palette;
		private readonly long _startedAt = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
		private JObject Welcome => new JObject {
			{ "width", _width },
			{ "height", _height },
			{ "color", ColorUtil.ToInt(_color) },
			{ "palette", new JArray(ColorUtil.ToIntArray(_palette)) }
		};

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public VpdbStream()
		{
			Logger.Info("Connecting to VPDB...");
			_socket = IO.Socket(EndPoint);
			_socket.On(Socket.EVENT_CONNECT, () => {
				_connected = true;
				Logger.Info("Connected to VPDB.");
				_socket.Emit("produce", Welcome);
			});
			_socket.On(Socket.EVENT_RECONNECT, () => {
				_connected = true;
				Logger.Info("Reconnected to VPDB.");
				_socket.Emit("produce", Welcome);
			});
			_socket.On(Socket.EVENT_DISCONNECT, () => {
				_connected = false;
				Logger.Info("Disconnected from VPDB.");
			});
		}

		public void Init()
		{
		}

		public void SetDimensions(int width, int height)
		{
			_width = width;
			_height = height;
			EmitObject("dimensions", new JObject { { "width", width }, { "height", height } });
		}

		/// <summary>
		/// Adds a timestamp to a byte array and sends it to the socket.
		/// </summary>
		/// <param name="eventName">Name of the event</param>
		/// <param name="dataLength">Length of the payload to send (without time stamp)</param>
		/// <param name="copy">Function that copies data to the provided array. Input: array with 8 bytes of timestamp and dataLength bytes to write</param>
		private void EmitTimestampedData(string eventName, int dataLength, Action<byte[], int> copy)
		{
			if (!_connected) {
				return;
			}
			try {
				var timestamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
				var data = new byte[dataLength + 8];
				Buffer.BlockCopy(BitConverter.GetBytes(timestamp - _startedAt), 0, data, 0, 8);
				copy(data, 8);
				_socket.Emit(eventName, data);

			} catch (Exception e) {
				Logger.Error(e, "Error sending " + eventName + " to socket.");
				_connected = false;
			}
		}

		public void RenderGray2(byte[] frame)
		{
			EmitTimestampedData("gray2planes", frame.Length / 4, (data, offset) => FrameUtil.Copy(FrameUtil.Split(_width, _height, 2, frame), data, offset));
		}

		public void RenderGray4(byte[] frame)
		{
			EmitTimestampedData("gray4planes", frame.Length / 2, (data, offset) => FrameUtil.Copy(FrameUtil.Split(_width, _height, 4, frame), data, offset));
		}

		public void RenderColoredGray2(byte[][] planes, Color[] palette)
		{
			if (planes.Length == 0) {
				return;
			}
			const int numColors = 4;
			const int bytesPerColor = 3;
			var dataLength = bytesPerColor * numColors + planes[0].Length * planes.Length;
			EmitTimestampedData("coloredgray2", dataLength, (data, offset) => {
				Buffer.BlockCopy(ColorUtil.ToByteArray(palette), 0, data, offset, bytesPerColor * numColors);
				FrameUtil.Copy(planes, data, offset + bytesPerColor * numColors);
			});
		}

		public void RenderColoredGray4(byte[][] planes, Color[] palette)
		{
			if (planes.Length == 0) {
				return;
			}
			const int numColors = 16;
			const int bytesPerColor = 3;
			var dataLength = bytesPerColor * numColors + planes[0].Length * planes.Length;
			EmitTimestampedData("coloredgray4", dataLength, (data, offset) => {
				Buffer.BlockCopy(ColorUtil.ToByteArray(palette), 0, data, offset, bytesPerColor * numColors);
				FrameUtil.Copy(planes, data, offset + bytesPerColor * numColors);
			});
		}

		public void RenderRgb24(byte[] frame)
		{
			EmitTimestampedData("rgb24frame", frame.Length, (data, offset) => Buffer.BlockCopy(frame, 0, data, offset, frame.Length));
		}

		public void SetColor(Color color)
		{
			_color = color;
			EmitObject("color", new JObject { { "color", ColorUtil.ToInt(color) } });
		}

		public void SetPalette(Color[] colors)
		{
			_palette = colors;
			EmitObject("palette", new JObject { { "palette", new JArray(ColorUtil.ToIntArray(colors)) } });
		}

		public void ClearPalette()
		{
			EmitData("clearPalette");
		}

		public void ClearColor()
		{
			EmitData("clearColor");
		}

		public void Dispose()
		{
			_socket?.Emit("stop");
			_socket?.Close();
		}

		private void EmitObject(string eventName, JObject data)
		{
			if (!_connected) {
				return;
			}
			try {
				_socket.Emit(eventName, data);
			} catch (Exception e) {
				Logger.Error(e, "Error sending " + eventName + " to socket.");
				_connected = false;
			}
		}

		private void EmitData(string eventName, IEnumerable data = null)
		{
			if (!_connected) {
				return;
			}
			try {
				if (data == null) {
					_socket.Emit(eventName);
				} else {
					_socket.Emit(eventName, data);
				}
			} catch (Exception e) {
				Logger.Error(e, "Error sending " + data + " to socket.");
				_connected = false;
			}
		}

		public static byte[] Compress(byte[] raw)
		{
			using (var memory = new MemoryStream()) {
				using (var gzip = new GZipStream(memory, CompressionMode.Compress, true)) { gzip.Write(raw, 0, raw.Length);
				}
				return memory.ToArray();
			}
		}
	}
}
