using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
namespace Fiddler
{
	internal class DNSResolver
	{
		private enum DnsQueryTypes
		{
			DNS_TYPE_A = 1,
			DNS_TYPE_AAAA = 28
		}
		private struct DnsARecord
		{
			public IntPtr pNext;
			public string pName;
			public short wType;
			public short wDataLength;
			public int flags;
			public int dwTtl;
			public int dwReserved;
			public uint ipAddress;
		}
		private class DNSCacheEntry
		{
			internal ulong iLastLookup;
			internal IPAddress[] arrAddressList;
			internal DNSCacheEntry(IPAddress[] arrIPs)
			{
				this.iLastLookup = Utilities.GetTickCount();
				this.arrAddressList = arrIPs;
			}
		}
		private static readonly Dictionary<string, DNSResolver.DNSCacheEntry> dictAddresses;
		internal static ulong MSEC_DNS_CACHE_LIFETIME;
		private static readonly int COUNT_MAX_A_RECORDS;
		static DNSResolver()
		{
			DNSResolver.COUNT_MAX_A_RECORDS = FiddlerApplication.Prefs.GetInt32Pref("fiddler.network.dns.MaxAddressCount", 5);
			DNSResolver.MSEC_DNS_CACHE_LIFETIME = (ulong)((long)FiddlerApplication.Prefs.GetInt32Pref("fiddler.network.timeouts.dnscache", 150000));
			DNSResolver.dictAddresses = new Dictionary<string, DNSResolver.DNSCacheEntry>();
			FiddlerApplication.Janitor.assignWork(new SimpleEventHandler(DNSResolver.ScavengeCache), 30000u);
		}
		internal static void ClearCache()
		{
			Dictionary<string, DNSResolver.DNSCacheEntry> obj;
			Monitor.Enter(obj = DNSResolver.dictAddresses);
			try
			{
				DNSResolver.dictAddresses.Clear();
			}
			finally
			{
				Monitor.Exit(obj);
			}
		}
		public static void ScavengeCache()
		{
			if (DNSResolver.dictAddresses.Count < 1)
			{
				return;
			}
			if (CONFIG.bDebugSpew)
			{
				FiddlerApplication.DebugSpew("Scavenging DNS Cache...");
			}
			List<string> list = new List<string>();
			Dictionary<string, DNSResolver.DNSCacheEntry> obj;
			Monitor.Enter(obj = DNSResolver.dictAddresses);
			try
			{
				foreach (KeyValuePair<string, DNSResolver.DNSCacheEntry> current in DNSResolver.dictAddresses)
				{
					if (current.Value.iLastLookup < Utilities.GetTickCount() - DNSResolver.MSEC_DNS_CACHE_LIFETIME)
					{
						list.Add(current.Key);
					}
				}
				if (CONFIG.bDebugSpew)
				{
					FiddlerApplication.DebugSpew(string.Concat(new string[]
					{
						"Expiring ",
						list.Count.ToString(),
						" of ",
						DNSResolver.dictAddresses.Count.ToString(),
						" DNS Records."
					}));
				}
				foreach (string current2 in list)
				{
					DNSResolver.dictAddresses.Remove(current2);
				}
			}
			finally
			{
				Monitor.Exit(obj);
			}
			if (CONFIG.bDebugSpew)
			{
				FiddlerApplication.DebugSpew("Done scavenging DNS Cache...");
			}
		}
		public static string InspectCache()
		{
			StringBuilder stringBuilder = new StringBuilder(8192);
			stringBuilder.AppendFormat("DNSResolver Cache\nfiddler.network.timeouts.dnscache: {0}ms\nContents\n--------\n", DNSResolver.MSEC_DNS_CACHE_LIFETIME);
			Dictionary<string, DNSResolver.DNSCacheEntry> obj;
			Monitor.Enter(obj = DNSResolver.dictAddresses);
			try
			{
				foreach (KeyValuePair<string, DNSResolver.DNSCacheEntry> current in DNSResolver.dictAddresses)
				{
					StringBuilder stringBuilder2 = new StringBuilder();
					stringBuilder2.Append(" [");
					IPAddress[] arrAddressList = current.Value.arrAddressList;
					for (int i = 0; i < arrAddressList.Length; i++)
					{
						IPAddress iPAddress = arrAddressList[i];
						stringBuilder2.Append(iPAddress.ToString());
						stringBuilder2.Append(", ");
					}
					stringBuilder2.Remove(stringBuilder2.Length - 2, 2);
					stringBuilder2.Append("]");
					stringBuilder.AppendFormat("\tHostName: {0}, Age: {1}ms, AddressList:{2}\n", current.Key, Utilities.GetTickCount() - current.Value.iLastLookup, stringBuilder2.ToString());
				}
			}
			finally
			{
				Monitor.Exit(obj);
			}
			stringBuilder.Append("--------\n");
			return stringBuilder.ToString();
		}
		public static IPAddress GetIPAddress(string sRemoteHost, bool bCheckCache)
		{
			return DNSResolver.GetIPAddressList(sRemoteHost, bCheckCache, null)[0];
		}
		[DllImport("dnsapi", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
		private static extern int DnsQuery_W([MarshalAs(UnmanagedType.VBByRefStr)] ref string pszName, DNSResolver.DnsQueryTypes wType, uint options, int aipServers, ref IntPtr ppQueryResults, int pReserved);
		[DllImport("dnsapi", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern void DnsRecordListFree(IntPtr pRecordList, int FreeType);
		public static IPAddress[] GetAddressNatively(string sHostname)
		{
			IntPtr zero = IntPtr.Zero;
			IntPtr ptr = IntPtr.Zero;
			List<IPAddress> list = new List<IPAddress>();
			DNSResolver.DnsARecord dnsARecord = default(DNSResolver.DnsARecord);
			int num = DNSResolver.DnsQuery_W(ref sHostname, DNSResolver.DnsQueryTypes.DNS_TYPE_A, 0u, 0, ref zero, 0);
			if (num != 0)
			{
				throw new Win32Exception(num, string.Format("Native DNS Resolution error [0x{0:x}]", num));
			}
			ptr = zero;
			while (!ptr.Equals(IntPtr.Zero))
			{
				dnsARecord = (DNSResolver.DnsARecord)Marshal.PtrToStructure(ptr, typeof(DNSResolver.DnsARecord));
				list.Add(new IPAddress((long)((ulong)dnsARecord.ipAddress)));
				ptr = dnsARecord.pNext;
			}
			DNSResolver.DnsRecordListFree(zero, 0);
			return list.ToArray();
		}
		public static IPAddress[] GetIPAddressList(string sRemoteHost, bool bCheckCache, SessionTimers oTimers)
		{
			IPAddress[] array = null;
			Stopwatch stopwatch = Stopwatch.StartNew();
			IPAddress iPAddress = Utilities.IPFromString(sRemoteHost);
			if (iPAddress != null)
			{
				array = new IPAddress[]
				{
					iPAddress
				};
				if (oTimers != null)
				{
					oTimers.DNSTime = (int)stopwatch.ElapsedMilliseconds;
				}
				return array;
			}
			DNSResolver.DNSCacheEntry dNSCacheEntry;
			if (bCheckCache && DNSResolver.dictAddresses.TryGetValue(sRemoteHost, out dNSCacheEntry))
			{
				if (dNSCacheEntry.iLastLookup > Utilities.GetTickCount() - DNSResolver.MSEC_DNS_CACHE_LIFETIME)
				{
					array = dNSCacheEntry.arrAddressList;
				}
				else
				{
					Dictionary<string, DNSResolver.DNSCacheEntry> obj;
					Monitor.Enter(obj = DNSResolver.dictAddresses);
					try
					{
						DNSResolver.dictAddresses.Remove(sRemoteHost);
					}
					finally
					{
						Monitor.Exit(obj);
					}
				}
			}
			if (array == null)
			{
				if (sRemoteHost.OICEndsWith(".onion") && !FiddlerApplication.Prefs.GetBoolPref("fiddler.network.dns.ResolveOnionHosts", false))
				{
					throw new InvalidOperationException("Hostnames ending in '.onion' cannot be resolved by DNS. You must send this request through a TOR gateway, e.g. oSession[\"X-OverrideGateway\"] = \"socks=127.0.0.1:9150\";");
				}
				try
				{
					if (sRemoteHost.Length > 126)
					{
						array = DNSResolver.GetAddressNatively(sRemoteHost);
					}
					else
					{
						array = Dns.GetHostAddresses(sRemoteHost);
					}
				}
				catch
				{
					if (oTimers != null)
					{
						oTimers.DNSTime = (int)stopwatch.ElapsedMilliseconds;
					}
					throw;
				}
				array = DNSResolver.trimAddressList(array);
				if (array.Length < 1)
				{
					throw new Exception("No valid IPv4 addresses were found for this host.");
				}
				if (array.Length > 0)
				{
					Dictionary<string, DNSResolver.DNSCacheEntry> obj2;
					Monitor.Enter(obj2 = DNSResolver.dictAddresses);
					try
					{
						if (!DNSResolver.dictAddresses.ContainsKey(sRemoteHost))
						{
							DNSResolver.dictAddresses.Add(sRemoteHost, new DNSResolver.DNSCacheEntry(array));
						}
					}
					finally
					{
						Monitor.Exit(obj2);
					}
				}
			}
			if (oTimers != null)
			{
				oTimers.DNSTime = (int)stopwatch.ElapsedMilliseconds;
			}
			return array;
		}
		private static IPAddress[] trimAddressList(IPAddress[] arrResult)
		{
			List<IPAddress> list = new List<IPAddress>();
			for (int i = 0; i < arrResult.Length; i++)
			{
				if (!list.Contains(arrResult[i]) && (CONFIG.bEnableIPv6 || arrResult[i].AddressFamily == AddressFamily.InterNetwork))
				{
					list.Add(arrResult[i]);
					if (DNSResolver.COUNT_MAX_A_RECORDS == list.Count)
					{
						break;
					}
				}
			}
			return list.ToArray();
		}
		internal static string GetCanonicalName(string sHostname)
		{
			string result;
			try
			{
				IPHostEntry hostEntry = Dns.GetHostEntry(sHostname);
				result = hostEntry.HostName;
			}
			catch (Exception eX)
			{
				FiddlerApplication.Log.LogFormat("Failed to retrieve CNAME for \"{0}\", because '{1}'", new object[]
				{
					sHostname,
					Utilities.DescribeException(eX)
				});
				result = string.Empty;
			}
			return result;
		}
		internal static string GetAllInfo(string sHostname)
		{
			IPHostEntry hostEntry;
			string result;
			try
			{
				hostEntry = Dns.GetHostEntry(sHostname);
				goto IL_20;
			}
			catch (Exception eX)
			{
				result = string.Format("FiddlerDNS> DNS Lookup for \"{0}\" failed because '{1}'\n", sHostname, Utilities.DescribeException(eX));
			}
			return result;
			IL_20:
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendFormat("FiddlerDNS> DNS Lookup for \"{0}\":\r\n", sHostname);
			stringBuilder.AppendFormat("CNAME:\t{0}\n", hostEntry.HostName);
			stringBuilder.AppendFormat("Aliases:\t{0}\n", string.Join(";", hostEntry.Aliases));
			stringBuilder.AppendLine("Addresses:");
			IPAddress[] addressList = hostEntry.AddressList;
			for (int i = 0; i < addressList.Length; i++)
			{
				IPAddress iPAddress = addressList[i];
				stringBuilder.AppendFormat("\t{0}\r\n", iPAddress.ToString());
			}
			return stringBuilder.ToString();
		}
	}
}
