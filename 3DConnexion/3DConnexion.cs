using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ODIF;
using HID_Devices;
using System.Windows.Media;
using System.Threading;
using System.Diagnostics;

namespace _3DConnexion
{
    [PluginInfo(
        PluginName = "3D Connexion device plugin",
        PluginDescription = "",
        PluginID = 0,
        PluginAuthorName = "InputMapper",
        PluginAuthorEmail = "jhebbel@gmail.com",
        PluginAuthorURL = "http://inputmapper.com"
    )]
    public class _3DConnexion_Plugin : InputDevicePlugin, pluginSettings
    {
        public SettingGroup settings { get; }

        public _3DConnexion_Plugin()
        {
            settings = new SettingGroup("General Settings", "");

            Setting connectExclusively = new Setting("Connect Exclusively", "", SettingControl.Checkbox, SettingType.Bool, true);
            connectExclusively.descriptionVisibility = DescriptionVisibility.SubText;
            settings.settings.Add(connectExclusively);

            settings.loadSettings();

            CheckForDevices();
        }

        public void CheckForDevices(object callback = null)
        {
            lock (Devices)
            {
                IEnumerable<HidDevice> foundDevices = HidDevices.Enumerate(0x046D, 0xC628);

                foreach (HidDevice device in foundDevices)
                    if (Devices.Where(d => (d as _3DConnexion_Device).hDevice.DevicePath == device.DevicePath).Count() == 0)
                    {
                        if (device.IsConnected)
                        {
                            if (settings.getSetting("Connect Exclusively"))
                            {
                                device.OpenDevice(true);
                                if (!device.IsOpen)
                                {
                                    ErrorHandling.LogWarning(this, new Warning("Could not connect to #DConnection device exclusively. Another application may be open and accessing the controller."));
                                    device.OpenDevice(false);
                                }
                            }
                            else
                            {
                                device.OpenDevice(false);
                            }

                            Stopwatch sw = new Stopwatch();
                            sw.Start();
                            while (!device.IsOpen && sw.Elapsed.Seconds <= 5)
                            {
                                Thread.Sleep(500);
                            }
                            sw.Stop();

                            NotebookSpaceNavigator Device = new NotebookSpaceNavigator(device);
                            Devices.Add(Device);
                        }
                    }

            }
        }
    }
    public class _3DConnexion_Device : InputDevice
    {
        internal HidDevice hDevice;
        
        public _3DConnexion_Device(HidDevice device)
        {
            this.hDevice = device;
        }
    }


    public class NotebookSpaceNavigator : _3DConnexion_Device
    {

        internal JoyAxis TranslateX = new JoyAxis("Translate X", DataFlowDirection.Input);
        internal JoyAxis TranslateY = new JoyAxis("Translate Y", DataFlowDirection.Input);
        internal JoyAxis TranslateZ = new JoyAxis("Translate Z", DataFlowDirection.Input);

        internal JoyAxis RotateX = new JoyAxis("Rotate X", DataFlowDirection.Input);
        internal JoyAxis RotateY = new JoyAxis("Rotate Y", DataFlowDirection.Input);
        internal JoyAxis RotateZ = new JoyAxis("Rotate Z", DataFlowDirection.Input);

        internal Button Button1 = new Button("Button 1", DataFlowDirection.Input);
        internal Button Button2 = new Button("Button 2", DataFlowDirection.Input);

        //internal InputChannelTypes.JoyAxis TranslateX = new InputChannelTypes.JoyAxis("Translate X");
        //internal InputChannelTypes.JoyAxis TranslateY = new InputChannelTypes.JoyAxis("Translate Y");
        //internal InputChannelTypes.JoyAxis TranslateZ = new InputChannelTypes.JoyAxis("Translate Z");

        //internal InputChannelTypes.JoyAxis RotateX = new InputChannelTypes.JoyAxis("Rotate X");
        //internal InputChannelTypes.JoyAxis RotateY = new InputChannelTypes.JoyAxis("Rotate Y");
        //internal InputChannelTypes.JoyAxis RotateZ = new InputChannelTypes.JoyAxis("Rotate Z");

        //internal InputChannelTypes.Button Button1 = new InputChannelTypes.Button("Button 1");
        //internal InputChannelTypes.Button Button2 = new InputChannelTypes.Button("Button 2");

        private Thread listenerThread;

        public NotebookSpaceNavigator(HidDevice device) : base(device)
        {
            this.DeviceName = "Notebook Space Navigator";

            Channels.Add(TranslateX);
            Channels.Add(TranslateY);
            Channels.Add(TranslateZ);

            Channels.Add(RotateX);
            Channels.Add(RotateY);
            Channels.Add(RotateZ);

            Channels.Add(Button1);
            Channels.Add(Button2);

            listenerThread = new Thread(ListenerThread);
            listenerThread.Start();
        }

        protected override void Dispose(bool disposing)
        {
            listenerThread?.Abort();
            this.hDevice.CancelIO();
            base.Dispose(disposing);
        }

        private void ListenerThread()
        {
            byte[] report = new byte[7];

            bool isResponding = true;
            EventWaitHandle MyEventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);

            // Packet Definition:
            // 1st byte is packet type, 1 = translation, 2 = rotation, 3 = buttons
            // Packet type 1:
            // b[1] = X translation (0-255 | 0 - 255)
            // b[2] = X translation dir and multiplier. (254,255 L|R 0,1)
            // b[3] = Y translation (0-255 | 0 - 255)
            // b[4] = Y translation dir and multiplier. (254,255 F|B 0,1)
            // b[5] = Z translation (0-255 | 0 - 255)
            // b[6] = Z translation dir and multiplier. (254,255 U|D 0,1)
            // Packet type 2:
            // b[1] = X rotation (0-255 | 0 - 255)
            // b[2] = X rotation dir and multiplier. (254,255 L|R 0,1)
            // b[3] = Y rotation (0-255 | 0 - 255)
            // b[4] = Y rotation dir and multiplier. (254,255 F|B 0,1)
            // b[5] = Z rotation (0-255 | 0 - 255)
            // b[6] = Z rotation dir and multiplier. (254,255 U|D 0,1)
            // Packet type 3:
            // b[1] = Buttons, Left >> 1, Right >> 2

            while (isResponding && !Global.IsShuttingDown)
            {
                MyEventWaitHandle.WaitOne(1);
                HidDevice.ReadStatus readStatus = hDevice.ReadFile(report);

                if (readStatus != HidDevice.ReadStatus.Success)
                {
                    isResponding = false;
                }

                if (report[0] == 1) // Translate
                {
                    int Xval, Yval, Zval;
                    double outXval, outYval, outZval;

                    if (report[2] >= 254)
                    {
                        Xval = report[1].flip();
                        if (report[2] == 254) Xval += 256;
                        Xval *= -1;
                    } else
                    {
                        Xval = report[1];
                        if (report[2] == 1) Xval += 255;
                    }
                    outXval = Xval / 349d;

                    if (report[4] >= 254)
                    {
                        Yval = report[3].flip();
                        if (report[4] == 254) Yval += 256;
                        Yval *= -1;
                    }
                    else
                    {
                        Yval = report[3];
                        if (report[4] == 1) Yval += 255;
                    }
                    outYval = Yval / 349d;

                    if (report[6] >= 254)
                    {
                        Zval = report[5].flip();
                        if (report[6] == 254) Zval += 256;
                        Zval *= -1;
                    }
                    else
                    {
                        Zval = report[5];
                        if (report[6] == 1) Zval += 255;
                    }
                    outZval = Zval / 349d;

                    TranslateX.Value = outXval;
                    TranslateY.Value = outYval;
                    TranslateZ.Value = outZval;
                }
                else if (report[0] == 2) // Rotate
                {
                    int Xval, Yval, Zval;
                    double outXval, outYval, outZval;

                    if (report[2] >= 254)
                    {
                        Xval = report[1].flip();
                        if (report[2] == 254) Xval += 256;
                        Xval *= -1;
                    }
                    else
                    {
                        Xval = report[1];
                        if (report[2] == 1) Xval += 255;
                    }
                    outXval = Xval / 349d;

                    if (report[4] >= 254)
                    {
                        Yval = report[3].flip();
                        if (report[4] == 254) Yval += 256;
                        Yval *= -1;
                    }
                    else
                    {
                        Yval = report[3];
                        if (report[4] == 1) Yval += 255;
                    }
                    outYval = Yval / 349d;

                    if (report[6] >= 254)
                    {
                        Zval = report[5].flip();
                        if (report[6] == 254) Zval += 256;
                        Zval *= -1;
                    }
                    else
                    {
                        Zval = report[5];
                        if (report[6] == 1) Zval += 255;
                    }
                    outZval = Zval / 349d;

                    RotateX.Value = outXval;
                    RotateY.Value = outYval;
                    RotateZ.Value = outZval;
                }
                else if (report[0] == 3) // Buttons
                {
                    Button1.Value = ((byte)report[1] & (1 << 0)) != 0;
                    Button2.Value = ((byte)report[1] & (1 << 1)) != 0;
                }
            }
        }
    }
    public static class extensions
    {
        public static byte flip(this byte inByte)
        {
            return (byte)(255 - inByte);
        }
    }
}
