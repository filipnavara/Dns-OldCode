using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using netlib.Dns.Records;

namespace netlib.Dns
{
	/// <summary>
	/// Platform agnostic dns service interface.
	/// </summary>
	public interface IDnsService
	{
		/// <summary>
		/// Get SRV records for domain.
		/// </summary>
		IList<SRVRecord> GetSrvRecords(string domain);
		bool TryGetSrvRecords(string domain, [NotNullWhen(true)] out IList<SRVRecord>? srvRecords);
		Task<IList<SRVRecord>> GetSrvRecordsAsync(string domain, CancellationToken cancellationToken) => Task.Run(() => GetSrvRecords(domain)).WaitAsync(cancellationToken);
		Task<IList<SRVRecord>?> TryGetSrvRecordsAsync(string domain, CancellationToken cancellationToken) => Task.Run(() =>
		{
			TryGetSrvRecords(domain, out var records);
			return records;
		}).WaitAsync(cancellationToken);

		/// <summary>
		/// Gets MX records for domain.
		/// </summary>
		IList<MXRecord> GetMxRecords(string domain);
		bool TryGetMxRecords(string domain, [NotNullWhen(true)] out IList<MXRecord>? mxRecords);
		Task<IList<MXRecord>> GetMxRecordsAsync(string domain, CancellationToken cancellationToken) => Task.Run(() => GetMxRecords(domain)).WaitAsync(cancellationToken);
		Task<IList<MXRecord>?> TryGetMxRecordsAsync(string domain, CancellationToken cancellationToken) => Task.Run(() =>
		{
			TryGetMxRecords(domain, out var records);
			return records;
		}).WaitAsync(cancellationToken);

		/// <summary>
		/// Get TXT records for domain.
		/// </summary>
		IList<TXTRecord> GetTxtRecords(string domain);
		bool TryGetTxtRecords(string domain, [NotNullWhen(true)] out IList<TXTRecord>? txtRecords);
		Task<IList<TXTRecord>> GetTxtRecordsAsync(string domain, CancellationToken cancellationToken) => Task.Run(() => GetTxtRecords(domain)).WaitAsync(cancellationToken);
		Task<IList<TXTRecord>?> TryGetTxtRecordsAsync(string domain, CancellationToken cancellationToken) => Task.Run(() =>
		{
			TryGetTxtRecords(domain, out var records);
			return records;
		}).WaitAsync(cancellationToken);
	}
}
