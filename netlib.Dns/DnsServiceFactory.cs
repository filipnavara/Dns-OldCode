using System;
using netlib.Dns.Bonjour;
using netlib.Dns.Managed;

namespace netlib.Dns
{
	/// <summary>
	/// Factory for platform specific <see cref="IDnsService"/> implementation.
	/// </summary>
	public static class DnsServiceFactory
	{
		private static readonly IDnsService s_dnsService =
			OperatingSystem.IsWindows() ? new WindowsDnsService() :
			OperatingSystem.IsMacOS() ? new AppleDnsService() :

			new ManagedDnsService();

		public static IDnsService Create() => s_dnsService;
	}
}
