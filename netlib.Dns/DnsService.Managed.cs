using MailClient.Utils.Threading;
using netlib.Dns.Records;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace netlib.Dns.Managed
{
	internal partial class ManagedDnsService : IDnsService
	{
		private int nextId = 1;
		//private readonly Func<string> getDnsServer;
		private Socket? cachedSocket;
		private string? cachedSocketHost;
		private string? dnsPrimaryHost;
		private readonly AsyncLock _lock = new();

		public ManagedDnsService()
		{
			NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
		}

		private void NetworkChange_NetworkAddressChanged(object sender, EventArgs e)
		{
			// Reset the known DNS address
			dnsPrimaryHost = null;
		}

		private string GetDnsHost()
		{
			// Return the cached value
			if (dnsPrimaryHost is not null)
				return dnsPrimaryHost;

			return GetDnsServerAddresses().FirstOrDefault() ?? "1.1.1.1";
		}

		public IList<MXRecord> GetMxRecords(string domain)
		{
			TryGetRecords(domain, RecordType.MX, out var records, true);
			return records.OfType<MxRecord>().Where(mx => mx.Name.Equals(domain, StringComparison.OrdinalIgnoreCase)).Select(mx => new MXRecord { Exchange = mx.Exchange, Preference = mx.Preference }).ToList();
		}

		public bool TryGetMxRecords(string domain, [NotNullWhen(true)] out IList<MXRecord>? mxRecords)
		{
			if (TryGetRecords(domain, RecordType.MX, out var records, false))
			{
				mxRecords = records.OfType<MxRecord>().Where(mx => mx.Name.Equals(domain, StringComparison.OrdinalIgnoreCase)).Select(mx => new MXRecord { Exchange = mx.Exchange, Preference = mx.Preference }).ToList();
				return true;
			}
			else
			{
				mxRecords = null;
				return false;
			}
		}

		public async Task<IList<MXRecord>> GetMxRecordsAsync(string domain, CancellationToken cancellationToken)
		{
			var records = await TryGetRecordsAsync(domain, RecordType.MX, cancellationToken, true).ConfigureAwait(false);
			return records.OfType<MxRecord>().Where(mx => mx.Name.Equals(domain, StringComparison.OrdinalIgnoreCase)).Select(mx => new MXRecord { Exchange = mx.Exchange, Preference = mx.Preference }).ToList();
		}

		public async Task<IList<MXRecord>?> TryGetMxRecordsAsync(string domain, CancellationToken cancellationToken)
		{
			var records = await TryGetRecordsAsync(domain, RecordType.MX, cancellationToken, false).ConfigureAwait(false);
			return records?.OfType<MxRecord>().Where(mx => mx.Name.Equals(domain, StringComparison.OrdinalIgnoreCase)).Select(mx => new MXRecord { Exchange = mx.Exchange, Preference = mx.Preference }).ToList();
		}

		public IList<SRVRecord> GetSrvRecords(string domain)
		{
			TryGetRecords(domain, RecordType.SRV, out var records, true);
			return records.OfType<SrvRecord>().Where(srv => srv.Name.Equals(domain, StringComparison.OrdinalIgnoreCase)).Select(srv => new SRVRecord { NameNext = srv.Target, Priority = srv.Priority, Weight = srv.Weight, Port = srv.Port }).ToList();
		}

		public bool TryGetSrvRecords(string domain, [NotNullWhen(true)] out IList<SRVRecord>? srvRecords)
		{
			if (TryGetRecords(domain, RecordType.SRV, out var records, false))
			{
				srvRecords = records.OfType<SrvRecord>().Where(srv => srv.Name.Equals(domain, StringComparison.OrdinalIgnoreCase)).Select(srv => new SRVRecord { NameNext = srv.Target, Priority = srv.Priority, Weight = srv.Weight, Port = srv.Port }).ToList();
				return true;
			}
			else
			{
				srvRecords = null;
				return false;
			}
		}
		public async Task<IList<SRVRecord>> GetSrvRecordsAsync(string domain, CancellationToken cancellationToken)
		{
			var records = await TryGetRecordsAsync(domain, RecordType.SRV, cancellationToken, true).ConfigureAwait(false);
			return records.OfType<SrvRecord>().Where(srv => srv.Name.Equals(domain, StringComparison.OrdinalIgnoreCase)).Select(srv => new SRVRecord { NameNext = srv.Target, Priority = srv.Priority, Weight = srv.Weight, Port = srv.Port }).ToList();
		}

		public async Task<IList<SRVRecord>?> TryGetSrvRecordsAsync(string domain, CancellationToken cancellationToken)
		{
			var records = await TryGetRecordsAsync(domain, RecordType.SRV, cancellationToken, false).ConfigureAwait(false);
			return records?.OfType<SrvRecord>().Where(srv => srv.Name.Equals(domain, StringComparison.OrdinalIgnoreCase)).Select(srv => new SRVRecord { NameNext = srv.Target, Priority = srv.Priority, Weight = srv.Weight, Port = srv.Port }).ToList();
		}

		public IList<TXTRecord> GetTxtRecords(string domain)
		{
			TryGetRecords(domain, RecordType.TXT, out var records, true);
			return records.OfType<TxtRecord>().Where(txt => txt.Name.Equals(domain, StringComparison.OrdinalIgnoreCase)).Select(txt => new TXTRecord { StringCount = 1, StringArray = txt.Text }).ToList();
		}

		public bool TryGetTxtRecords(string domain, [NotNullWhen(true)] out IList<TXTRecord>? txtRecords)
		{
			if (TryGetRecords(domain, RecordType.TXT, out var records, false))
			{
				txtRecords = records.OfType<TxtRecord>().Where(txt => txt.Name.Equals(domain, StringComparison.OrdinalIgnoreCase)).Select(txt => new TXTRecord { StringCount = 1, StringArray = txt.Text }).ToList();
				return true;
			}
			else
			{
				txtRecords = null;
				return false;
			}
		}

		public async Task<IList<TXTRecord>> GetTxtRecordsAsync(string domain, CancellationToken cancellationToken)
		{
			var records = await TryGetRecordsAsync(domain, RecordType.TXT, cancellationToken, true).ConfigureAwait(false);
			return records.OfType<TxtRecord>().Where(txt => txt.Name.Equals(domain, StringComparison.OrdinalIgnoreCase)).Select(txt => new TXTRecord { StringCount = 1, StringArray = txt.Text }).ToList();
		}

		public async Task<IList<TXTRecord>?> TryGetTxtRecordsAsync(string domain, CancellationToken cancellationToken)
		{
			var records = await TryGetRecordsAsync(domain, RecordType.TXT, cancellationToken, false).ConfigureAwait(false);
			return records?.OfType<TxtRecord>().Where(txt => txt.Name.Equals(domain, StringComparison.OrdinalIgnoreCase)).Select(txt => new TXTRecord { StringCount = 1, StringArray = txt.Text }).ToList();
		}

		private async Task<IEnumerable<DnsRecord>?> TryGetRecordsAsync(string domain, RecordType type, CancellationToken cancellationToken, bool throwOnError)
		{
			// HACK for now only one DNS request can be processed at a time but eventually we should add support for multiple in-flight requests
			var host = GetDnsHost();

			if (host is null)
				return null;

			Socket? socket = null;
			int id;
			var data = ArrayPool<byte>.Shared.Rent(4096);
			try
			{
				using (await _lock.LockAsync(cancellationToken))
				{
					if (!host.Equals(cachedSocketHost, StringComparison.OrdinalIgnoreCase) || cachedSocket is null)
					{
						var endpoint = new IPEndPoint(IPAddress.Parse(host), 53);
						if (cachedSocket is object)
						{
							socket = cachedSocket;
							cachedSocketHost = null;
							await socket.DisconnectAsync(reuseSocket: true, cancellationToken).ConfigureAwait(false);
						}
						else
						{
							socket = new Socket(endpoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
							socket.ReceiveTimeout = 5000;
							socket.SendTimeout = 5000;
						}
						await socket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
						cachedSocket = null;
					}
					else
					{
						socket = cachedSocket;
						cachedSocket = null;
						cachedSocketHost = null;
					}

					id = nextId++;
				}
				MakeQuery(id, domain, type, data, out var sendLen);
				socket.Send(data.AsSpan(0, sendLen));
				var responseLen = await socket.ReceiveAsync(data, cancellationToken).ConfigureAwait(false);
				var records = ProcessResponse(data.AsSpan(0, responseLen));
				return records;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				if (throwOnError)
					throw new DnsException("Failed to retrieve records for " + domain, ex);
				return null;
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(data);
				if (socket is object)
				{
					lock (_lock)
					{
						if (cachedSocket is null)
						{
							cachedSocket = socket;
							cachedSocketHost = host;
							socket = null;
						}
					}
					if (socket is object)
					{
						socket.Close();
					}
				}
			}
		}

		private bool TryGetRecords(string domain, RecordType type, [NotNullWhen(true)] out IReadOnlyList<DnsRecord>? records, bool throwOnError)
		{
			// HACK for now only one DNS request can be processed at a time but eventually we should add support for multiple in-flight requests
			records = null;
			var host = GetDnsHost();

			if (host is null)
			{
				return false;
			}

			Socket? socket = null;
			int id;
			var data = ArrayPool<byte>.Shared.Rent(4096);
			try
			{
				using (_lock.Lock())
				{
					if (!host.Equals(cachedSocketHost, StringComparison.OrdinalIgnoreCase) || cachedSocket is null)
					{
						var endpoint = new IPEndPoint(IPAddress.Parse(host), 53);
						if (cachedSocket is object)
						{
							socket = cachedSocket;
							cachedSocketHost = null;
							socket.Disconnect(reuseSocket: true);
						}
						else
						{
							socket = new Socket(endpoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
							socket.ReceiveTimeout = 5000;
							socket.SendTimeout = 5000;
						}
						socket.Connect(endpoint);
						cachedSocket = null;
					}
					else
					{
						socket = cachedSocket;
						cachedSocket = null;
						cachedSocketHost = null;
					}

					id = nextId++;
				}
				MakeQuery(id, domain, type, data, out var sendLen);
				socket.Send(data.AsSpan(0, sendLen));
				var responseLen = socket.Receive(data);
				records = ProcessResponse(data.AsSpan(0, responseLen));
				return true;
			}
			catch (Exception ex)
			{
				if (throwOnError)
					throw new DnsException("Failed to retrieve records for " + domain, ex);
				return false;
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(data);
				if (socket is object)
				{
					lock (_lock)
					{
						if (cachedSocket is null)
						{
							cachedSocket = socket;
							cachedSocketHost = host;
							socket = null;
						}
					}
					if (socket is object)
					{
						socket.Close();
					}
				}
			}
		}

		private void MakeQuery(int id, string name, RecordType type, byte[] data, out int length)
		{
			Array.Clear(data, 0, data.Length);
			// see https://tools.ietf.org/html/rfc1035
			// see https://www2.cs.duke.edu/courses/fall16/compsci356/DNS/DNS-primer.pdf
			data[0] = (byte)(id >> 8);
			data[1] = (byte)(id & 0xFF);
			data[2] = (byte)1; data[3] = (byte)0;
			data[4] = (byte)0; data[5] = (byte)1;
			data[6] = (byte)0; data[7] = (byte)0;
			data[8] = (byte)0; data[9] = (byte)0;
			data[10] = (byte)0; data[11] = (byte)0;

			var position = 12;
			//QNAME
			foreach (var label in name.Split('.'))
			{
				data[position++] = (byte)(label.Length & 0xFF); // TODO make sure no more then 63 chars
				byte[] b = Encoding.ASCII.GetBytes(label);

				for (int k = 0; k < b.Length; k++)
				{
					data[position++] = b[k];
				}

			}
			data[position++] = (byte)0; //QNAME terminator

			data[position++] = (byte)((int)type >> 8);	data[position++] = (byte)((int)type & 0xFF);
			data[position++] = (byte)0;	data[position++] = (byte)1;  // QCLASS
			length = position;
		}

		private static IReadOnlyList<DnsRecord> ProcessResponse(ReadOnlySpan<byte> data)
		{
			var qCount = ((data[4] & 0xFF) << 8) | (data[5] & 0xFF);
			// TODO question count validation
			var aCount = ((data[6] & 0xFF) << 8) | (data[7] & 0xFF);
			// TODO answer count validation
			var position = 12;

			// skip over questions
			for(var i = 0; i < qCount; i++)
			{
				GetName(data, ref position, ignore: true);
				if (position < 0)
				{
					// TODO
					return Array.Empty<DnsRecord>();
				}
				position += 4;
			}

			if (aCount > 0)
			{
				var records = new List<DnsRecord>(aCount);
				for (var i = 0; i < aCount; i++)
				{
					var queryName = GetName(data, ref position, ignore: false); // TODO maybe ignore
					if (position < 0 || (position + 10) >= data.Length)
					{
						// TODO
						return Array.Empty<DnsRecord>();
					}
					var rType = (RecordType)(((data[position++] & 0xFF) << 8) | (data[position++] & 0xFF));
					var rClass = ((data[position++] & 0xFF) << 8) | (data[position++] & 0xFF); // TODO confirm 0x0001
					var ttl = ((data[position++] & 0xFF) << 24) | ((data[position++] & 0xFF) << 16) | ((data[position++] & 0xFF) << 8) | (data[position++] & 0xFF); // in seconds
					var rdLen = ((data[position++] & 0xFF) << 8) | (data[position++] & 0xFF); // length of RDATA

					Debug.Assert(queryName is not null);
					DnsRecord? record = rType switch
					{
						RecordType.MX => new MxRecord(queryName, ttl, data, position),
						RecordType.TXT => new TxtRecord(queryName, ttl, data, position, data.Slice(position, rdLen)),
						RecordType.SRV => new SrvRecord(queryName, ttl, data, position),
						_ => null
					};
					position += rdLen;
					if (record is not null)
					{
						records.Add(record);
					}
					else
					{
						// Windows Subsystem for Android often sends us unrelated DNS records
						// (A type).
					}
				}

				return records;
			}
			return Array.Empty<DnsRecord>();
		}

		static string? GetName(in ReadOnlySpan<byte> data, ref int position, bool ignore)
		{
			string? name = null;
			var len = (data[position++] & 0xFF);
			while (len != 0)
			{
				if ((len & 0b11000000) == 0b11000000) // pointer
				{
					if (position >= data.Length)
					{
						position = -1;
						return null;
					}
					var offset = ((len & 0b00111111) << 8) | (data[position++] & 0xFF);
					if (ignore) return null; // we don't care where the pointer leads to
					name += GetName(data, ref offset, ignore);
					if (offset < 0)
					{
						position = -1;
						return null;
					}
					return name;
				}
				else
				{
					if ((position + len) >= data.Length)
					{
						position = -1;
						return null;
					}
					if (!ignore)
					{
						name += Encoding.ASCII.GetString(data.Slice(position, len));
					}
					position += len;
				}
				if (position >= data.Length)
				{
					position = -1;
					return null;
				}
				len = data[position++] & 0xFF;

				if (len != 0 && !ignore)
				{
					name += ".";
				}
			}
			return name;
		}

		private enum RecordType
		{
			MX = 0x000F,
			TXT = 0x0010,
			SRV = 0x0021,
		}

		private abstract class DnsRecord
		{
			public string Name { get; }
			public RecordType Type { get; }
			public int TTL { get; }

			public DnsRecord(string name, int ttl, RecordType type)
			{
				Name = name;
				TTL = ttl;
				Type = type;
			}
		}

		private class MxRecord : DnsRecord
		{
			public ushort Preference { get; }
			public string? Exchange { get; }

			public MxRecord(string name, int ttl, in ReadOnlySpan<byte> data, int position) : base(name, ttl, RecordType.MX)
			{
				Preference = (ushort)((data[position++] << 8) | (data[position++] & 0xFF));
				Exchange = GetName(data, ref position, ignore: false);
				if (position < 0)
				{
					// TODO error
				}
			}
		}

		private class SrvRecord : DnsRecord
		{
			public ushort Priority { get; }

			public ushort Weight { get; }

			public ushort Port { get; }

			public string? Target { get; }

			public SrvRecord(string name, int ttl, in ReadOnlySpan<byte> data, int position) : base(name, ttl, RecordType.SRV)
			{
				Priority = (ushort)((data[position++] << 8) | (data[position++] & 0xFF));
				Weight = (ushort)((data[position++] << 8) | (data[position++] & 0xFF));
				Port = (ushort)((data[position++] << 8) | (data[position++] & 0xFF));
				Target = GetName(data, ref position, ignore: false);
				if (position < 0)
				{
					// TODO error
				}
			}
		}

		private class TxtRecord : DnsRecord
		{
			public string Text { get; } = string.Empty;

			public TxtRecord(string name, int ttl, in ReadOnlySpan<byte> data, int position, in ReadOnlySpan<byte> rdata) : base(name, ttl, RecordType.TXT)
			{
				var pos = 0;
				while (pos < rdata.Length)
				{
					var len = rdata[pos++] & 0xFF;
					if (len > 0)
					{
						if (pos + len > rdata.Length)
						{
							// TODO error
							return;
						}
						Text += Encoding.UTF8.GetString(rdata.Slice(pos, len));
						pos += len;
					}
				}
			}
		}
	}
}
