using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Net.Http;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace WateringPlantControlInterface
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        HttpClient client = new HttpClient();
        public MainPage()
        {
            this.InitializeComponent();
        }

        private void ToggleSwitch1_Toggled(object sender, RoutedEventArgs e)
        {
            if (ToggleSwitch1.IsOn) { client.GetStringAsync("http://192.168.1.50:8081/a?131"); }
            else { client.GetStringAsync("http://192.168.1.50:8081/a?141"); }
        }

        private void ToggleSwitch2_Toggled(object sender, RoutedEventArgs e)
        {
            if (ToggleSwitch2.IsOn) { client.GetStringAsync("http://192.168.1.50:8081/a?132"); }
            else { client.GetStringAsync("http://192.168.1.50:8081/a?142"); }
        }

        private void ToggleSwitch3_Toggled(object sender, RoutedEventArgs e)
        {
            if (ToggleSwitch3.IsOn) { client.GetStringAsync("http://192.168.1.50:8081/a?133"); }
            else { client.GetStringAsync("http://192.168.1.50:8081/a?143"); }
        }

        private void ToggleSwitch4_Toggled(object sender, RoutedEventArgs e)
        {
            if (ToggleSwitch4.IsOn) { client.GetStringAsync("http://192.168.1.50:8081/a?134"); }
            else { client.GetStringAsync("http://192.168.1.50:8081/a?144"); }
        }

        private void ToggleSwitch5_Toggled(object sender, RoutedEventArgs e)
        {
            if (ToggleSwitch5.IsOn) { client.GetStringAsync("http://192.168.1.50:8081/a?135"); }
            else { client.GetStringAsync("http://192.168.1.50:8081/a?145"); }
        }

        private void ToggleSwitch6_Toggled(object sender, RoutedEventArgs e)
        {
            if (ToggleSwitch6.IsOn) { client.GetStringAsync("http://192.168.1.50:8081/a?136"); }
            else { client.GetStringAsync("http://192.168.1.50:8081/a?146"); }
        }

        private void ToggleSwitch7_Toggled(object sender, RoutedEventArgs e)
        {
            if (ToggleSwitch7.IsOn) { client.GetStringAsync("http://192.168.1.50:8081/a?137"); }
            else { client.GetStringAsync("http://192.168.1.50:8081/a?147"); }
        }

        private void ToggleSwitch8_Toggled(object sender, RoutedEventArgs e)
        {
            if (ToggleSwitch8.IsOn) { client.GetStringAsync("http://192.168.1.50:8081/a?138"); }
            else { client.GetStringAsync("http://192.168.1.50:8081/a?148"); }
        }
    }
}
