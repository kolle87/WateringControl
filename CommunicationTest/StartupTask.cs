using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Foundation.Diagnostics;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;
using System.Diagnostics;
using Windows.Devices.Gpio;
using System.Xml.Linq;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace CommunicationTest
{
    public delegate string TcpRequestReceived(string request); // Basic Server which listens for TCP Requests and provides the user with the ability to craft own responses as strings
    public sealed class TcpServer
    {
        private StreamSocketListener fListener;
        private const uint BUFFER_SIZE = 8192;
        public TcpRequestReceived RequestReceived { get; set; }
        public TcpServer() { }
        public void Initialise(int port)
        {
            fListener = new StreamSocketListener();
            fListener.BindServiceNameAsync(port.ToString());
            fListener.ConnectionReceived += async (sender, args) =>
            {
                HandleRequest(sender, args);
            };
        }
        private async void HandleRequest(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            StringBuilder request = new StringBuilder();
            using (IInputStream input = args.Socket.InputStream)
            {
                byte[] data = new byte[BUFFER_SIZE];
                IBuffer buffer = data.AsBuffer();
                uint dataRead = BUFFER_SIZE;
                while (dataRead == BUFFER_SIZE)
                {
                    await input.ReadAsync(buffer, BUFFER_SIZE, InputStreamOptions.Partial);
                    request.Append(Encoding.UTF8.GetString(data, 0, data.Length));
                    dataRead = buffer.Length;
                }
            }
            string requestString = request.ToString();
            string response = RequestReceived?.Invoke(requestString);
            using (IOutputStream output = args.Socket.OutputStream)
            using (Stream responseStream = output.AsStreamForWrite())
            {
                MemoryStream body;
                if (response != null)
                {
                    body = new MemoryStream(Encoding.UTF8.GetBytes(response));
                }
                else
                {
                    body = new MemoryStream(Encoding.UTF8.GetBytes("No response specified"));
                }
                var header = Encoding.UTF8.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nContent-Type: application/xml\r\nConnection: close\r\n\r\n");
                await responseStream.WriteAsync(header, 0, header.Length);
                await body.CopyToAsync(responseStream);
                await responseStream.FlushAsync();
            }
        }
    }
    public sealed class TwiServer
    {
        private I2cDevice TWI_Temperature;  // 0x48
        private I2cDevice TWI_uController;  // 0x56
        private I2cDevice TWI_VisibleLight; // 0x60

        public async void InitTWIAsync()
        {
            Debug.WriteLine("TWI Interface: Start initialisation...");
            var settings1 = new I2cConnectionSettings(0x48) { BusSpeed = I2cBusSpeed.FastMode };   // Temperature Sensor
            var settings2 = new I2cConnectionSettings(0x56) { BusSpeed = I2cBusSpeed.FastMode };   // ATmega uController
            var settings3 = new I2cConnectionSettings(0x60) { BusSpeed = I2cBusSpeed.FastMode };   // Visible Light Sensor
            var controller = await I2cController.GetDefaultAsync();                                // Create an I2cDevice with our selected bus controller and I2C settings

            TWI_Temperature = controller.GetDevice(settings1);
            TWI_uController = controller.GetDevice(settings2);
            TWI_VisibleLight = controller.GetDevice(settings3);
            if (TWI_Temperature == null)  { Debug.WriteLine("TWI_Temperature: FAILURE WHILE INIT");  return; }
            if (TWI_uController == null)  { Debug.WriteLine("TWI_uController: FAILURE WHILE INIT");  return; }
            if (TWI_VisibleLight == null) { Debug.WriteLine("TWI_VisibleLight: FAILURE WHILE INIT"); return; }
            TWI_Temperature.ConnectionSettings.SharingMode  = I2cSharingMode.Shared;
            TWI_uController.ConnectionSettings.SharingMode  = I2cSharingMode.Shared;
            TWI_VisibleLight.ConnectionSettings.SharingMode = I2cSharingMode.Shared;
        }

        // ----- ATmega Commands
        public void TWI_ATmega_ResetCounter() { var vARC = new byte[] { 0x40 }; TWI_uController.Write(vARC); }
        public byte TWI_ATmega_ReadSensor(byte vChn)
        {
            var vASr = new byte[] { (byte)(32 + vChn) };    // 0x20 ... 0x28
            var vASa = new byte[1];
            TWI_uController.Write(vASr);
            TWI_uController.Read(vASa);
            return vASa[0];
        }

        public int TWI_ATmega_ReadPressure()
        {
            var vASr = new byte[] { 0x27 };    // 0x20 ... 0x28
            var vASa = new byte[1];
            TWI_uController.Write(vASr);
            TWI_uController.Read(vASa);
            var tmp_press = (vASa[0] - 45) * 0.56;
            return Convert.ToInt16(Math.Round(tmp_press));
        }

        public int TWI_ATmega_ReadRain()
        {
            var vASr = new byte[] { 0x25 };    // 0x20 ... 0x28
            var vASa = new byte[1];
            TWI_uController.Write(vASr);
            TWI_uController.Read(vASa);
            double tmp_rain = (vASa[0] / 255) * 100;
            return Convert.ToInt16(Math.Round(tmp_rain));
        }

        public int TWI_ATmega_ReadLevel()
        {
            var vASr = new byte[] { 0x26 };    // 0x20 ... 0x28
            var vASa = new byte[1];
            TWI_uController.Write(vASr);
            TWI_uController.Read(vASa);
            return vASa[0];
        }

        // ----- Light sensor commands
        public void TWI_Light_Prepare()
        {
            var vReq1 = new byte[1];
            var vReq2 = new byte[2];
            var vRes1 = new byte[1];
            var vRes2 = new byte[2];

            vReq1[0] = 0x18; vReq2[1] = 0x01; TWI_VisibleLight.Write(vReq2);             // Restart

            var t_wait = Task.Run(async delegate { await Task.Delay(1000); });          //wait 1s

            vReq1[0] = 0x02; TWI_VisibleLight.WriteRead(vReq1, vRes1);  // check Seq_ID
            Debug.WriteLine("[Light] Seq_ID is {0}", vRes1[0]);

            vReq2[0] = 0x07; vReq2[1] = 0x17; TWI_VisibleLight.Write(vReq2);             // set Sensor to normal operation mode
            vReq1[0] = 0x07; TWI_VisibleLight.WriteRead(vReq1, vRes1);  // check if register was written
            if (vRes1[0] != 0x17) { Debug.WriteLine("[Light] 0x17 not set"); }

            vReq2[0] = 0x08; vReq2[1] = 0xFF; TWI_VisibleLight.Write(vReq2);             // set measuring rate (H)
            vReq1[0] = 0x08; TWI_VisibleLight.WriteRead(vReq1, vRes1);  // check if register was written
            if (vRes1[0] != 0xFF) { Debug.WriteLine("[Light] 0x08 not set"); }

            vReq2[0] = 0x09; vReq2[1] = 0xFF; TWI_VisibleLight.Write(vReq2);             // set measuring rate (L)
            vReq1[0] = 0x09; TWI_VisibleLight.WriteRead(vReq1, vRes1);  // check if register was written
            if (vRes1[0] != 0xFF) { Debug.WriteLine("[Light] 0x09 not set"); }

            vReq2[0] = 0x13; vReq2[1] = 0x29; TWI_VisibleLight.Write(vReq2);             // set UV coeff 0
            vReq1[0] = 0x13; TWI_VisibleLight.WriteRead(vReq1, vRes1);  // check if register was written
            if (vRes1[0] != 0x29) { Debug.WriteLine("[Light] 0x13 not set"); }

            vReq2[0] = 0x14; vReq2[1] = 0x89; TWI_VisibleLight.Write(vReq2);             // set UV coeff 1
            vReq1[0] = 0x14; TWI_VisibleLight.WriteRead(vReq1, vRes1);  // check if register was written
            if (vRes1[0] != 0x89) { Debug.WriteLine("[Light] 0x14 not set"); }

            vReq2[0] = 0x15; vReq2[1] = 0x02; TWI_VisibleLight.Write(vReq2);             // set UV coeff 2
            vReq1[0] = 0x15; TWI_VisibleLight.WriteRead(vReq1, vRes1);  // check if register was written
            if (vRes1[0] != 0x02) { Debug.WriteLine("[Light] 0x15 not set"); }

            vReq2[0] = 0x16; vReq2[1] = 0x00; TWI_VisibleLight.Write(vReq2);             // set UV coeff 3
            vReq1[0] = 0x16; TWI_VisibleLight.WriteRead(vReq1, vRes1);  // check if register was written
            if (vRes1[0] != 0x00) { Debug.WriteLine("[Light] 0x16 not set"); }

            vReq2[0] = 0x17; vReq2[1] = 0x20; TWI_VisibleLight.Write(vReq2);             // prep para - VIS high mode
            vReq2[0] = 0x18; vReq2[1] = 0x00; TWI_VisibleLight.Write(vReq2);             // reset response register
            vReq1[0] = 0x20; TWI_VisibleLight.WriteRead(vReq1, vRes1);        // read response register
            if (vRes1[0] != 0x00) { Debug.WriteLine("[Light] response clear failed (178)"); }  // check if response reg is empty
            vReq2[0] = 0x18; vReq2[1] = 0xB2; TWI_VisibleLight.Write(vReq2);             // write command
            vReq1[0] = 0x20; TWI_VisibleLight.WriteRead(vReq1, vRes1);        // read response register
            if (vRes1[0] == 0x00) { Debug.WriteLine("[Light] command write failed (181)"); }  // check if response reg is empty

            vReq2[0] = 0x17; vReq2[1] = 0x20; TWI_VisibleLight.Write(vReq2);             // prep para - IR high mode
            vReq2[0] = 0x18; vReq2[1] = 0x00; TWI_VisibleLight.Write(vReq2);             // reset response register
            vReq1[0] = 0x20; TWI_VisibleLight.WriteRead(vReq1, vRes1);        // read response register
            if (vRes1[0] != 0x00) { Debug.WriteLine("[Light] response clear failed (186)"); }  // check if response reg is empty
            vReq2[0] = 0x18; vReq2[1] = 0xBF; TWI_VisibleLight.Write(vReq2);             // write command
            vReq1[0] = 0x20; TWI_VisibleLight.WriteRead(vReq1, vRes1);        // read response register
            if (vRes1[0] == 0x00) { Debug.WriteLine("[Light] command write failed (189)"); }  // check if response reg is empty	

            vReq2[0] = 0x17; vReq2[1] = 0xB0; TWI_VisibleLight.Write(vReq2);             // prep para - set measuring channels
            vReq2[0] = 0x18; vReq2[1] = 0x00; TWI_VisibleLight.Write(vReq2);             // reset response register
            vReq1[0] = 0x20; TWI_VisibleLight.WriteRead(vReq1, vRes1);        // read response register
            if (vRes1[0] != 0x00) { Debug.WriteLine("[Light] response clear failed (194)"); }  // check if response reg is empty
            vReq2[0] = 0x18; vReq2[1] = 0xA1; TWI_VisibleLight.Write(vReq2);             // write command
            vReq1[0] = 0x20; TWI_VisibleLight.WriteRead(vReq1, vRes1);        // read response register
            if (vRes1[0] == 0x00) { Debug.WriteLine("[Light] command write failed (197)"); }  // check if response reg is empty

            // prep para - start auto mode
            vReq2[0] = 0x18; vReq2[1] = 0x00; TWI_VisibleLight.Write(vReq2);             // reset response register
            vReq1[0] = 0x20; TWI_VisibleLight.WriteRead(vReq1, vRes1);        // read response register
            if (vRes1[0] != 0x00) { Debug.WriteLine("[Light] response clear failed (202)"); }  // check if response reg is empty
            vReq2[0] = 0x18; vReq2[1] = 0x0E; TWI_VisibleLight.Write(vReq2);             // write command
            vReq1[0] = 0x20; TWI_VisibleLight.WriteRead(vReq1, vRes1);        // read response register
            if (vRes1[0] == 0x00) { Debug.WriteLine("[Light] command write failed (205)"); }  // check if response reg is empty
        }

        public int TWI_Light_ReadVis()
        {
            var vLVLr = new byte[] { 0x22 };
            var vLVLa = new byte[1];
            TWI_VisibleLight.WriteRead(vLVLr, vLVLa);

            var vLVHr = new byte[] { 0x23 };
            var vLVHa = new byte[1];
            TWI_VisibleLight.WriteRead(vLVHr, vLVHa);

            var vLVC = vLVLa[0] + (vLVHa[0] << 8);
            return vLVC;
        }
        public int TWI_Light_ReadIR()
        {
            var vLILr = new byte[] { 0x24 };
            var vLILa = new byte[1];
            TWI_VisibleLight.WriteRead(vLILr, vLILa);

            var vLIHr = new byte[] { 0x25 };
            var vLIHa = new byte[1];
            TWI_VisibleLight.WriteRead(vLIHr, vLIHa);

            var vLIC = vLILa[0] + (vLIHa[0] << 8);
            return vLIC;

        }
        public int TWI_Light_ReadUV()
        {
            var vLUHr = new byte[] { 0x2C };
            var vLUHa = new byte[1];
            TWI_VisibleLight.WriteRead(vLUHr, vLUHa);

            var vLULr = new byte[] { 0x2D };
            var vLULa = new byte[1];
            TWI_VisibleLight.WriteRead(vLULr, vLULa);
            
            var vLUC = vLULa[0] + (vLUHa[0] << 8);
            return vLUC;
        }

        // ----- Temperature sensor commands
        public void TWI_Temperature_Start() { var vTSB = new byte[] { 0xEE }; TWI_Temperature.Write(vTSB); }
        public void TWI_Temperature_Config(){ var vTSC = new byte[] { 0xAC, 0x02 }; TWI_Temperature.Write(vTSC); }
        public int TWI_Temperature_Measure()
        {
            var vTHr = new byte[] { 0xAA };
            var vTHa = new byte[2];
            TWI_Temperature.WriteRead(vTHr, vTHa);

            var vNeg = vTHa[0] & 0x80;
            var vTmp = vTHa[0] & 0x7F;
            var vTempCalc = vTmp;

            if (vNeg == 1) {vTempCalc = -1 * (128 - vTmp);}

            return vTempCalc;
        }

    }


    public sealed class StartupTask : IBackgroundTask
    {
        private BackgroundTaskDeferral fDef;
        private TcpServer fTcpServer;
        private TwiServer fTwiServer;
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            fDef = taskInstance.GetDeferral();          // get deferral to keep running

            // ----- configure GPIOs -----------
            GpioController gpio = GpioController.GetDefault();
            if (gpio == null) { Debug.WriteLine("GPIO initialisation FAILURE"); }

            GpioPin Pin_DO1 = gpio.OpenPin(21);
            GpioPin Pin_DO2 = gpio.OpenPin(20);
            GpioPin Pin_DO3 = gpio.OpenPin(16);
            GpioPin Pin_DO4 = gpio.OpenPin(12);
            GpioPin Pin_DO5 = gpio.OpenPin(7);
            GpioPin Pin_DO6 = gpio.OpenPin(8);
            GpioPin Pin_DO7 = gpio.OpenPin(25);
            GpioPin Pin_DO8 = gpio.OpenPin(24);

            Pin_DO1.SetDriveMode(GpioPinDriveMode.Output);
            Pin_DO2.SetDriveMode(GpioPinDriveMode.Output);
            Pin_DO3.SetDriveMode(GpioPinDriveMode.Output);
            Pin_DO4.SetDriveMode(GpioPinDriveMode.Output);
            Pin_DO5.SetDriveMode(GpioPinDriveMode.Output);
            Pin_DO6.SetDriveMode(GpioPinDriveMode.Output);
            Pin_DO7.SetDriveMode(GpioPinDriveMode.Output);
            Pin_DO8.SetDriveMode(GpioPinDriveMode.Output);
            
            Pin_DO1.Write(GpioPinValue.Low);
            Pin_DO2.Write(GpioPinValue.Low);
            Pin_DO3.Write(GpioPinValue.Low);
            Pin_DO4.Write(GpioPinValue.Low);
            Pin_DO5.Write(GpioPinValue.Low);
            Pin_DO6.Write(GpioPinValue.Low);
            Pin_DO7.Write(GpioPinValue.Low);
            Pin_DO8.Write(GpioPinValue.Low);
            
            fTwiServer = new TwiServer();
            fTwiServer.InitTWIAsync();

            fTwiServer.TWI_Temperature_Config();
            fTwiServer.TWI_Temperature_Start();

            // ----- INIT Light Sensor --------
            fTwiServer.TWI_Light_Prepare();
            // --------------------------------

            fTcpServer = new TcpServer();
            fTcpServer.RequestReceived = (request) =>
            {
                var requestLines = request.ToString().Split(' ');
                var url = requestLines.Length > 1 ? requestLines[1] : string.Empty;
                var uri = new Uri("http://localhost" + url);
                switch (uri.Query)
                {
                    case "?120":
                        Debug.WriteLine("Command 120 received, ATmega counter reset");
                        fTwiServer.TWI_ATmega_ResetCounter();
                        return "CMD_120: counter reset";
                    //------ cmd 13n = DOn -> ON  ------------------------------------
                    case "?130":
                        Debug.WriteLine("Command 130 received, ALL VALVES -> OPEN");
                        Pin_DO3.Write(GpioPinValue.High);
                        Pin_DO4.Write(GpioPinValue.High);
                        Pin_DO5.Write(GpioPinValue.High);
                        Pin_DO6.Write(GpioPinValue.High);
                        Pin_DO7.Write(GpioPinValue.High);
                        return "CMD_130: ALL VALVES -> OPEN";
                    case "?131":
                        Debug.WriteLine("Command 131 received, DO_1 -> ON");
                        Pin_DO1.Write(GpioPinValue.High);
                        return "CMD_131: DO_1 -> ON";
                    case "?132":
                        Debug.WriteLine("Command 132 received, DO_2 -> ON");
                        Pin_DO2.Write(GpioPinValue.High);
                        return "CMD_132: DO_2 -> ON";
                    case "?133":
                        Debug.WriteLine("Command 133 received, DO_3 -> ON");
                        Pin_DO3.Write(GpioPinValue.High);
                        return "CMD_133: DO_3 -> ON";
                    case "?134":
                        Debug.WriteLine("Command 134 received, DO_4 -> ON");
                        Pin_DO4.Write(GpioPinValue.High);
                        return "CMD_134: DO_4 -> ON";
                    case "?135":
                        Debug.WriteLine("Command 135 received, DO_5 -> ON");
                        Pin_DO5.Write(GpioPinValue.High);
                        return "CMD_135: DO_5 -> ON";
                    case "?136":
                        Debug.WriteLine("Command 136 received, DO_6 -> ON");
                        Pin_DO6.Write(GpioPinValue.High);
                        return "CMD_136: DO_6 -> ON";
                    case "?137":
                        Debug.WriteLine("Command 137 received, DO_7 -> ON");
                        Pin_DO7.Write(GpioPinValue.High);
                        return "CMD_137: DO_7 -> ON";
                    case "?138":
                        Debug.WriteLine("Command 138 received, DO_8 -> ON");
                        Pin_DO8.Write(GpioPinValue.High);
                        return "CMD_138: DO_8 -> ON";
                    //------ cmd 14n = DOn -> OFF  ------------------------------------
                    case "?140":
                        Debug.WriteLine("Command 140 received, ALL -> OFF");
                        Pin_DO1.Write(GpioPinValue.Low);
                        Pin_DO2.Write(GpioPinValue.Low);
                        Pin_DO3.Write(GpioPinValue.Low);
                        Pin_DO4.Write(GpioPinValue.Low);
                        Pin_DO5.Write(GpioPinValue.Low);
                        Pin_DO6.Write(GpioPinValue.Low);
                        Pin_DO7.Write(GpioPinValue.Low);
                        Pin_DO8.Write(GpioPinValue.Low);
                        return "CMD_140: ALL -> OFF";
                    case "?141":
                        Debug.WriteLine("Command 141 received, DO_1 -> OFF");
                        Pin_DO1.Write(GpioPinValue.Low);
                        return "CMD_141: DO_1 -> OFF";
                    case "?142":
                        Debug.WriteLine("Command 142 received, DO_2 -> OFF");
                        Pin_DO2.Write(GpioPinValue.Low);
                        return "CMD_142: DO_2 -> OFF";
                    case "?143":
                        Debug.WriteLine("Command 143 received, DO_3 -> OFF");
                        Pin_DO3.Write(GpioPinValue.Low);
                        return "CMD_143: DO_3 -> OFF";
                    case "?144":
                        Debug.WriteLine("Command 144 received, DO_4 -> OFF");
                        Pin_DO4.Write(GpioPinValue.Low);
                        return "CMD_144: DO_4 -> OFF";
                    case "?145":
                        Debug.WriteLine("Command 145 received, DO_5 -> OFF");
                        Pin_DO5.Write(GpioPinValue.Low);
                        return "CMD_145: DO_5 -> OFF";
                    case "?146":
                        Debug.WriteLine("Command 146 received, DO_6 -> OFF");
                        Pin_DO6.Write(GpioPinValue.Low);
                        return "CMD_146: DO_6 -> OFF";
                    case "?147":
                        Debug.WriteLine("Command 147 received, DO_7 -> OFF");
                        Pin_DO7.Write(GpioPinValue.Low);
                        return "CMD_147: DO_7 -> OFF";
                    case "?148":
                        Debug.WriteLine("Command 148 received, DO_8 -> OFF");
                        Pin_DO8.Write(GpioPinValue.Low);
                        return "CMD_148: DO_8 -> OFF";
                    //-------- cmd 121 = gather enviromental data ----------------------------------
                    case "?121":
                        Debug.WriteLine("Command 121 received, environmental data requested");
                        XElement EnvDataXML =
                            new XElement("EnvironmentalData",
                            new XElement("Level", fTwiServer.TWI_ATmega_ReadLevel()),
                            new XElement("LightIR", fTwiServer.TWI_Light_ReadIR()),
                            new XElement("LightVIS", fTwiServer.TWI_Light_ReadVis()),
                            new XElement("Temperature", fTwiServer.TWI_Temperature_Measure()),
                            new XElement("LightUV", fTwiServer.TWI_Light_ReadUV()),
                            new XElement("Rain", fTwiServer.TWI_ATmega_ReadRain())
                            );

                        return EnvDataXML.ToString();
                    //-------- cmd 122 = gather sensor data ----------------------------------
                    case "?122":
                        Debug.WriteLine("Command 122 received, sensors data requested");
                        // 
                        XElement SensDataXML =
                            new XElement("SensorData",
                            new XElement("Flow1", fTwiServer.TWI_ATmega_ReadSensor(0)),
                            new XElement("Flow2", fTwiServer.TWI_ATmega_ReadSensor(1)),
                            new XElement("Flow3", fTwiServer.TWI_ATmega_ReadSensor(2)),
                            new XElement("Flow4", fTwiServer.TWI_ATmega_ReadSensor(3)),
                            new XElement("Flow5", fTwiServer.TWI_ATmega_ReadSensor(4)),
                            new XElement("Press", fTwiServer.TWI_ATmega_ReadPressure())
                            );

                        return SensDataXML.ToString();
                    //-------- cmd 123 = all sensor data for service tool ----------------------------------
                    case "?123":
                        Debug.WriteLine("Command 123 received, GPIO states reaquested");
                        // 
                        XElement ServiceDataXML =
                            new XElement("GPIO_States",
                            new XElement("DO1", Pin_DO1.Read()),
                            new XElement("DO2", Pin_DO2.Read()),
                            new XElement("DO3", Pin_DO3.Read()),
                            new XElement("DO4", Pin_DO4.Read()),
                            new XElement("DO5", Pin_DO5.Read()),
                            new XElement("DO6", Pin_DO6.Read()),
                            new XElement("DO7", Pin_DO7.Read()),
                            new XElement("DO8", Pin_DO8.Read())                                                      
                            );

                        return ServiceDataXML.ToString();
                    // -------- unknown request code -   
                    default:
                        return "FAILURE_UNKNOWN";
                }
                ;
            };
            fTcpServer.Initialise(8081);
        }
    }

}
