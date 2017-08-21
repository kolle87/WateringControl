using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using System.Windows.Input;
using System.Diagnostics;
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
using Windows.UI.Popups;
using System.Threading.Tasks;
using System.Globalization;
using Windows.UI;
using Windows.UI.Xaml.Media.Imaging;
using System.Reflection;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace WateringPlantControlInterface
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        HttpClient      client = new HttpClient();
        DispatcherTimer Timer1 = new DispatcherTimer();

        private void Timer1_Tick(object sender, object e)
        {
            // Update indicators
        }

        public async void GetDataUpdate()
        {
            Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss.f") + " - Request Sensor Data...");
            WebRequest W_wrGETURL = WebRequest.Create("http://192.168.1.50:8081/a?123");
            WebResponse W_response = await W_wrGETURL.GetResponseAsync();
            Stream W_dataStream = W_response.GetResponseStream();
            Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss.f") + " - ...Data received");

            XElement SensorDataObj = XElement.Load(W_dataStream);
            XElement xmlFlow1 = SensorDataObj.Element("Flow1");
            XElement xmlFlow2 = SensorDataObj.Element("Flow2");
            XElement xmlFlow3 = SensorDataObj.Element("Flow3");
            XElement xmlFlow4 = SensorDataObj.Element("Flow4");
            XElement xmlFlow5 = SensorDataObj.Element("Flow5");
            XElement xmlPress = SensorDataObj.Element("Press");
            XElement xmlLevel = SensorDataObj.Element("Level");
            XElement xmlRain  = SensorDataObj.Element("Rain");
            XElement xmlTemp  = SensorDataObj.Element("Temperature");
            XElement xmlVIS   = SensorDataObj.Element("LightVIS");
            XElement xmlIR    = SensorDataObj.Element("LightIR");
            XElement xmlUV    = SensorDataObj.Element("LightUV");

            Text_Flow1.Text = string.Format("Pulses: {0}   =   {1:N0}ml", xmlFlow1.Value, (Convert.ToInt16(xmlFlow1.Value) / 91));
            Text_Flow2.Text = string.Format("Pulses: {0}   =   {1:N0}ml", xmlFlow2.Value, (Convert.ToInt16(xmlFlow2.Value) / 91));
            Text_Flow3.Text = string.Format("Pulses: {0}   =   {1:N0}ml", xmlFlow3.Value, (Convert.ToInt16(xmlFlow3.Value) / 91));
            Text_Flow4.Text = string.Format("Pulses: {0}   =   {1:N0}ml", xmlFlow4.Value, (Convert.ToInt16(xmlFlow4.Value) / 91));
            Text_Flow5.Text = string.Format("Pulses: {0}   =   {1:N0}ml", xmlFlow5.Value, (Convert.ToInt16(xmlFlow5.Value) / 91));
            Text_Press.Text = string.Format("Pressure: {0:N0}bar"   , (Convert.ToInt16(xmlPress.Value) / 10));
            Text_Level.Text = string.Format("Tank Level: {0}%"      , (Convert.ToInt16(xmlLevel.Value) - 130));
            Text_Rain.Text  = string.Format("Rain: {0}%"            , (Convert.ToInt16(xmlRain.Value)));
            Text_Temp.Text  = string.Format("Temperature: {0} degC" , (Convert.ToInt16(xmlTemp.Value)));
            Text_Vis.Text   = string.Format("Visible Light: {0}"    , (Convert.ToInt16(xmlVIS.Value)));
            Text_IR.Text    = string.Format("IR Light: {0}"         , (Convert.ToInt16(xmlIR.Value)));
            Text_UV.Text    = string.Format("UV Index: {0}"         , (Convert.ToInt16(xmlUV.Value)));

            Bar_Flow1.Value = Convert.ToInt16(xmlFlow1.Value);
            Bar_Flow2.Value = Convert.ToInt16(xmlFlow2.Value);
            Bar_Flow3.Value = Convert.ToInt16(xmlFlow3.Value);
            Bar_Flow4.Value = Convert.ToInt16(xmlFlow4.Value);
            Bar_Flow5.Value = Convert.ToInt16(xmlFlow5.Value);
            Bar_Press.Value = Convert.ToInt16(xmlPress.Value);
            Bar_Level.Value = Convert.ToInt16(xmlLevel.Value);
            Bar_Rain.Value  = Convert.ToInt16(xmlRain.Value);

        }

        public MainPage()
        {
            this.InitializeComponent();
            Timer1.Interval = new TimeSpan(0, 0, 0, 1, 0);
            Timer1.Tick += Timer1_Tick;
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

        private void Btn_RstFlow_Click(object sender, RoutedEventArgs e)
        {
            client.GetStringAsync("http://192.168.1.50:8081/a?120");
        }

        private void Btn_AllOff_Click(object sender, RoutedEventArgs e)
        {
            client.GetStringAsync("http://192.168.1.50:8081/a?140");
        }

        private void Btn_Update_Click(object sender, RoutedEventArgs e)
        {
            GetDataUpdate();
        }
    }
}
