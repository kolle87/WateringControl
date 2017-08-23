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
            GetDataUpdate();
        }

        public async void GetDataUpdate()
        {
            Timer1.Stop();
            Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss.f") + " - Request Sensor Data 1 ...");
            WebRequest W_wrGETURL = WebRequest.Create("http://192.168.1.50:8081/a?121");
            WebResponse W_response = await W_wrGETURL.GetResponseAsync();
            Stream W_dataStream = W_response.GetResponseStream();

            XElement Sensor1DataObj = XElement.Load(W_dataStream);
            XElement xmlTemp  = Sensor1DataObj.Element("Temperature");
            XElement xmlVIS   = Sensor1DataObj.Element("LightVIS");
            XElement xmlIR    = Sensor1DataObj.Element("LightIR");
            XElement xmlUV    = Sensor1DataObj.Element("LightUV");
            XElement xmlRain  = Sensor1DataObj.Element("Rain");

            Text_Rain.Text  = string.Format("Rain: {0}%"            , (Convert.ToInt16(xmlRain.Value)));
            Text_Temp.Text  = string.Format("{0} °C"   , (Convert.ToInt16(xmlTemp.Value)));
            Text_Vis.Text   = string.Format("{0:N0}" , (Convert.ToInt32(xmlVIS.Value)));
            Text_IR.Text    = string.Format("{0:N0}"      , (Convert.ToInt32(xmlIR.Value)));
            Text_UV.Text    = string.Format("{0}"         , (Convert.ToInt16(xmlUV.Value)));

            Bar_Rain.Value  = Convert.ToInt16(xmlRain.Value);

            Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss.f") + " - Request Sensor Data 2 ...");
            WebRequest X_wrGETURL = WebRequest.Create("http://192.168.1.50:8081/a?122");
            WebResponse X_response = await X_wrGETURL.GetResponseAsync();
            Stream X_dataStream = X_response.GetResponseStream();

            XElement Sensor2DataObj = XElement.Load(X_dataStream);
            XElement xmlFlow1 = Sensor2DataObj.Element("Flow1");
            XElement xmlFlow2 = Sensor2DataObj.Element("Flow2");
            XElement xmlFlow3 = Sensor2DataObj.Element("Flow3");
            XElement xmlFlow4 = Sensor2DataObj.Element("Flow4");
            XElement xmlFlow5 = Sensor2DataObj.Element("Flow5");
            XElement xmlPress = Sensor2DataObj.Element("Press");
            XElement xmlLevel = Sensor2DataObj.Element("Level");

            Text_Flow1.Text = string.Format("Pulses: {0}   =   {1:N0}ml", xmlFlow1.Value, (Convert.ToInt16(xmlFlow1.Value) / 0.91));
            Text_Flow2.Text = string.Format("Pulses: {0}   =   {1:N0}ml", xmlFlow2.Value, (Convert.ToInt16(xmlFlow2.Value) / 0.91));
            Text_Flow3.Text = string.Format("Pulses: {0}   =   {1:N0}ml", xmlFlow3.Value, (Convert.ToInt16(xmlFlow3.Value) / 0.91));
            Text_Flow4.Text = string.Format("Pulses: {0}   =   {1:N0}ml", xmlFlow4.Value, (Convert.ToInt16(xmlFlow4.Value) / 0.91));
            Text_Flow5.Text = string.Format("Pulses: {0}   =   {1:N0}ml", xmlFlow5.Value, (Convert.ToInt16(xmlFlow5.Value) / 0.91));
            Text_Press.Text = string.Format("Pressure: {0:N0}bar", (Convert.ToInt16(xmlPress.Value) / 10));
            Text_Level.Text = string.Format("Tank Level: {0}%", (Convert.ToInt16(xmlLevel.Value) - 130));

            Bar_Flow1.Value = Convert.ToInt16(xmlFlow1.Value);
            Bar_Flow2.Value = Convert.ToInt16(xmlFlow2.Value);
            Bar_Flow3.Value = Convert.ToInt16(xmlFlow3.Value);
            Bar_Flow4.Value = Convert.ToInt16(xmlFlow4.Value);
            Bar_Flow5.Value = Convert.ToInt16(xmlFlow5.Value);
            Bar_Press.Value = Convert.ToInt16(xmlPress.Value);
            Bar_Level.Value = Convert.ToInt16(xmlLevel.Value);

            Timer1.Start();
        }

        public MainPage()
        {
            this.InitializeComponent();
            Timer1.Interval = new TimeSpan(0, 0, 0, 2, 0);
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
