#if ANDROID
using System;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Net;

namespace netlib.Dns.Managed
{
	internal partial class ManagedDnsService
	{
		private static ConnectivityManager connectivityManager = (ConnectivityManager)Application.Context.GetSystemService(Context.ConnectivityService);

		private static IReadOnlyList<string> GetDnsServerAddresses()
		{
			if (OperatingSystem.IsAndroidVersionAtLeast(23))
			{
				if (connectivityManager.ActiveNetwork is Network activeNetwork &&
					connectivityManager.GetLinkProperties(activeNetwork) is LinkProperties linkProperties)
				{
					return linkProperties.DnsServers.Select(dns => dns.HostAddress).ToArray();
				}
			}
			else
			{
				if (connectivityManager.GetAllNetworks() is Network[] networks)
				{
					var servers = new List<string>();

					foreach (var network in networks)
					{
						if (connectivityManager.GetLinkProperties(network) is LinkProperties linkProperties)
						{
							if (linkProperties.Routes.Any(r => r.IsDefaultRoute))
							{
								servers.InsertRange(0, linkProperties.DnsServers.Select(dns => dns.HostAddress));
							}
							else
							{
								servers.AddRange(linkProperties.DnsServers.Select(dns => dns.HostAddress));
							}
						}
					}

					if (servers.Count > 0)
						return servers;
				}
			}

			return Array.Empty<string>();
		}
	}
}
#endif
