using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using netlib.Dns.Records;

namespace netlib.Dns
{
	/// <summary>
	/// Dns service implemented by DNS API.
	/// </summary>
	internal class WindowsDnsService : IDnsService
	{
		/// <summary>
		/// 
		/// </summary>
		public IList<SRVRecord> GetSrvRecords(string domain)
		{
			DnsResponse response = new DnsRequest(domain).GetResponse(DnsRecordType.SRV);
			return response.SRVRecords;
		}
	
		/// <summary>
		/// 
		/// </summary>
		public IList<MXRecord> GetMxRecords(string domain)
		{
			DnsResponse response = new DnsRequest(domain).GetResponse(DnsRecordType.MX);
			return response.MXRecords;
		}

		/// <summary>
		/// 
		/// </summary>
		public IList<TXTRecord> GetTxtRecords(string domain)
		{
			DnsResponse response = new DnsRequest(domain).GetResponse(DnsRecordType.TEXT);
			return response.TXTRecords;
		}

		public bool TryGetSrvRecords(string domain, [NotNullWhen(true)] out IList<SRVRecord> srvRecords)
		{
			if (new DnsRequest(domain).TryGetResponse(DnsRecordType.SRV, out var response))
			{
				srvRecords = response.SRVRecords;
				return true;
			}
			else
			{
				srvRecords = null;
				return false;
			}
		}

		public bool TryGetMxRecords(string domain, [NotNullWhen(true)] out IList<MXRecord> mxRecords)
		{
			if (new DnsRequest(domain).TryGetResponse(DnsRecordType.MX, out var response))
			{
				mxRecords = response.MXRecords;
				return true;
			}
			else
			{
				mxRecords = null;
				return false;
			}
		}

		public bool TryGetTxtRecords(string domain, [NotNullWhen(true)] out IList<TXTRecord> txtRecords)
		{
			if (new DnsRequest(domain).TryGetResponse(DnsRecordType.TEXT, out var response))
			{
				txtRecords = response.TXTRecords;
				return true;
			}
			else
			{
				txtRecords = null;
				return false;
			}
		}
	}
}
