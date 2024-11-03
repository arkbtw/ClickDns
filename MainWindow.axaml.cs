using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using MsBox.Avalonia;

namespace dnsset;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        string[] presets = ["Manual", "Cloudflare", "Google", "Quad9","AdGuard"];
        var configs = Dnsutil.GetDnsConfigs();
        Presets.ItemsSource = presets;
        Presets.SelectedItem = presets[0];

        if (configs.IsConfigsSame() && configs[0].IPInfos.Length != 0)
        {
            var isDohNull = configs[0].IPInfos[0].Url == null;
            string? match = presets.Skip(1).Where(p => Dnsutil.CompareIp(Dnsutil.GetPreset(p), configs[0].IPInfos)).FirstOrDefault();
            if (match is string s)
            {
                Presets.SelectedItem = s;
                if (!isDohNull) CheckBox_EnableEnc.IsChecked = true;
            }
        }
    }

    void ConfigClick(object sender, RoutedEventArgs e)
    {
        Window windows = new ConfigureWindow();
        windows.Show();
    }


    void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        CheckBox_EnableEnc.IsChecked = false;
        if ((string)Presets.SelectedItem! == "Manual")
        {
            CButton.IsVisible = true;
            CheckBox_EnableEnc.IsEnabled = false;
        }
        else
        {
            CheckBox_EnableEnc.IsEnabled = true;
            CButton.IsVisible = false;
        }
    }

    void SaveButton_Click(object sender, RoutedEventArgs e)
    {

        try
        {
            IPInfo[] infos = Dnsutil.GetPreset((string)Presets.SelectedItem!);
            if (!(bool)CheckBox_EnableEnc.IsChecked!) infos = infos.Select(el => el with { Url = null }).ToArray();
            Dnsutil.SetDnsConfig(infos, null);
            MessageBoxManager.GetMessageBoxStandard("Success", "Dns settings has been applied").ShowAsync();
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
}