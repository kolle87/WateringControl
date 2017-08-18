﻿using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
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
            TWI_uController.WriteRead(vASr, vASa);
            return vASa[0];
        }


        // ----- Light sensor commands
        public void TWI_Light_RegisterService() { var vLRS   = new byte[] { 0x07, 0x17 }; TWI_VisibleLight.Write(vLRS); }
        public void TWI_Light_UVcoef0()         { var vLUVC0 = new byte[] { 0x13, 0x29 }; TWI_VisibleLight.Write(vLUVC0); }
        public void TWI_Light_UVcoef1()         { var vLUVC1 = new byte[] { 0x14, 0x89 }; TWI_VisibleLight.Write(vLUVC1); }
        public void TWI_Light_UVcoef2()         { var vLUVC2 = new byte[] { 0x15, 0x02 }; TWI_VisibleLight.Write(vLUVC2); }
        public void TWI_Light_UVcoef3()         { var vLUVC3 = new byte[] { 0x16, 0x00 }; TWI_VisibleLight.Write(vLUVC3); }
        public void TWI_Light_SetParam()        { var vLSP   = new byte[] { 0x17, 0xB0 }; TWI_VisibleLight.Write(vLSP); }
        public void TWI_Light_WriteParam()      { var vLWP   = new byte[] { 0x18, 0xA1 }; TWI_VisibleLight.Write(vLWP); }
        public void TWI_Light_StartMeas()       { var vLSM   = new byte[] { 0x18, 0x06 }; TWI_VisibleLight.Write(vLSM); }

        public byte TWI_Light_ReadVis_H() { var vLVHr = new byte[] { 0x22 }; var vLVHa = new byte[1]; TWI_VisibleLight.WriteRead(vLVHr, vLVHa); return vLVHa[0]; }
        public byte TWI_Light_ReadVis_L() { var vLVLr = new byte[] { 0x23 }; var vLVLa = new byte[1]; TWI_VisibleLight.WriteRead(vLVLr, vLVLa); return vLVLa[0]; }
        public byte TWI_Light_ReadIR_H()  { var vLIHr = new byte[] { 0x24 }; var vLIHa = new byte[1]; TWI_VisibleLight.WriteRead(vLIHr, vLIHa); return vLIHa[0]; }
        public byte TWI_Light_ReadIR_L()  { var vLILr = new byte[] { 0x25 }; var vLILa = new byte[1]; TWI_VisibleLight.WriteRead(vLILr, vLILa); return vLILa[0]; }
        public byte TWI_Light_ReadUV_H()  { var vLUHr = new byte[] { 0x2C }; var vLUHa = new byte[1]; TWI_VisibleLight.WriteRead(vLUHr, vLUHa); return vLUHa[0]; }
        public byte TWI_Light_ReadUV_L()  { var vLULr = new byte[] { 0x2D }; var vLULa = new byte[1]; TWI_VisibleLight.WriteRead(vLULr, vLULa); return vLULa[0]; }


        // ----- Temperature sensor commands
        public void TWI_Temperature_Meas()  { var vTSM = new byte[] { 0xEE }; TWI_Temperature.Write(vTSM); }
        public byte TWI_Temperature_TempH() { var vTHr = new byte[] { 0xAA }; var vTHa = new byte[2]; TWI_VisibleLight.WriteRead(vTHr, vTHa); return vTHa[0]; }
        public byte TWI_Temperature_TempL() { var vTLr = new byte[] { 0xAA }; var vTLa = new byte[2]; TWI_VisibleLight.WriteRead(vTLr, vTLa); return vTLa[0]; }

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

            Pin_DO1.SetDriveMode(GpioPinDriveMode.OutputOpenSourcePullDown);
            Pin_DO2.SetDriveMode(GpioPinDriveMode.OutputOpenSourcePullDown);
            Pin_DO3.SetDriveMode(GpioPinDriveMode.OutputOpenSourcePullDown);
            Pin_DO4.SetDriveMode(GpioPinDriveMode.OutputOpenSourcePullDown);
            Pin_DO5.SetDriveMode(GpioPinDriveMode.OutputOpenSourcePullDown);
            Pin_DO6.SetDriveMode(GpioPinDriveMode.OutputOpenSourcePullDown);
            Pin_DO7.SetDriveMode(GpioPinDriveMode.OutputOpenSourcePullDown);
            Pin_DO8.SetDriveMode(GpioPinDriveMode.OutputOpenSourcePullDown);

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

            // ----- INIT Light Sensor --------
            fTwiServer.TWI_Light_RegisterService();
            fTwiServer.TWI_Light_UVcoef0();
            fTwiServer.TWI_Light_UVcoef1();
            fTwiServer.TWI_Light_UVcoef2();
            fTwiServer.TWI_Light_UVcoef3();
            fTwiServer.TWI_Light_SetParam();
            fTwiServer.TWI_Light_WriteParam();
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
                        return "<html><body>Command 120(reset counter) received, TWI 0x22 sent... </body></html>";
                    //------ cmd 13n = DOn -> ON  ------------------------------------
                    case "?131":
                        Debug.WriteLine("Command 131 received, DO_1 -> ON");
                        Pin_DO1.Write(GpioPinValue.High);
                        return "<html><body>DO_1 -> ON</body></html>";
                    case "?132":
                        Debug.WriteLine("Command 132 received, DO_2 -> ON");
                        Pin_DO2.Write(GpioPinValue.High);
                        return "<html><body>DO_2 -> ON</body></html>";
                    case "?133":
                        Debug.WriteLine("Command 133 received, DO_3 -> ON");
                        Pin_DO3.Write(GpioPinValue.High);
                        return "<html><body>DO_3 -> ON</body></html>";
                    case "?134":
                        Debug.WriteLine("Command 134 received, DO_4 -> ON");
                        Pin_DO4.Write(GpioPinValue.High);
                        return "<html><body>DO_4 -> ON</body></html>";
                    case "?135":
                        Debug.WriteLine("Command 135 received, DO_5 -> ON");
                        Pin_DO5.Write(GpioPinValue.High);
                        return "<html><body>DO_5 -> ON</body></html>";
                    case "?136":
                        Debug.WriteLine("Command 136 received, DO_6 -> ON");
                        Pin_DO6.Write(GpioPinValue.High);
                        return "<html><body>DO_6 -> ON</body></html>";
                    case "?137":
                        Debug.WriteLine("Command 137 received, DO_7 -> ON");
                        Pin_DO7.Write(GpioPinValue.High);
                        return "<html><body>DO_7 -> ON</body></html>";
                    case "?138":
                        Debug.WriteLine("Command 138 received, DO_8 -> ON");
                        Pin_DO8.Write(GpioPinValue.High);
                        return "<html><body>DO_8 -> ON</body></html>";
                    //------ cmd 14n = DOn -> OFF  ------------------------------------
                    case "?141":
                        Debug.WriteLine("Command 141 received, DO_1 -> OFF");
                        Pin_DO1.Write(GpioPinValue.Low);
                        return "<html><body>DO_1 -> OFF</body></html>";
                    case "?142":
                        Debug.WriteLine("Command 142 received, DO_2 -> OFF");
                        Pin_DO2.Write(GpioPinValue.Low);
                        return "<html><body>DO_2 -> OFF</body></html>";
                    case "?143":
                        Debug.WriteLine("Command 143 received, DO_3 -> OFF");
                        Pin_DO3.Write(GpioPinValue.Low);
                        return "<html><body>DO_3 -> OFF</body></html>";
                    case "?144":
                        Debug.WriteLine("Command 144 received, DO_4 -> OFF");
                        Pin_DO4.Write(GpioPinValue.Low);
                        return "<html><body>DO_4 -> OFF</body></html>";
                    case "?145":
                        Debug.WriteLine("Command 145 received, DO_5 -> OFF");
                        Pin_DO5.Write(GpioPinValue.Low);
                        return "<html><body>DO_5 -> OFF</body></html>";
                    case "?146":
                        Debug.WriteLine("Command 146 received, DO_6 -> OFF");
                        Pin_DO6.Write(GpioPinValue.Low);
                        return "<html><body>DO_6 -> OFF</body></html>";
                    case "?147":
                        Debug.WriteLine("Command 147 received, DO_7 -> OFF");
                        Pin_DO7.Write(GpioPinValue.Low);
                        return "<html><body>DO_7 -> OFF</body></html>";
                    case "?148":
                        Debug.WriteLine("Command 148 received, DO_8 -> OFF");
                        Pin_DO8.Write(GpioPinValue.Low);
                        return "<html><body>DO_8 -> OFF</body></html>";
                    //-------- cmd 122 = gather sensor data ----------------------------------
                    case "?122":
                        Debug.WriteLine("Command 122 received, TWI will read");
                        
                        XElement redmineRequestXML =
                            new XElement("SensorData",
                            new XElement("Flow1", fTwiServer.TWI_ATmega_ReadSensor(0)),
                            new XElement("Flow2", fTwiServer.TWI_ATmega_ReadSensor(1)),
                            new XElement("Flow3", fTwiServer.TWI_ATmega_ReadSensor(2)),
                            new XElement("Flow4", fTwiServer.TWI_ATmega_ReadSensor(3)),
                            new XElement("Flow5", fTwiServer.TWI_ATmega_ReadSensor(4)),
                            new XElement("Rain" , fTwiServer.TWI_ATmega_ReadSensor(5)),
                            new XElement("Level", fTwiServer.TWI_ATmega_ReadSensor(6)),
                            new XElement("Press", fTwiServer.TWI_ATmega_ReadSensor(7))
                            );

                        return redmineRequestXML.ToString();
                    // -------- unknown request code -   
                    default:
                        return "<html><body>---FAILURE---</body></html>";
                }
                ;
            };
            fTcpServer.Initialise(8081);
        }
    }

}
