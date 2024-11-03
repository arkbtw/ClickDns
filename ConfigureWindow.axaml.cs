
using Avalonia.Controls;
using System.Linq;
using Avalonia.Interactivity;
using System;
using Avalonia.Controls.Primitives;
using System.Collections.Generic;
using MsBox.Avalonia;

namespace dnsset;

public partial class ConfigureWindow : Window
{
    DnsConfig[] configs;
    public ConfigureWindow()
    {
        InitializeComponent();
        configs= Dnsutil.GetDnsConfigs();
        string[] adapters = ["All Adapters", .. configs.Select(c => c.IfInfo.AdapterName)];
        Adapters.ItemsSource = adapters;
        Adapters.SelectedItem = "All Adapters";
    }

    void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Reset();
        if ((string)Adapters.SelectedItem! == "All Adapters" && !configs.IsConfigsSame()) return;

        DnsConfig cf;
        if ((string)Adapters.SelectedItem! == "All Adapters" && configs.IsConfigsSame())
        {
            cf = configs.First();
        }
        else
        {
            cf = configs.Where(c => c.IfInfo.AdapterName == (string)Adapters.SelectedItem!).First();
        }
        IPInfo[] ip4s = cf.IPInfos.Where(ip => ip.Ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToArray();
        IPInfo[] ip6s = cf.IPInfos.Where(ip => ip.Ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6).ToArray();

        if (ip4s.Length != 0)
        {
            Ip4Switch.IsChecked = true;
            Ip4Primary.AddIp(ip4s[0]);
        }
        if (ip4s.Length > 1)
        {
            Ip4Secondary.AddIp(ip4s[1]);
        }

        if (ip6s.Length != 0)
        {
            Ip6Switch.IsChecked = true;
            Ip6Primary.AddIp(ip6s[0]);
        }
        if (ip6s.Length > 1)
        {
            Ip6Secondary.AddIp(ip6s[1]);
        }

    }

    IPInfo[] GetIpAll()
    {
        List<IPInfo> infos = [];

        if ((bool)Ip4Switch.IsChecked!)
        {
            infos.Add(Ip4Primary.GetIp());
            infos.Add(Ip4Secondary.GetIp());
        }
        if ((bool)Ip6Switch.IsChecked!)
        {
            infos.Add(Ip6Primary.GetIp());
            infos.Add(Ip6Secondary.GetIp());
        }
        return infos.ToArray();
    }

    void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            IPInfo[] infos = GetIpAll();
            DnsConfig[] cf = configs.Where(f => f.IfInfo.AdapterName == (string)Adapters.SelectedItem!).ToArray();
            if (cf.Length == 0) Dnsutil.SetDnsConfig(infos, null);
            else Dnsutil.SetDnsConfig(infos, cf.Last().IfInfo.Guid);
            MessageBoxManager.GetMessageBoxStandard("Success", "Dns settings has been applied").ShowAsync();
            configs = Dnsutil.GetDnsConfigs();
        }
        catch (Exception exp)
        {
            MessageBoxManager.GetMessageBoxStandard("Error", exp.Message).ShowAsync();
        }
        (sender as ToggleButton)!.IsChecked = true;
    }
    void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    void Reset()
    {
        Ip4Switch.IsChecked = false;
        Ip6Switch.IsChecked = false;
        foreach (IpControl item in Ip4Items.Children)
        {
            item.Reset();
        }

        foreach (IpControl item in Ip6Items.Children)
        {
            item.Reset();
        }
    }
}