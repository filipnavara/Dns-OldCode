//#define BONJOUR_INSTALLED
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
//using System.Linq;
using System.Text;
using netlib.Dns.Bonjour;
using netlib.Dns.Records;
using NUnit.Framework;
// ReSharper disable InconsistentNaming

namespace netlib.Dns.Test
{
	[TestFixture]
	public class DnsLookupTest
	{
		[Test]
		[Category("ExternalServices")]
		public void SrvExternalTest()
		{
			var dnsService = DnsServiceFactory.Create();
			var records = dnsService.GetSrvRecords("_autodiscover._tcp.e.emclient.com");
			Assert.That(records.Count, Is.EqualTo(1));
			Assert.That(records[0].NameNext, Is.EqualTo("autodiscover-s.outlook.com"));
		}

		[Test]
		[Category("ExternalServices")]
		public void MxExternalTest()
		{
			var dnsService = DnsServiceFactory.Create();
			var records = dnsService.GetMxRecords("e.emclient.com");
			Assert.That(records.Count, Is.EqualTo(1));
			Assert.That(records[0].Exchange, Is.EqualTo("e-emclient-com.mail.protection.outlook.com"));
		}


#if !MAC && BONJOUR_INSTALLED
		[Test]
		public void SimulateVariousDnsRequests()
		{
			Console.WriteLine("Test start " + DateTime.Now);

			IDnsService service = new BonjourServiceAdapter();
			IList<MXRecord> mxRecords = service.GetMxRecords("gmail.com");
			IList<SRVRecord> xmppRecords = service.GetSrvRecords("_xmpp-client._tcp.gmail.com");
			IList<SRVRecord> discoRecords = service.GetSrvRecords("_autodiscover._tcp.emclient.com");
			IList<TXTRecord> txtRecords = service.GetTxtRecords("microsoft.com");

			try
			{
				IList<MXRecord> emptyMxRecords = service.GetMxRecords("example.com");
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
			}

			try
			{
				IList<MXRecord> failMxRecords = service.GetMxRecords("example123456789asdfzxcvzxcvagdf.com");
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
			}

			try
			{
				service.GetSrvRecords("_xmpp-client._tcp.outlook.com");
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
			}

			Console.WriteLine("Test finish " + DateTime.Now);
		}

		[Test]
		public void TestXmppSrvRecordsEqualFromBonjourAndWinDns()
		{

			try
			{
				Assert.IsTrue(CallAndCompareResult(service => service.GetSrvRecords("_xmpp-client._tcp.gmail.com")));
				Assert.IsTrue(CallAndCompareResult(service => service.GetSrvRecords("_xmpp-client._tcp.jabbim.cz")));
				Assert.IsTrue(CallAndCompareResult(service => service.GetSrvRecords("_xmpp-client._tcp.jabber.org")));
				Assert.IsTrue(CallAndCompareResult(service => service.GetSrvRecords("_xmpp-client._tcp.outlook.com"))); // nonexistent domain

				Assert.IsTrue(CallAndCompareResult(service => service.GetSrvRecords("_xmpp-client._tcp.12jabber.net")));
				Assert.IsTrue(CallAndCompareResult(service => service.GetSrvRecords("_xmpp-client._tcp.blah.im")));
				Assert.IsTrue(CallAndCompareResult(service => service.GetSrvRecords("_xmpp-client._tcp.0nl1ne.at")));
				Assert.IsTrue(CallAndCompareResult(service => service.GetSrvRecords("_xmpp-client._tcp.12jabber.com")));

			}
			catch (Exception e)
			{
				throw;
			}
		}

		[Test]
		public void TestMXRecordsEqualFromBonjourAndWinDns()
		{
			try
			{
				Assert.IsTrue(CallAndCompareResult(service => service.GetMxRecords("gmail.com")));
				Assert.IsTrue(CallAndCompareResult(service => service.GetMxRecords("emclient.com")));
				Assert.IsTrue(CallAndCompareResult(service => service.GetMxRecords("jabbim.cz")));
				Assert.IsTrue(CallAndCompareResult(service => service.GetMxRecords("outlook.com")));
			}
			catch (Exception e)
			{
				throw;
			}
		}

		[Test]
		public void TestAutodiscoverEqualFromBonjourAndWinDns()
		{
			Assert.IsTrue(CallAndCompareResult(service => service.GetSrvRecords("_autodiscover._tcp.emclient.com")));
			Assert.IsTrue(CallAndCompareResult(service => service.GetSrvRecords("_autodiscover._tcp.icewarp.com")));
		}

		[Test]
		public void TestTXTEqualFromBonjourAndWinDns()
		{
			Assert.IsTrue(CallAndCompareResult(service => service.GetTxtRecords("microsoft.com")));
			Assert.IsTrue(CallAndCompareResult(service => service.GetTxtRecords("emclient.com")));
		}

		[Test]
		public void TestTXTXBosh()
		{
			var winRecords = new WinDnsService().GetTxtRecords("_xmppconnect.jabbim.cz");
			var bonjourRecords = new BonjourServiceAdapter().GetTxtRecords("_xmppconnect.jabbim.cz");

			Assert.IsTrue(SortedSequencesEqual(winRecords, bonjourRecords));

			Assert.IsTrue(winRecords.Any(x => x.StringArray.StartsWith("_xmpp-client-xbosh=")));
			Assert.IsTrue(bonjourRecords.Any(x => x.StringArray.StartsWith("_xmpp-client-xbosh=")));
		}

		private delegate IEnumerable<T> TestServicesDelegate<T>(IDnsService service);

		private bool CallAndCompareResult<T>(TestServicesDelegate<T> callback)
		{
			IDnsService bonjourService = new BonjourServiceAdapter();
			IDnsService winDnsService = new WinDnsService();

			
			IEnumerable<T> bonjourResult = callback(bonjourService);
			try
			{
				IEnumerable<T> winDnsResult = callback(winDnsService);

				return SortedSequencesEqual(bonjourResult, winDnsResult);
			}
			catch (DnsException e)
			{
				if (e.ErrorCode == 9003 // DNS API error 9003 (domain not exist) is treated equal to empty Bonjour service resultlist
					|| e.ErrorCode == 9501) // DNS_INFO_NO_RECORDS
					return bonjourResult == null || !bonjourResult.Any();
				else
					throw;
			}
		}

		private static bool SortedSequencesEqual<T>(IEnumerable<T> bonjourResult, IEnumerable<T> winDnsResult)
		{
			IEnumerable<T> bonjourResultSorted = bonjourResult.OrderBy(r => r.ToString());
			IEnumerable<T> winDnsSorted = winDnsResult.OrderBy(r => r.ToString()).ToArray();

			return bonjourResultSorted.SequenceEqual(winDnsSorted);
		}
#endif
	}
}
