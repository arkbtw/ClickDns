using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Net;
using System.Net.Sockets;
using Windows.Win32;
using Windows.Win32.NetworkManagement.IpHelper;
using System.Runtime.InteropServices;

namespace dnsset;
public record IfInfo(string AdapterName, Guid Guid);
public record IPInfo(IPAddress Ip, string? Url);
public record DnsConfig(IPInfo[] IPInfos, IfInfo IfInfo);

public static class Extension
{
    public static bool IsConfigsSame(this DnsConfig[] configs)
    {
        var curr = configs[0];
        foreach (DnsConfig c in configs)
        {
            if (!Dnsutil.CompareIpInfos(curr.IPInfos, c.IPInfos)) return false;
            curr = c;
        }
        return true;

    }
}
public static class Dnsutil
{
    static IfInfo[] GetAdapters()
    {
        List<IfInfo> ifInfos = [];
        uint len = 0;
        unsafe
        {
            uint r = PInvoke.GetAdaptersAddresses(0, 0, null, null, &len);
            fixed (void* ptr = new byte[len].AsSpan())
            {
                IP_ADAPTER_ADDRESSES_LH* ptr_ad = (IP_ADAPTER_ADDRESSES_LH*)ptr;
                r = PInvoke.GetAdaptersAddresses(0, 0, null, ptr_ad, &len);
                if (r != 0) throw new Exception("error getting adapters");

                for (IP_ADAPTER_ADDRESSES_LH* adapter = ptr_ad; adapter != null; adapter = adapter->Next)
                {
                    MIB_IF_ROW2 ifInfo = new()
                    {
                        InterfaceLuid = adapter->Luid
                    };
                    r = (uint)PInvoke.GetIfEntry2(ref ifInfo);
                    if (r != 0) throw new Exception("error getting interface info");

                    if (ifInfo.InterfaceAndOperStatusFlags.ConnectorPresent)
                    {
                        ifInfos.Add(new IfInfo(ifInfo.Alias.ToString(), ifInfo.InterfaceGuid));
                    }
                }
            }

            return ifInfos.ToArray();
        }
    }

    public static DnsConfig[] GetDnsConfigs()
    {
        List<DnsConfig> cf_list = [];
        unsafe
        {
            foreach (IfInfo adapter in GetAdapters())
            {
                DNS_INTERFACE_SETTINGS3 setting = new()
                {
                    Version = 3
                };

                uint r = (uint)PInvoke.GetInterfaceDnsSettings(adapter.Guid, (DNS_INTERFACE_SETTINGS*)&setting);
                if (r != 0) throw new Exception("error retrieving dns servers");
                var ips = CreateIps(setting);

                setting.Flags = 1;
                r = (uint)PInvoke.GetInterfaceDnsSettings(adapter.Guid, (DNS_INTERFACE_SETTINGS*)&setting);
                if (r != 0) throw new Exception("error retrieving dns servers");
                ips.AddRange(CreateIps(setting));


                cf_list.Add(new DnsConfig(ips.ToArray(), adapter));
            }
        }
        return cf_list.ToArray();
    }

    unsafe static List<IPInfo> CreateIps(DNS_INTERFACE_SETTINGS3 setting)
    {
        var ips = setting.NameServer.ToString().Split(',').Where(ip => ip.Length != 0).Select(el => new IPInfo(IPAddress.Parse(el), null)).ToList();
        for (int i = 0; i < setting.cServerProperties; i++)
        {
            string url = setting.ServerProperties[i].Property.DohSettings->Template.ToString();
            if (url.Length != 1) ips[i] = ips[i] with { Url = url };
        }
        return ips;
    }

    public static bool CompareIpInfos(IPInfo[] first, IPInfo[] second)
    {
        if (first.Length != second.Length) return false;
        foreach (var (i, x) in first.Select((x, i) => (i, x)))
        {
            var s = second[i];
            if (!x.Ip.Equals(s.Ip) || x.Url != s.Url) return false;
        }
        return true;
    }
    public static bool CompareIp(IPInfo[] first, IPInfo[] second)
    {
        if (first.Length != second.Length) return false;
        foreach (var (i, x) in first.Select((x, i) => (i, x)))
        {
            var s = second[i];
            if (!x.Ip.Equals(s.Ip)) return false;
        }
        return true;
    }


    public static void SetDnsConfig(IPInfo[] infos, Guid? adapter)
    {
        if (adapter is Guid id)
        {
            SetConfigs(infos, id);
        }
        else
        {
            foreach (var ad in GetAdapters())
            {
                SetConfigs(infos, ad.Guid);
            }
        }
    }

    unsafe static void SetConfigs(IPInfo[] infos, Guid adapter)
    {
        var ipv4s = infos.Where(i => i.Ip.AddressFamily == AddressFamily.InterNetwork).ToArray();
        var ipv6s = infos.Where(i => i.Ip.AddressFamily == AddressFamily.InterNetworkV6).ToArray();

        var (setting, handles) = AllocateSetting(adapter, ipv4s, false);
        var r = PInvoke.SetInterfaceDnsSettings(adapter, (DNS_INTERFACE_SETTINGS*)&setting);
        handles.ForEach(h => h.Free());
        if (r != 0) throw new Exception("Error applying settings");

        (setting, handles) = AllocateSetting(adapter, ipv6s, true);
        r = PInvoke.SetInterfaceDnsSettings(adapter, (DNS_INTERFACE_SETTINGS*)&setting);
        handles.ForEach(h => h.Free());
        if (r != 0) throw new Exception("Error applying settings");
    }

    unsafe static (DNS_INTERFACE_SETTINGS3, List<GCHandle>) AllocateSetting(Guid adapter, IPInfo[] infos, bool IPv6)
    {
        DNS_INTERFACE_SETTINGS3 setting = new()
        {
            Version = 3,
        };

        var r = PInvoke.GetInterfaceDnsSettings(adapter, (DNS_INTERFACE_SETTINGS*)&setting);
        if (r != 0) throw new Exception("Error getting settings");

        setting.Flags += 0x2;
        if (IPv6) setting.Flags += 0x1;
        setting.Flags += 0x1000;
        setting.NameServer = null;
        setting.cServerProperties = 0;
        setting.ServerProperties = null;
        setting.Flags += 0x2000;
        setting.cProfileServerProperties = 0;
        setting.ProfileNameServer = null;

        if (infos.Length == 0)
        {
            return (setting, []);
        }

        List<GCHandle> handles = [];

        string ipString = infos.Select(info => info.Ip.ToString()).Aggregate((str, ip) => str += ',' + ip);
        var urlIdx = infos.Select(info => info.Url).Select((url, i) => (url, i)).Where(el => el.url != null).ToArray();

        handles.Add(GCHandle.Alloc(ipString, GCHandleType.Pinned));
        fixed (char* str = ipString) setting.NameServer = str;

        if (urlIdx.Length == 0)
        {
            return (setting, handles);
        }

        DNS_SERVER_PROPERTY[] props = [];
        setting.cServerProperties = (uint)urlIdx.Length;

        foreach (var (url, i) in urlIdx)
        {
            var dohSetting = new DNS_DOH_SERVER_SETTINGS()
            {
                Flags = 2
            };
            handles.Add(GCHandle.Alloc(url, GCHandleType.Pinned));
            fixed (char* str = url) dohSetting.Template = str;

            handles.Add(GCHandle.Alloc(dohSetting, GCHandleType.Pinned));
            DNS_SERVER_PROPERTY prop = new()
            {
                Version = 1,
                ServerIndex = (uint)i,
                Property = new DNS_SERVER_PROPERTY_TYPES()
                {
                    DohSettings = (DNS_DOH_SERVER_SETTINGS*)handles.Last().AddrOfPinnedObject(),
                },
                Type = DNS_SERVER_PROPERTY_TYPE.DnsServerDohProperty
            };
            props = [.. props, prop];
        }

        handles.Add(GCHandle.Alloc(props, GCHandleType.Pinned));
        fixed (DNS_SERVER_PROPERTY* arr = props) setting.ServerProperties = arr;
        return (setting, handles);
    }

    public static IPInfo[] GetPreset(string name) => name switch
    {
        "Cloudflare" => [
            new IPInfo(new IPAddress([1,1,1,1]),"https://cloudflare-dns.com/dns-query"),
                new IPInfo(new IPAddress([1,0,0,1]),"https://cloudflare-dns.com/dns-query"),
                new IPInfo(IPAddress.Parse("2606:4700:4700::1111"),"https://cloudflare-dns.com/dns-query"),
                new IPInfo(IPAddress.Parse("2606:4700:4700::1001"),"https://cloudflare-dns.com/dns-query"),
            ],
        "Google" => [
            new IPInfo(new IPAddress([8,8,8,8]),"https://dns.google/query"),
                new IPInfo(new IPAddress([8,8,4,4]),"https://dns.google/query"),
                new IPInfo(IPAddress.Parse("2001:4860:4860::8888"),"https://dns.google/query"),
                new IPInfo(IPAddress.Parse("2001:4860:4860::8844"),"https://dns.google/query"),
            ],
        "Quad9" => [
            new IPInfo(new IPAddress([9,9,9,9]),"https://dns.quad9.net/dns-query"),
                new IPInfo(new IPAddress([149,112,112,112]),"https://dns.quad9.net/dns-query"),
                new IPInfo(IPAddress.Parse("2620:fe::fe"),"https://dns.quad9.net/dns-query"),
                new IPInfo(IPAddress.Parse("2620:fe::9"),"https://dns.quad9.net/dns-query"),
            ],
        "AdGuard" => [
            new IPInfo(new IPAddress([94,140,14,14]),"https://dns.adguard.com/dns-query"),
                new IPInfo(new IPAddress([94,140,14,15]),"https://dns.adguard.com/dns-query"),
                new IPInfo(IPAddress.Parse("2a10:50c0::ad1:ff"),"https://dns.adguard.com/dns-query"),
                new IPInfo(IPAddress.Parse("2a10:50c0::ad2:ff"),"https://dns.adguard.com/dns-query"),
            ],
        _ => throw new Exception("No configs for selected name")
    };
}