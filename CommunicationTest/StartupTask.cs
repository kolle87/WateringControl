using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.I2c;
using Windows.Devices.Gpio;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using Windows.Networking.Sockets;
using Windows.Foundation.Diagnostics;
using Windows.ApplicationModel.Background;
using Dropbox.Api;
using Dropbox.Api.Users;
using Dropbox.Api.Files;
using System.Net.Sockets;
using System.Net;

/*
07-09-2017 Michael Kollmeyer
Finalized branche, fully developable
    - cleaned up
    - new timer for logging
    - dropbox integration
    - xml data to service tool via http/tcp
     */

namespace CommunicationTest
{
    public delegate string TcpRequestReceived(string request); // Basic Server which listens for TCP Requests and provides the user with the ability to craft own responses as strings
    public sealed class TcpServer
    {
        private StreamSocketListener fListener;
        private const uint BUFFER_SIZE = 8192;
        public TcpRequestReceived RequestReceived { get; set; }
        public TcpServer() { }
        public async void Initialise(int port)
        {
            this.fListener = new StreamSocketListener();
            await this.fListener.BindServiceNameAsync(port.ToString());
            this.fListener.ConnectionReceived += (sender, args) =>
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
        private I2cConnectionSettings settings1;
        private I2cConnectionSettings settings2;
        private I2cConnectionSettings settings3;
        private I2cController controller;
        private Boolean TWI_ATmega_Available;
        private Boolean TWI_LightSensor_Available;
        private Boolean TWI_Temperature_Available;
        public  StartupTask vParent { get; set; }

        public async void InitTWIAsync()
        {
            vParent.DebugLog("[TWI]","Start initialisation");

            try
            {
                this.controller = await I2cController.GetDefaultAsync(); // Create an I2cDevice with our selected bus controller and I2C settings

                try
                {
                    this.settings1 = new I2cConnectionSettings(0x48) { BusSpeed = I2cBusSpeed.FastMode };   // Temperature Sensor
                    this.TWI_Temperature = this.controller.GetDevice(settings1);
                    if (this.TWI_Temperature == null)
                    {
                        vParent.DebugLog("[Temp]","Device could not be assigned");
                        TWI_Temperature_Available = false;
                    }
                    else
                    {
                        this.TWI_Temperature.ConnectionSettings.SharingMode = I2cSharingMode.Shared;
                        TWI_Temperature_Available = true;
                    }
                }
                catch (Exception e)
                {
                    vParent.DebugLog("[Temp]","FAILURE WHILE INIT");
                    vParent.DebugLog("[Temp]", e.Message);
                }

                try
                {
                    this.settings2 = new I2cConnectionSettings(0x56) { BusSpeed = I2cBusSpeed.FastMode };   // ATmega uController
                    this.TWI_uController = this.controller.GetDevice(settings2);
                    if (this.TWI_uController == null)
                    {
                        vParent.DebugLog("[ATmega]", "Device could not be assigned");
                        TWI_ATmega_Available = false;
                    }
                    else
                    {
                        this.TWI_uController.ConnectionSettings.SharingMode = I2cSharingMode.Shared;
                        TWI_ATmega_Available = true;
                    }
                }
                catch (Exception e)
                {
                    vParent.DebugLog("[ATmega] FAILURE WHILE INIT ({0})", e.Message);
                }

                try
                {
                    this.settings3 = new I2cConnectionSettings(0x60) { BusSpeed = I2cBusSpeed.FastMode };   // Visible Light Sensor
                    this.TWI_VisibleLight = this.controller.GetDevice(settings3);
                    if (this.TWI_VisibleLight == null)
                    {
                        vParent.DebugLog("[Light]","Device could not be assigned");
                        TWI_LightSensor_Available = false;
                    }
                    else
                    {
                        this.TWI_VisibleLight.ConnectionSettings.SharingMode = I2cSharingMode.Shared;
                        TWI_LightSensor_Available = true;
                    }
                }
                catch (Exception e)
                {
                    vParent.DebugLog("[Light]","FAILURE WHILE INIT");
                    vParent.DebugLog("[Light]", e.Message);
                }
            }
            catch (Exception e)
            {
                vParent.DebugLog("[TWI]", "Controller could not be assigned");
                vParent.DebugLog("[TWI]", e.Message);
            }
        }

        // ----- ATmega Commands
        public void TWI_ATmega_ResetCounter()
        {
            if (TWI_ATmega_Available)
            {
                try
                {
                    var vARC = new byte[] { 0x40 };
                    this.TWI_uController.Write(vARC);
                }
                catch (Exception e)
                {
                    vParent.DebugLog("[AVR]","Write Failure");
                    vParent.DebugLog("[AVR]", e.Message);
                }

            }
            else
            {
                vParent.DebugLog("[AVR]", "Slave not available");
            }
        }
        public byte TWI_ATmega_ReadSensor(byte vChn)
        {
            if (TWI_ATmega_Available)
            {
                try
                {
                    var vASr = new byte[] { (byte)(32 + vChn) };    // 0x20 ... 0x28
                    var vASa = new byte[1];
                    this.TWI_uController.Write(vASr);
                    this.TWI_uController.Read(vASa);
                    return vASa[0];
                }
                catch (Exception e)
                {
                    vParent.DebugLog("[AVR]", "Write/Read Failure");
                    vParent.DebugLog("[AVR]", e.Message);
                    return 255;
                }
            }
            else
            {
                vParent.DebugLog("[AVR]", "Slave not available");
                return 255;
            }
        }
        public int TWI_ATmega_ReadPressure()
        {
            if (TWI_ATmega_Available)
            {
                try
                {
                    var vASr = new byte[] { 0x27 };    // 0x20 ... 0x28
                    var vASa = new byte[1];
                    this.TWI_uController.Write(vASr);
                    this.TWI_uController.Read(vASa);
                    var tmp_press = (vASa[0] - 45) * 0.56;
                    return Convert.ToInt16(Math.Round(tmp_press));
                }
                catch (Exception e)
                {
                    vParent.DebugLog("[AVR]", "Write/Read Failure");
                    vParent.DebugLog("[AVR]", e.Message);
                    return 255;
                }
            }
            else
            {
                vParent.DebugLog("[AVR]", "Slave not available");
                return 255;
            }
        }
        public int TWI_ATmega_ReadRain()
        {
            if (TWI_ATmega_Available)
            {
                try
                {
                    var vASr = new byte[] { 0x25 };    // 0x20 ... 0x28
                    var vASa = new byte[1];
                    this.TWI_uController.Write(vASr);
                    this.TWI_uController.Read(vASa);
                    double tmp_rain = (vASa[0] / 255) * 100;
                    return Convert.ToInt16(Math.Round(tmp_rain));
                }
                catch (Exception e)
                {
                    vParent.DebugLog("[AVR]", "Write/Read Failure");
                    vParent.DebugLog("[AVR]", e.Message);
                    return 255;
                }
            }
            else
            {
                vParent.DebugLog("[AVR]", "Slave not available");
                return 255;
            }
        }
        public int TWI_ATmega_ReadLevel()
        {
            if (TWI_ATmega_Available)
            {
                try
                {
                    var vASr = new byte[] { 0x26 };    // 0x20 ... 0x28
                    var vASa = new byte[1];
                    this.TWI_uController.Write(vASr);
                    this.TWI_uController.Read(vASa);
                    return vASa[0];
                }
                catch (Exception e)
                {
                    vParent.DebugLog("[AVR]", "Write/Read Failure");
                    vParent.DebugLog("[AVR]", e.Message);
                    return 255;
                }
            }
            else
            {
                vParent.DebugLog("[AVR]", "Slave not available");
                return 255;
            }
        }

        // ----- Light sensor commands
        public void TWI_Light_Prepare()
        {
            if (TWI_LightSensor_Available)
            {
                try
                {
                    var vReq1 = new byte[1];
                    var vReq2 = new byte[2];
                    var vRes1 = new byte[1];
                    var vRes2 = new byte[2];

                    vReq1[0] = 0x18; vReq2[1] = 0x01; this.TWI_VisibleLight.Write(vReq2);                   // Restart            
                    var t_wait = Task.Run(async delegate { await Task.Delay(1000); }); t_wait.Wait();       // wait 1s            
                    vReq1[0] = 0x02; this.TWI_VisibleLight.WriteRead(vReq1, vRes1);                         // check Seq_ID
                    vParent.DebugLog("[LUM]",String.Format("Seq_ID is {0}", vRes1[0]));

                    vReq2[0] = 0x07; vReq2[1] = 0x17; this.TWI_VisibleLight.Write(vReq2);                   // set Sensor to normal operation mode
                    vReq1[0] = 0x07; this.TWI_VisibleLight.WriteRead(vReq1, vRes1);                         // check if register was written
                    if (vRes1[0] != 0x17) { vParent.DebugLog("[LUM]","0x17 not set"); }

                    vReq2[0] = 0x08; vReq2[1] = 0xFF; this.TWI_VisibleLight.Write(vReq2);                   // set measuring rate (H)
                    vReq1[0] = 0x08; this.TWI_VisibleLight.WriteRead(vReq1, vRes1);                         // check if register was written
                    if (vRes1[0] != 0xFF) { vParent.DebugLog("[LUM]","0x08 not set"); }

                    vReq2[0] = 0x09; vReq2[1] = 0xFF; this.TWI_VisibleLight.Write(vReq2);                   // set measuring rate (L)
                    vReq1[0] = 0x09; this.TWI_VisibleLight.WriteRead(vReq1, vRes1);                         // check if register was written
                    if (vRes1[0] != 0xFF) { vParent.DebugLog("[LUM]","0x09 not set"); }

                    vReq2[0] = 0x13; vReq2[1] = 0x29; this.TWI_VisibleLight.Write(vReq2);                   // set UV coeff 0
                    vReq1[0] = 0x13; this.TWI_VisibleLight.WriteRead(vReq1, vRes1);                         // check if register was written
                    if (vRes1[0] != 0x29) { vParent.DebugLog("[LUM]","0x13 not set"); }

                    vReq2[0] = 0x14; vReq2[1] = 0x89; this.TWI_VisibleLight.Write(vReq2);                   // set UV coeff 1
                    vReq1[0] = 0x14; this.TWI_VisibleLight.WriteRead(vReq1, vRes1);                         // check if register was written
                    if (vRes1[0] != 0x89) { vParent.DebugLog("[LUM]","0x14 not set"); }

                    vReq2[0] = 0x15; vReq2[1] = 0x02; this.TWI_VisibleLight.Write(vReq2);                   // set UV coeff 2
                    vReq1[0] = 0x15; this.TWI_VisibleLight.WriteRead(vReq1, vRes1);                         // check if register was written
                    if (vRes1[0] != 0x02) { vParent.DebugLog("[LUM]","0x15 not set"); }

                    vReq2[0] = 0x16; vReq2[1] = 0x00; this.TWI_VisibleLight.Write(vReq2);                   // set UV coeff 3
                    vReq1[0] = 0x16; this.TWI_VisibleLight.WriteRead(vReq1, vRes1);                         // check if register was written
                    if (vRes1[0] != 0x00) { vParent.DebugLog("[LUM]","0x16 not set"); }

                    vReq2[0] = 0x17; vReq2[1] = 0x20; this.TWI_VisibleLight.Write(vReq2);                   // prep para - VIS high mode
                    vReq2[0] = 0x18; vReq2[1] = 0x00; this.TWI_VisibleLight.Write(vReq2);                   // reset response register
                    t_wait = Task.Run(async delegate { await Task.Delay(100); }); t_wait.Wait();            // wait 100ms
                    vReq1[0] = 0x20; this.TWI_VisibleLight.WriteRead(vReq1, vRes1);                         // read response register
                    if (vRes1[0] != 0x00) { vParent.DebugLog("[LUM]","response clear failed (178)"); }       // check if response reg is empty
                    vReq2[0] = 0x18; vReq2[1] = 0xB2; this.TWI_VisibleLight.Write(vReq2);                   // write command
                    t_wait = Task.Run(async delegate { await Task.Delay(100); }); t_wait.Wait();            // wait 100ms
                    vReq1[0] = 0x20; this.TWI_VisibleLight.WriteRead(vReq1, vRes1);                         // read response register
                    if (vRes1[0] == 0x00) { vParent.DebugLog("[LUM]","command write failed (181)"); }        // check if response reg is empty

                    vReq2[0] = 0x17; vReq2[1] = 0x20; this.TWI_VisibleLight.Write(vReq2);                   // prep para - IR high mode
                    vReq2[0] = 0x18; vReq2[1] = 0x00; this.TWI_VisibleLight.Write(vReq2);                   // reset response register
                    t_wait = Task.Run(async delegate { await Task.Delay(100); }); t_wait.Wait();            // wait 100ms
                    vReq1[0] = 0x20; this.TWI_VisibleLight.WriteRead(vReq1, vRes1);                         // read response register
                    if (vRes1[0] != 0x00) { vParent.DebugLog("[LUM]","response clear failed (186)"); }       // check if response reg is empty
                    vReq2[0] = 0x18; vReq2[1] = 0xBF; this.TWI_VisibleLight.Write(vReq2);                   // write command
                    t_wait = Task.Run(async delegate { await Task.Delay(100); }); t_wait.Wait();            // wait 100ms
                    vReq1[0] = 0x20; this.TWI_VisibleLight.WriteRead(vReq1, vRes1);                         // read response register
                    if (vRes1[0] == 0x00) { vParent.DebugLog("[LUM],","command write failed (189)"); }        // check if response reg is empty	

                    vReq2[0] = 0x17; vReq2[1] = 0xB0; this.TWI_VisibleLight.Write(vReq2);                   // prep para - set measuring channels
                    vReq2[0] = 0x18; vReq2[1] = 0x00; this.TWI_VisibleLight.Write(vReq2);                   // reset response register
                    t_wait = Task.Run(async delegate { await Task.Delay(100); }); t_wait.Wait();            // wait 100ms
                    vReq1[0] = 0x20; this.TWI_VisibleLight.WriteRead(vReq1, vRes1);                         // read response register
                    if (vRes1[0] != 0x00) { vParent.DebugLog("[LUM]","response clear failed (194)"); }       // check if response reg is empty
                    vReq2[0] = 0x18; vReq2[1] = 0xA1; this.TWI_VisibleLight.Write(vReq2);                   // write command
                    t_wait = Task.Run(async delegate { await Task.Delay(100); }); t_wait.Wait();            // wait 100ms
                    vReq1[0] = 0x20; this.TWI_VisibleLight.WriteRead(vReq1, vRes1);                         // read response register
                    if (vRes1[0] == 0x00) { vParent.DebugLog("[LUM]","command write failed (197)"); }        // check if response reg is empty

                    // prep para - start auto mode
                    vReq2[0] = 0x18; vReq2[1] = 0x00; this.TWI_VisibleLight.Write(vReq2);                   // reset response register
                    t_wait = Task.Run(async delegate { await Task.Delay(100); }); t_wait.Wait();            // wait 100ms
                    vReq1[0] = 0x20; this.TWI_VisibleLight.WriteRead(vReq1, vRes1);                         // read response register
                    if (vRes1[0] != 0x00) { vParent.DebugLog("[LUM]","response clear failed (202)"); }       // check if response reg is empty
                    vReq2[0] = 0x18; vReq2[1] = 0x0E; this.TWI_VisibleLight.Write(vReq2);                   // write command
                    t_wait = Task.Run(async delegate { await Task.Delay(100); }); t_wait.Wait();            // wait 100ms
                    vReq1[0] = 0x20; this.TWI_VisibleLight.WriteRead(vReq1, vRes1);                         // read response register
                    if (vRes1[0] == 0x00)
                    {
                        vParent.DebugLog("[LUM]","command write failed (205)");                              // check if response reg is empty
                        vParent.DebugLog("[LUM]","repeating command");
                        vReq2[0] = 0x18; vReq2[1] = 0x00; this.TWI_VisibleLight.Write(vReq2);               // reset response register
                        t_wait = Task.Run(async delegate { await Task.Delay(100); }); t_wait.Wait();        // wait 100ms
                        vReq1[0] = 0x20; this.TWI_VisibleLight.WriteRead(vReq1, vRes1);                     // read response register
                        if (vRes1[0] != 0x00) { vParent.DebugLog("[LUM]","response clear failed (225)"); }   // check if response reg is empty
                        vReq2[0] = 0x18; vReq2[1] = 0x0E; this.TWI_VisibleLight.Write(vReq2);               // write command
                        t_wait = Task.Run(async delegate { await Task.Delay(100); }); t_wait.Wait();        // wait 100ms
                        vReq1[0] = 0x20; this.TWI_VisibleLight.WriteRead(vReq1, vRes1);                     // read response register
                        if (vRes1[0] == 0x00) { vParent.DebugLog("[LUM]","command write failed (229)"); }
                    }
                }
                catch (Exception e)
                {
                    vParent.DebugLog("[LUM]","Write/Read Failure ({0})");
                    vParent.DebugLog("[LUM]", e.Message);
                }
            }
            else
            {
                vParent.DebugLog("[LUM]", "Slave not available");
            }
        }
        public int TWI_Light_ReadVis()
        {
            if (TWI_LightSensor_Available)
            {
                try
                {
                    var vLVLr = new byte[] { 0x22 };
                    var vLVLa = new byte[1];
                    this.TWI_VisibleLight.WriteRead(vLVLr, vLVLa);

                    var vLVHr = new byte[] { 0x23 };
                    var vLVHa = new byte[1];
                    this.TWI_VisibleLight.WriteRead(vLVHr, vLVHa);

                    var vLVC = vLVLa[0] + (vLVHa[0] << 8);
                    return vLVC;
                }
                catch (Exception e)
                {
                    vParent.DebugLog("[LUM]", "Write/Read Failure");
                    vParent.DebugLog("[LUM]", e.Message);
                    return 255;
                }
            }
            else
            {
                vParent.DebugLog("[LUM]", "Slave not available");
                return 255;
            }
        }
        public int TWI_Light_ReadIR()
        {
            if (TWI_LightSensor_Available)
            {
                try
                {
                    var vLILr = new byte[] { 0x24 };
                    var vLILa = new byte[1];
                    this.TWI_VisibleLight.WriteRead(vLILr, vLILa);

                    var vLIHr = new byte[] { 0x25 };
                    var vLIHa = new byte[1];
                    this.TWI_VisibleLight.WriteRead(vLIHr, vLIHa);

                    var vLIC = vLILa[0] + (vLIHa[0] << 8);
                    return vLIC;
                }
                catch (Exception e)
                {
                    vParent.DebugLog("[LUM]", "Write/Read Failure ({0})");
                    vParent.DebugLog("[LUM]", e.Message);
                    return 255;
                }
            }
            else
            {
                vParent.DebugLog("[LUM]", "Slave not available");
                return 255;
            }
        }
        public int TWI_Light_ReadUV()
        {
            if (TWI_LightSensor_Available)
            {
                try
                {
                    var vLULr = new byte[] { 0x2C };
                    var vLULa = new byte[1];
                    this.TWI_VisibleLight.WriteRead(vLULr, vLULa);

                    var vLUHr = new byte[] { 0x2D };
                    var vLUHa = new byte[1];
                    this.TWI_VisibleLight.WriteRead(vLUHr, vLUHa);

                    var vLUC = vLULa[0] + (vLUHa[0] << 8);
                    return vLUC;
                }
                catch (Exception e)
                {
                    vParent.DebugLog("[LUM]", "Write/Read Failure");
                    vParent.DebugLog("[LUM]", e.Message);
                    return 255;
                }
            }
            else
            {
                vParent.DebugLog("[LUM]", "Slave not available");
                return 255;
            }
        }

        // ----- Temperature sensor commands
        public void TWI_Temperature_Start()
        {
            if (TWI_Temperature_Available)
            {
                try
                {
                    var vTSB = new byte[] { 0xEE };
                     this.TWI_Temperature.Write(vTSB);
                }
                catch (Exception e)
                {
                    vParent.DebugLog("[TMP]","Write/Read Failure");
                    vParent.DebugLog("[TMP]", e.Message);
                }
            }
            else
            {
                vParent.DebugLog("[TMP]", "Slave not available");
            }
        }
        public void TWI_Temperature_Config()
        {
            if (TWI_Temperature_Available)
            {
                try
                {
                    var vTSC = new byte[] { 0xAC, 0x02 };
                    this.TWI_Temperature.Write(vTSC);
                }
                catch (Exception e)
                {
                    vParent.DebugLog("[TMP]", "Write/Read Failure");
                    vParent.DebugLog("[TMP]", e.Message);
                }
            }
            else
            {
                vParent.DebugLog("[TMP]", "Slave not available");
            }
        }
        public int TWI_Temperature_Measure()
        {
            if (TWI_Temperature_Available)
            {
                try
                {
                    var vTHr = new byte[] { 0xAA };
                    var vTHa = new byte[2];
                    this.TWI_Temperature.WriteRead(vTHr, vTHa);

                    var vNeg = vTHa[0] & 0x80;
                    var vTmp = vTHa[0] & 0x7F;
                    var vTempCalc = vTmp;

                    if (vNeg == 1) { vTempCalc = -1 * (128 - vTmp); }

                    return vTempCalc;
                }
                catch (Exception e)
                {
                    vParent.DebugLog("[TMP]", "Write/Read Failure");
                    vParent.DebugLog("[TMP]", e.Message);
                    return 255;
                }
            }
            else
            {
                vParent.DebugLog("[TMP]", "Slave not available");
                return 255;
            }
        }
    }
    public sealed class GpioServer
    { 
        private GpioPin Pin_DO1;
        private GpioPin Pin_DO2;
        private GpioPin Pin_DO3;
        private GpioPin Pin_DO4;
        private GpioPin Pin_DO5;
        private GpioPin Pin_DO6;
        private GpioPin Pin_DO7;
        private GpioPin Pin_DO8;
        private GpioController gpio;
        public  StartupTask vParent { get; set; }

        public void InitGPIO()
        {
            vParent.DebugLog("[GIO]","Starting GPIO Service");
            try
            {
                this.gpio = GpioController.GetDefault();
                if (this.gpio != null)
                {
                
                    this.Pin_DO1 = this.gpio.OpenPin(21);
                    this.Pin_DO2 = this.gpio.OpenPin(20);
                    this.Pin_DO3 = this.gpio.OpenPin(16);
                    this.Pin_DO4 = this.gpio.OpenPin(12);
                    this.Pin_DO5 = this.gpio.OpenPin(7);
                    this.Pin_DO6 = this.gpio.OpenPin(8);
                    this.Pin_DO7 = this.gpio.OpenPin(25);
                    this.Pin_DO8 = this.gpio.OpenPin(24);

                    this.Pin_DO1.SetDriveMode(GpioPinDriveMode.Output);
                    this.Pin_DO2.SetDriveMode(GpioPinDriveMode.Output);
                    this.Pin_DO3.SetDriveMode(GpioPinDriveMode.Output);
                    this.Pin_DO4.SetDriveMode(GpioPinDriveMode.Output);
                    this.Pin_DO5.SetDriveMode(GpioPinDriveMode.Output);
                    this.Pin_DO6.SetDriveMode(GpioPinDriveMode.Output);
                    this.Pin_DO7.SetDriveMode(GpioPinDriveMode.Output);
                    this.Pin_DO8.SetDriveMode(GpioPinDriveMode.Output);

                    this.Pin_DO1.Write(GpioPinValue.Low);
                    this.Pin_DO2.Write(GpioPinValue.Low);
                    this.Pin_DO3.Write(GpioPinValue.Low);
                    this.Pin_DO4.Write(GpioPinValue.Low);
                    this.Pin_DO5.Write(GpioPinValue.Low);
                    this.Pin_DO6.Write(GpioPinValue.Low);
                    this.Pin_DO7.Write(GpioPinValue.Low);
                    this.Pin_DO8.Write(GpioPinValue.Low);                
                }
                else
                {
                    vParent.DebugLog("[GIO]","Controller not available");
                }
            }
            catch (Exception e)
            {
                vParent.DebugLog("[GIO]","FAILURE WHILE INIT ({0})");
                vParent.DebugLog("[GIO]", e.Message);
            }
        }
        public bool GetPinState(byte vPin)
        {
            try
            {
                this.gpio = GpioController.GetDefault();
                if (this.gpio != null)
                {
                    switch (vPin)
                    {
                        case 1: if (this.Pin_DO1.Read() == GpioPinValue.High) { return true; } else { return false; }
                        case 2: if (this.Pin_DO2.Read() == GpioPinValue.High) { return true; } else { return false; }
                        case 3: if (this.Pin_DO3.Read() == GpioPinValue.High) { return true; } else { return false; }
                        case 4: if (this.Pin_DO4.Read() == GpioPinValue.High) { return true; } else { return false; }
                        case 5: if (this.Pin_DO5.Read() == GpioPinValue.High) { return true; } else { return false; }
                        case 6: if (this.Pin_DO6.Read() == GpioPinValue.High) { return true; } else { return false; }
                        case 7: if (this.Pin_DO7.Read() == GpioPinValue.High) { return true; } else { return false; }
                        case 8: if (this.Pin_DO8.Read() == GpioPinValue.High) { return true; } else { return false; }
                        default: return false;
                    }
                }
                else
                {
                    vParent.DebugLog("[GPIO]","Controller not available");
                    return false;
                }
            }
            catch (Exception e)
            {
                vParent.DebugLog("[GPIO]","Write/Read Failure");
                vParent.DebugLog("[GPIO]", e.Message);
                return false;
            }

        }
        public void SetPinState(byte vPin, bool vValue)
        {
            try
            {
                this.gpio = GpioController.GetDefault();
                if (this.gpio != null)
                {
                    switch (vPin)
                    {
                        case 1: if (vValue) { this.Pin_DO1.Write(GpioPinValue.High); break; } else { this.Pin_DO1.Write(GpioPinValue.Low); break; }
                        case 2: if (vValue) { this.Pin_DO2.Write(GpioPinValue.High); break; } else { this.Pin_DO2.Write(GpioPinValue.Low); break; }
                        case 3: if (vValue) { this.Pin_DO3.Write(GpioPinValue.High); break; } else { this.Pin_DO3.Write(GpioPinValue.Low); break; }
                        case 4: if (vValue) { this.Pin_DO4.Write(GpioPinValue.High); break; } else { this.Pin_DO4.Write(GpioPinValue.Low); break; }
                        case 5: if (vValue) { this.Pin_DO5.Write(GpioPinValue.High); break; } else { this.Pin_DO5.Write(GpioPinValue.Low); break; }
                        case 6: if (vValue) { this.Pin_DO6.Write(GpioPinValue.High); break; } else { this.Pin_DO6.Write(GpioPinValue.Low); break; }
                        case 7: if (vValue) { this.Pin_DO7.Write(GpioPinValue.High); break; } else { this.Pin_DO7.Write(GpioPinValue.Low); break; }
                        case 8: if (vValue) { this.Pin_DO8.Write(GpioPinValue.High); break; } else { this.Pin_DO8.Write(GpioPinValue.Low); break; }
                        default: { break; }
                    }
                }
                else
                {
                    vParent.DebugLog("[GPIO]","Controller not available");
                }
            }
            catch (Exception e)
            {
                vParent.DebugLog("[GPIO]","Write/Read Failure");
                vParent.DebugLog("[GPIO]", e.Message);
            }

        }
    }

    public sealed class StartupTask : IBackgroundTask
    {        
        List<string> LogDataList = new List<string>();
        List<string> AppLogList  = new List<string>();

        private BackgroundTaskDeferral fDef;
        private TcpServer fTcpServer;
        private TwiServer fTwiServer;
        private GpioServer fGpioServer;
        private DropboxClient fDropbox;
        private Timer LogTimer;

        UdpClient TxUDPclient = new UdpClient();
        IPEndPoint IPconf = new IPEndPoint(IPAddress.Broadcast, 12300);

        public void DebugLog(string vMsg, string vDetail)
        {
            AppLogList.Add(DateTime.Now.ToString("HH:mm:ss.fff") + ";" + vMsg + ";" + vDetail);
            Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff - ") + vMsg + " " + vDetail);
        }
        private void LogTimer_Tick(Object stateInfo)
        {
            try
            {
            #region variables
                var v_LightIR  = fTwiServer.TWI_Light_ReadIR();        // int -> 2byte
                var v_LightVS  = fTwiServer.TWI_Light_ReadVis();       // int -> 2byte
                var v_LightUV  = fTwiServer.TWI_Light_ReadUV();        // int -> 2byte
                var v_Tempera  = fTwiServer.TWI_Temperature_Measure(); // int -> 2byte
                var v_Level    = fTwiServer.TWI_ATmega_ReadLevel();    // int -> 2byte
                var v_Rain     = fTwiServer.TWI_ATmega_ReadRain();     // int -> 2byte
                var v_Pressure = fTwiServer.TWI_ATmega_ReadPressure(); // int -> 2byte
                var v_Flow1    = fTwiServer.TWI_ATmega_ReadSensor(0);  // byte
                var v_Flow2    = fTwiServer.TWI_ATmega_ReadSensor(1);  // byte
                var v_Flow3    = fTwiServer.TWI_ATmega_ReadSensor(2);  // byte
                var v_Flow4    = fTwiServer.TWI_ATmega_ReadSensor(3);  // byte
                var v_Flow5    = fTwiServer.TWI_ATmega_ReadSensor(4);  // byte
                var v_Output1  = fGpioServer.GetPinState(1);           // byte
                var v_Output2  = fGpioServer.GetPinState(2);           // byte
                var v_Output3  = fGpioServer.GetPinState(3);           // byte
                var v_Output4  = fGpioServer.GetPinState(4);           // byte
                var v_Output5  = fGpioServer.GetPinState(5);           // byte
                var v_Output6  = fGpioServer.GetPinState(6);           // byte
                var v_Output7  = fGpioServer.GetPinState(7);           // byte
                var v_Output8  = fGpioServer.GetPinState(8);           // byte
                #endregion variables
            #region LogDataList
                LogDataList.Add(Convert.ToString(DateTime.Now) + ";" +
                                Convert.ToString(v_LightIR)    + ";" +
                                Convert.ToString(v_LightVS)    + ";" +
                                Convert.ToString(v_LightUV)    + ";" +
                                Convert.ToString(v_Tempera)    + ";" +
                                Convert.ToString(v_Level)      + ";" +
                                Convert.ToString(v_Rain)       + ";" +
                                Convert.ToString(v_Pressure)   + ";" +
                                Convert.ToString(v_Flow1)      + ";" +
                                Convert.ToString(v_Flow2)      + ";" +
                                Convert.ToString(v_Flow3)      + ";" +
                                Convert.ToString(v_Flow4)      + ";" +
                                Convert.ToString(v_Flow5)      + ";" +
                                Convert.ToString(v_Output1)    + ";" +
                                Convert.ToString(v_Output2)    + ";" +
                                Convert.ToString(v_Output3)    + ";" +
                                Convert.ToString(v_Output4)    + ";" +
                                Convert.ToString(v_Output5)    + ";" +
                                Convert.ToString(v_Output6)    + ";" +
                                Convert.ToString(v_Output7)    + ";" +
                                Convert.ToString(v_Output8)
                                );
                #endregion LogDataList
            #region UDPstream
                List<byte> vDataStream = new List<byte>();
                vDataStream.AddRange(BitConverter.GetBytes(v_LightIR));
                vDataStream.AddRange(BitConverter.GetBytes(v_LightVS));
                vDataStream.AddRange(BitConverter.GetBytes(v_LightUV));
                vDataStream.AddRange(BitConverter.GetBytes(v_Tempera));
                vDataStream.AddRange(BitConverter.GetBytes(v_Level));
                vDataStream.AddRange(BitConverter.GetBytes(v_Rain));
                vDataStream.AddRange(BitConverter.GetBytes(v_Pressure));
                vDataStream.AddRange(BitConverter.GetBytes(v_Flow1));
                vDataStream.AddRange(BitConverter.GetBytes(v_Flow2));
                vDataStream.AddRange(BitConverter.GetBytes(v_Flow3));
                vDataStream.AddRange(BitConverter.GetBytes(v_Flow4));
                vDataStream.AddRange(BitConverter.GetBytes(v_Flow5));
                vDataStream.AddRange(BitConverter.GetBytes(v_Output1));
                vDataStream.AddRange(BitConverter.GetBytes(v_Output2));
                vDataStream.AddRange(BitConverter.GetBytes(v_Output3));
                vDataStream.AddRange(BitConverter.GetBytes(v_Output4));
                vDataStream.AddRange(BitConverter.GetBytes(v_Output5));
                vDataStream.AddRange(BitConverter.GetBytes(v_Output6));
                vDataStream.AddRange(BitConverter.GetBytes(v_Output7));
                vDataStream.AddRange(BitConverter.GetBytes(v_Output8));
                #endregion UDPstream
                
                var UDPsend = Task.Run(async delegate { await TxUDPclient.SendAsync(vDataStream.ToArray(), vDataStream.ToArray().Length, IPconf); });
                UDPsend.Wait();

                if (DateTime.Now.Hour == 23 && DateTime.Now.Minute == 59 && DateTime.Now.Second == 59) { SaveLogList(true); SaveDebugList(); }
            }
            catch (Exception e)
            {
                this.DebugLog("[LOG]","unable to write in log");
                this.DebugLog("[LOG]", e.Message);
            }
        }
        private void PrepareLogList()
        {
            try
            {
                LogDataList.Clear();
                LogDataList.Add("DateTime;" +
                            "LightIR;" +
                            "LightVis;" +
                            "LightUV;" +
                            "Temperature;" +
                            "Level;" +
                            "Rain;" +
                            "Pressure;" +
                            "Flow1;" +
                            "Flow2;" +
                            "Flow3;" +
                            "Flow4;" +
                            "Flow5;" +
                            "DO1;" +
                            "DO2;" +
                            "DO3;" +
                            "DO4;" +
                            "DO5;" +
                            "DO6;" +
                            "DO7;" +
                            "DO8;");
            }
            catch (Exception e)
            {
                this.DebugLog("[Log]","unable to prepare log");
                this.DebugLog("[Log]", e.Message);
            }
        }
        private void SaveLogList(bool clear)
        {
            try
            {
                this.DebugLog("[Log]","Start uploading LogDataList");
                String vFilename = DateTime.Now.ToString("yyyy_MM_dd") + " - LogFile.csv";
                Monitor.Enter(LogDataList);
                var t_DbxUpload = Task.Run(async delegate { await UploadFile(fDropbox, "/LogFiles", vFilename, string.Join("\n",LogDataList.ToArray())); });
                t_DbxUpload.Wait();
                if (clear) { PrepareLogList(); }
                Monitor.Exit(LogDataList);
            }
            catch (Exception e)
            {
                this.DebugLog("[Log]","Failed to save LogDataList");
                this.DebugLog("[Log]", e.Message);
            }
        }
        private void SaveDebugList()
        {
            try
            {
                this.DebugLog("[Log]", "Start uploading DebugDataList");
                String vFilename = DateTime.Now.ToString("yyyy_MM_dd") + " - DebugFile.csv";
                Monitor.Enter(AppLogList);
                var t_DbxUpload = Task.Run(async delegate { await UploadFile(fDropbox, "/LogFiles", vFilename, string.Join("\n", AppLogList.ToArray())); });
                t_DbxUpload.Wait();
                AppLogList.Clear();
                AppLogList.Add("TimeStamp;Message;Detail");
                Monitor.Exit(AppLogList);
            }
            catch (DropboxException e)
            {
                this.DebugLog("[Log]", "Failed to save DebugDataList");
                this.DebugLog("[Log]", e.Message);
            }
        }
        
        private async Task ShowCurrentAccount(DropboxClient dbx)
        {
            var AccInf = await dbx.Users.GetCurrentAccountAsync();
            this.DebugLog("[DBX]", String.Format("{0} - {1}", AccInf.Name.DisplayName, AccInf.Email));
        }
        private async Task UploadFile(DropboxClient dbx, string vFolder, string vFile, string vContent)
        {
            using (var mem = new MemoryStream(Encoding.UTF8.GetBytes(vContent)))
            {
                var updated = await dbx.Files.UploadAsync(vFolder + "/" + vFile, WriteMode.Add.Instance, true, null, true, body: mem);
                this.DebugLog("[DBX]",String.Format("Saved (DropBox){0}/{1} [rev {2}]", vFolder, vFile, updated.Rev));
            }
        }

        public void Run(IBackgroundTaskInstance taskInstance)
        {                      
            fDef = taskInstance.GetDeferral();          // get deferral to keep running

            AppLogList.Add("TimeStamp;Message;Detail");

            fTwiServer = new TwiServer();
            fTwiServer.vParent = this;
            fTwiServer.InitTWIAsync();

            fTwiServer.TWI_Temperature_Config();
            fTwiServer.TWI_Temperature_Start();

            // ----- INIT Light Sensor --------
            fTwiServer.TWI_Light_Prepare();
            // --------------------------------

            fGpioServer = new GpioServer();
            fGpioServer.vParent = this;
            fGpioServer.InitGPIO();

            PrepareLogList();

            this.DebugLog("[DBX]","Connect account");
            fDropbox = new DropboxClient("IQ3QuN4TjrwAAAAAAAAiil1HquoQVD2wBsu2T9z0QuzuZUSEh4tqnHfGPBKBubqe");
            var t_DpxAcc = Task.Run(async delegate { await ShowCurrentAccount(fDropbox); });
            t_DpxAcc.Wait();

            this.DebugLog("[APP]","Starting Log timer");
            this.LogTimer     = new Timer(this.LogTimer_Tick,     null,    1000,     1000);

            this.DebugLog("[APP]","Starting TCP Server");
            fTcpServer = new TcpServer();
            fTcpServer.RequestReceived = (request) =>
            {
                var requestLines = request.ToString().Split(' ');
                var url = requestLines.Length > 1 ? requestLines[1] : string.Empty;
                var uri = new Uri("http://localhost" + url);
                switch (uri.Query)
                {
                    case "?120":
                        this.DebugLog("[NET]","Command 120 received, ATmega counter reset");
                        fTwiServer.TWI_ATmega_ResetCounter();
                        return "CMD_120: counter reset";
                    //------ cmd 13n = DOn -> ON  ------------------------------------
                    case "?130":
                        this.DebugLog("[NET]", "Command 130 received, ALL VALVES -> OPEN");
                        fGpioServer.SetPinState(3, true);
                        fGpioServer.SetPinState(4, true);
                        fGpioServer.SetPinState(5, true);
                        fGpioServer.SetPinState(6, true);
                        fGpioServer.SetPinState(7, true);
                        return "CMD_130: ALL VALVES -> OPEN";
                    case "?131":
                        this.DebugLog("[NET]", "Command 131 received, DO_1 -> ON");
                        fGpioServer.SetPinState(1, true);
                        return "CMD_131: DO_1 -> ON";
                    case "?132":
                        this.DebugLog("[NET]", "Command 132 received, DO_2 -> ON");
                        fGpioServer.SetPinState(2, true);
                        return "CMD_132: DO_2 -> ON";
                    case "?133":
                        this.DebugLog("[NET]", "Command 133 received, DO_3 -> ON");
                        fGpioServer.SetPinState(3, true);
                        return "CMD_133: DO_3 -> ON";
                    case "?134":
                        this.DebugLog("[NET]", "Command 134 received, DO_4 -> ON");
                        fGpioServer.SetPinState(4, true);
                        return "CMD_134: DO_4 -> ON";
                    case "?135":
                        this.DebugLog("[NET]", "Command 135 received, DO_5 -> ON");
                        fGpioServer.SetPinState(5, true);
                        return "CMD_135: DO_5 -> ON";
                    case "?136":
                        this.DebugLog("[NET]", "Command 136 received, DO_6 -> ON");
                        fGpioServer.SetPinState(6, true);
                        return "CMD_136: DO_6 -> ON";
                    case "?137":
                        this.DebugLog("[NET]", "Command 137 received, DO_7 -> ON");
                        fGpioServer.SetPinState(7, true);
                        return "CMD_137: DO_7 -> ON";
                    case "?138":
                        this.DebugLog("[NET]", "Command 138 received, DO_8 -> ON");
                        fGpioServer.SetPinState(8, true);
                        return "CMD_138: DO_8 -> ON";
                    //------ cmd 14n = DOn -> OFF  ------------------------------------
                    case "?140":
                        this.DebugLog("[NET]", "Command 140 received, ALL -> OFF");
                        fGpioServer.SetPinState(1, false);
                        fGpioServer.SetPinState(2, false);
                        fGpioServer.SetPinState(3, false);
                        fGpioServer.SetPinState(4, false);
                        fGpioServer.SetPinState(5, false);
                        fGpioServer.SetPinState(6, false);
                        fGpioServer.SetPinState(7, false);
                        fGpioServer.SetPinState(8, false);
                        return "CMD_140: ALL -> OFF";
                    case "?141":
                        this.DebugLog("[NET]", "Command 141 received, DO_1 -> OFF");
                        fGpioServer.SetPinState(1, false);
                        return "CMD_141: DO_1 -> OFF";
                    case "?142":
                        this.DebugLog("[NET]", "Command 142 received, DO_2 -> OFF");
                        fGpioServer.SetPinState(2, false);
                        return "CMD_142: DO_2 -> OFF";
                    case "?143":
                        this.DebugLog("[NET]", "Command 143 received, DO_3 -> OFF");
                        fGpioServer.SetPinState(3, false);
                        return "CMD_143: DO_3 -> OFF";
                    case "?144":
                        this.DebugLog("[NET]", "Command 144 received, DO_4 -> OFF");
                        fGpioServer.SetPinState(4, false);
                        return "CMD_144: DO_4 -> OFF";
                    case "?145":
                        this.DebugLog("[NET]", "Command 145 received, DO_5 -> OFF");
                        fGpioServer.SetPinState(5, false);
                        return "CMD_145: DO_5 -> OFF";
                    case "?146":
                        this.DebugLog("[NET]", "Command 146 received, DO_6 -> OFF");
                        fGpioServer.SetPinState(6, false);
                        return "CMD_146: DO_6 -> OFF";
                    case "?147":
                        this.DebugLog("[NET]", "Command 147 received, DO_7 -> OFF");
                        fGpioServer.SetPinState(7, false);
                        return "CMD_147: DO_7 -> OFF";
                    case "?148":
                        this.DebugLog("[NET]", "Command 148 received, DO_8 -> OFF");
                        fGpioServer.SetPinState(8, false);
                        return "CMD_148: DO_8 -> OFF";
                    //-------- cmd 121 = gather enviromental data ----------------------------------
                    case "?121":
                        this.DebugLog("[NET]", "Command 121 received, environmental data requested");
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
                        this.DebugLog("[NET]", "Command 122 received, sensors data requested");
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
                        this.DebugLog("[NET]", "Command 123 received, GPIO states reaquested");
                        // 
                        XElement GpioDataXML =
                            new XElement("GPIO_States",
                            new XElement("DO1", fGpioServer.GetPinState(1)),
                            new XElement("DO2", fGpioServer.GetPinState(2)),
                            new XElement("DO3", fGpioServer.GetPinState(3)),
                            new XElement("DO4", fGpioServer.GetPinState(4)),
                            new XElement("DO5", fGpioServer.GetPinState(5)),
                            new XElement("DO6", fGpioServer.GetPinState(6)),
                            new XElement("DO7", fGpioServer.GetPinState(7)),
                            new XElement("DO8", fGpioServer.GetPinState(8))                                                      
                            );

                        return GpioDataXML.ToString();
                    
                    case "?190":
                        this.DebugLog("[NET]", "Command 190 received, Logging data reaquested");
                        return LogDataList.ToString();
                    // -------- unknown request code - 
                    default:
                        return "FAILURE_UNKNOWN";
                }
                ;
            };
            fTcpServer.Initialise(8081);
            this.DebugLog("[APP]","Initialization finished");
        }
    }

}
