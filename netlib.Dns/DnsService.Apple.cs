using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using netlib.Dns.Records;

namespace netlib.Dns.Bonjour
{
	/// <summary>
	/// Wrapper for Apple Bonjour service.
	/// Due to lack of context in QueryRecordAnswered handler, this class is limited to single operation per instance.
	/// </summary>
	internal class AppleDnsService : IDnsService
	{
		private class QueryContextBase
		{
			public DNSServiceErrorType Error { get; set; }
			public CancellationToken CancellationToken { get; set; }
			public object RecordsLock { get; } = new object();
			public volatile bool LastResultMoreComing = false;
		}

		private class QueryContext<TRecord> : QueryContextBase
		{
			public IList<TRecord> Records { get; } = new List<TRecord>();					
		}

		public IList<SRVRecord> GetSrvRecords(string domain) => GetRecordsInternal<SRVRecord>(domain, DNSServiceType.SRV);
		public IList<MXRecord> GetMxRecords(string domain) => GetRecordsInternal<MXRecord>(domain, DNSServiceType.MX);
		public IList<TXTRecord> GetTxtRecords(string domain) => GetRecordsInternal<TXTRecord>(domain, DNSServiceType.TXT);

		public bool TryGetSrvRecords(string domain, [NotNullWhen(true)] out IList<SRVRecord> srvRecords) => TryGetRecordsInternal(domain, DNSServiceType.SRV, out srvRecords);
		public bool TryGetMxRecords(string domain, [NotNullWhen(true)] out IList<MXRecord> mxRecords) => TryGetRecordsInternal(domain, DNSServiceType.MX, out mxRecords);
		public bool TryGetTxtRecords(string domain, [NotNullWhen(true)] out IList<TXTRecord> txtRecords) => TryGetRecordsInternal(domain, DNSServiceType.TXT, out txtRecords);

		private IList<TRecord> GetRecordsInternal<TRecord>(string domain, DNSServiceType recordType)
		{
			CancellationTokenSource tokenSource = new CancellationTokenSource();
			CancellationToken token = tokenSource.Token;
			EventWaitHandle queryFinishedHandle = new ManualResetEvent(false);

			IntPtr serviceRef = IntPtr.Zero;
			QueryContext<TRecord> context = new QueryContext<TRecord>() {CancellationToken = token};
			GCHandle handle = GCHandle.Alloc(context);
			try
			{
				var callback = new DNSImports.DNSServiceQueryReply(QueryAnsweredCallback);

				DNSServiceErrorType errorCode1 = DNSImports.DNSServiceQueryRecord(out serviceRef, DNSServiceFlags.Timeout | DNSServiceFlags.ReturnIntermediates, 0, domain, recordType,
				                                                                  DNSServiceClass.IN, callback, GCHandle.ToIntPtr(handle));

				if (errorCode1 != DNSServiceErrorType.NoError && errorCode1 != DNSServiceErrorType.Timeout)
					throw new DnsException(errorCode1 + " " + (int)errorCode1);

				ThreadPool.QueueUserWorkItem(delegate
				{
					do
					{
						if (token.IsCancellationRequested)
							return;

						var errorCode = DNSServiceErrorType.NoError;
						try
						{
							DNSImports.DNSServiceProcessResult(serviceRef);
						}
						catch (ArgumentException)
						{
							errorCode = DNSServiceErrorType.NoSuchName;
						}
						catch (Exception)
						{
							errorCode = DNSServiceErrorType.Unknown;
						}

						if (errorCode != DNSServiceErrorType.NoError && errorCode != DNSServiceErrorType.Timeout)
						{
							context.Error = errorCode;
							break;
						}
					}
					while (context.LastResultMoreComing);


					if (token.IsCancellationRequested)
						return;

					queryFinishedHandle.Set();
				});

				queryFinishedHandle.WaitOne(5000);

				lock(context.RecordsLock)
					tokenSource.Cancel();

				if (context.Error == DNSServiceErrorType.Timeout)
					return new List<TRecord>();

				if(context.Error != DNSServiceErrorType.NoError)
					throw new DnsException(context.Error + " " + (int)context.Error);

				return context.Records;
			}
			finally
			{
				if (serviceRef != IntPtr.Zero)
				{
					DNSImports.DNSServiceRefDeallocate(serviceRef);
				}

				handle.Free();

				queryFinishedHandle.Close();			
			}

		}

		private bool TryGetRecordsInternal<TRecord>(string domain, DNSServiceType recordType, [NotNullWhen(true)] out IList<TRecord>? records)
		{
			CancellationTokenSource tokenSource = new CancellationTokenSource();
			CancellationToken token = tokenSource.Token;
			EventWaitHandle queryFinishedHandle = new ManualResetEvent(false);

			IntPtr serviceRef = IntPtr.Zero;
			QueryContext<TRecord> context = new QueryContext<TRecord>() { CancellationToken = token };
			GCHandle handle = GCHandle.Alloc(context);
			try
			{
				var callback = new DNSImports.DNSServiceQueryReply(QueryAnsweredCallback);

				DNSServiceErrorType errorCode1 = DNSImports.DNSServiceQueryRecord(out serviceRef, DNSServiceFlags.Timeout | DNSServiceFlags.ReturnIntermediates, 0, domain, recordType,
																				  DNSServiceClass.IN, callback, GCHandle.ToIntPtr(handle));

				if (errorCode1 != DNSServiceErrorType.NoError && errorCode1 != DNSServiceErrorType.Timeout)
				{
					records = null;
					return false;
				}

				ThreadPool.QueueUserWorkItem((WaitCallback)delegate
				{
					do
					{
						if (token.IsCancellationRequested)
							return;

						var errorCode = DNSServiceErrorType.NoError;
						try
						{
							DNSImports.DNSServiceProcessResult(serviceRef);
						}
						catch (ArgumentException)
						{
							errorCode = DNSServiceErrorType.NoSuchName;
						}
						catch (Exception)
						{
							errorCode = DNSServiceErrorType.Unknown;
						}

						if (errorCode != DNSServiceErrorType.NoError && errorCode != DNSServiceErrorType.Timeout)
						{
							context.Error = errorCode;
							break;
						}
					}
					while (context.LastResultMoreComing);


					if (token.IsCancellationRequested)
						return;

					queryFinishedHandle.Set();
				});

				queryFinishedHandle.WaitOne(5000);

				lock (context.RecordsLock)
					tokenSource.Cancel();

				if (context.Error == DNSServiceErrorType.Timeout)
				{
					records = new List<TRecord>();
					return true;
				}

				if (context.Error != DNSServiceErrorType.NoError)
				{
					records = null;
					return false;
				}

				records = context.Records;
				return true;
			}
			catch
			{
				records = null;
				return false;
			}
			finally
			{
				if (serviceRef != IntPtr.Zero)
				{
					DNSImports.DNSServiceRefDeallocate(serviceRef);
				}

				handle.Free();

				queryFinishedHandle.Close();
			}

		}

		private void QueryAnsweredCallback(
			IntPtr sdRef,
			DNSServiceFlags flags,
			uint interfaceIndex,
			DNSServiceErrorType errorCode,
			string fullName,
			DNSServiceType rrType,
			DNSServiceClass rrClass,
			ushort rdLength,
			byte[] rData,
			uint ttl,
			IntPtr context)
		{
			if (errorCode == DNSServiceErrorType.Timeout)
				return;

			GCHandle handle = GCHandle.FromIntPtr(context);
			if (handle.Target == null || !handle.IsAllocated)
				return;

			QueryContextBase queryContext = (QueryContextBase) handle.Target;

			if (queryContext.CancellationToken.IsCancellationRequested)
				return;

			if (errorCode != DNSServiceErrorType.NoError)
			{
				queryContext.Error = errorCode;
				return;
			}

			queryContext.LastResultMoreComing = (flags & DNSServiceFlags.MoreComing) != 0;

			if (rData.Length > 1)
			{
				if (rrType == DNSServiceType.SRV)
				{
					// bytes in big endian ("network byte order")
					// "\0\u0014\0\0\u0014f\u0004alt4\u0004xmpp\u0001l\u0006google\u0003com\0"

					ushort priority = BinaryPrimitives.ReadUInt16BigEndian(rData.AsSpan(0));
					ushort weight = BinaryPrimitives.ReadUInt16BigEndian(rData.AsSpan(2));
					ushort port = BinaryPrimitives.ReadUInt16BigEndian(rData.AsSpan(4));
					string readableHostString = ParseDomainName(rData, 6);

					SRVRecord rec = new SRVRecord
					{
						NameNext = readableHostString,
						Port = port,
						Priority = priority,
						Weight = weight,
						Pad = 0
					};

					QueryContext<SRVRecord> srvContext = (QueryContext<SRVRecord>) queryContext;
					lock (srvContext.RecordsLock)
					{
						if (!srvContext.CancellationToken.IsCancellationRequested)
							srvContext.Records.Add(rec);
					}

				}
				else if (rrType == DNSServiceType.MX)
				{
					ushort preference = BinaryPrimitives.ReadUInt16BigEndian(rData.AsSpan(0));
					string readableHostString = ParseDomainName(rData, 2);

					MXRecord rec = new MXRecord
					{
						Preference = preference,
						Exchange = readableHostString,
						Pad = 0
					};

					QueryContext<MXRecord> mxContext = (QueryContext<MXRecord>) queryContext;
					lock (mxContext.RecordsLock)
					{
						if (!mxContext.CancellationToken.IsCancellationRequested)
							mxContext.Records.Add(rec);
					}
				}
				else if (rrType == DNSServiceType.TXT)
				{
					TXTRecord rec = new TXTRecord
					{
						StringCount = 1,
						StringArray = ParseDomainName(rData, 0)
					};

					lock (queryContext.RecordsLock)
					{
						if (!queryContext.CancellationToken.IsCancellationRequested)
							((QueryContext<TXTRecord>) queryContext).Records.Add(rec);
					}
				}
				else
				{
					var msg = $"DNS record type {rrType} is not supported";
					System.Diagnostics.Debug.WriteLine(msg);
				}
			}
		}


		private static string ParseDomainName(byte[] data, int position)
		{
			// RFC 1035
			/*
				QNAME           a domain name represented as a sequence of labels, where
								each label consists of a length octet followed by that
								number of octets.  The domain name terminates with the
								zero length octet for the null label of the root.  Note
								that this field may be an odd number of octets; no
								padding is used.
			*/

			StringBuilder builder = new StringBuilder();
			int i = position;
			while (i < data.Length - 1)
			{
				if (i > position)
					builder.Append('.');

				byte segmentLength = data[i];
				i++;

				byte[] segmentData = data.Skip(i).Take(segmentLength).ToArray();
				string segment = Encoding.UTF8.GetString(segmentData);
				builder.Append(segment);

				i += segmentLength;
			}

			return builder.ToString();
		}
	}
}
