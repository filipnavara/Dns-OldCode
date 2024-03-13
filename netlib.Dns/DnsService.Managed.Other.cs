#if !ANDROID
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;

namespace netlib.Dns.Managed
{
	internal partial class ManagedDnsService
	{
		private static IReadOnlyList<string> GetDnsServerAddresses()
		{
			var servers = new List<string>();

			if (OperatingSystem.IsIOS())
				return new List<string>() { "1.1.1.1" };

			// We use Bonjour on macOS
			Debug.Assert(!OperatingSystem.IsMacOS());

			foreach (NetworkInterface iface in NetworkInterface.GetAllNetworkInterfaces())
			{
				if (iface.OperationalStatus == OperationalStatus.Up &&
					iface.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
					iface.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
					iface.NetworkInterfaceType != NetworkInterfaceType.Unknown) // On OS X some tunnel interfaces (eg. utun0) are reported as Unknown.
				{
					var ipProperties = iface.GetIPProperties();
					if (ipProperties.IsDnsEnabled && ipProperties.DnsAddresses is { Count: > 0 } dnsAddresses)
					{
						servers.AddRange(dnsAddresses.Select(a => a.ToString()));
					}
				}
			}

			return servers;
		}
	}
}
#endif
