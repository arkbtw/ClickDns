using System;
using System.Net;
using Avalonia;
using Avalonia.Controls;

namespace dnsset;

public partial class IpControl : UserControl
{
    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<IpControl, string>(nameof(Text), defaultValue: "Empty");
    public static readonly StyledProperty<string> IpAddressProperty = AvaloniaProperty.Register<IpControl, string>(nameof(IpAddress), defaultValue: "Empty");
    public static readonly StyledProperty<bool> EnableDohProperty = AvaloniaProperty.Register<IpControl, bool>(nameof(EnableDoh), defaultValue: false);
    public static readonly StyledProperty<bool> IsIPv6Property = AvaloniaProperty.Register<IpControl, bool>(nameof(IsIpv6), defaultValue: false);
    public static readonly StyledProperty<string> DohUrlProperty = AvaloniaProperty.Register<IpControl, string>(nameof(DohUrl), defaultValue: "Empty");
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string IpAddress { get => GetValue(IpAddressProperty); set => SetValue(IpAddressProperty, value); }
    public bool EnableDoh { get => GetValue(EnableDohProperty); set => SetValue(EnableDohProperty, value); }
    public bool IsIpv6 { get => GetValue(IsIPv6Property); set => SetValue(IsIPv6Property, value); }
    public string DohUrl { get => GetValue(DohUrlProperty); set => SetValue(DohUrlProperty, value); }
    public IpControl()
    {
        InitializeComponent();
        DataContext = this;
    }

    public void AddIp(IPInfo info)
    {
        IpAddress = info.Ip.ToString();
        if (info.Url != null)
        {
            EnableDoh = true;
            DohUrl = info.Url!;
        }
    }

    public IPInfo GetIp()
    {
        string family = IsIpv6 ? "IPv6" : "IPv4";
        IPAddress addr;
        try
        {
            addr = IPAddress.Parse(IpAddress);
        }
        catch (Exception)
        {
            throw new FormatException($"{family} {Text} is incorrect");
        }

        if (IsIpv6 && addr.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6) throw new Exception($"{family} {Text} should be IPv6");
        if (!IsIpv6 && addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6) throw new Exception($"{family} {Text} should be IPv4");

        string? url = null;
        if (EnableDoh)
        {
            if (!Uri.IsWellFormedUriString(DohUrl, UriKind.Absolute)) throw new FormatException($"{family} {Text} URL is incorrect");
            url = DohUrl;
        }
        return new IPInfo(addr, url);
    }

    public void Reset()
    {
        IpAddress = "";
        EnableDoh = false;
        DohUrl = "";
    }
}