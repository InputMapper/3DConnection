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

namespace _3DConnection
{
    [PluginInfo(
        PluginName = "3D Connection device plugin",
        PluginDescription = "",
        PluginID = 0,
        PluginAuthorName = "InputMapper",
        PluginAuthorEmail = "jhebbel@gmail.com",
        PluginAuthorURL = "http://inputmapper.com"
    )]
    public class _3DConnection_Plugin : InputDevicePlugin, pluginSettings
    {
        public SettingGroup settings { get; }

        public _3DConnection_Plugin()
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
                    if (Devices.Where(d => (d as _3DConnection_Device).hDevice.DevicePath == device.DevicePath).Count() == 0)
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
    public class _3DConnection_Device : InputDevice
    {
        internal HidDevice hDevice;

        public _3DConnection_Device(HidDevice device)
        {
            this.hDevice = device;
        }
    }


    public class NotebookSpaceNavigator : _3DConnection_Device
    {
        internal JoyAxis TranslateX = new JoyAxis("Translate X", DataFlowDirection.Input);
        internal JoyAxis TranslateY = new JoyAxis("Translate Y", DataFlowDirection.Input);
        internal JoyAxis TranslateZ = new JoyAxis("Translate Y", DataFlowDirection.Input);

        internal JoyAxis RotateX = new JoyAxis("Rotate X", DataFlowDirection.Input);
        internal JoyAxis RotateY = new JoyAxis("Rotate Y", DataFlowDirection.Input);
        internal JoyAxis RotateZ = new JoyAxis("Rotate Y", DataFlowDirection.Input);

        internal Button Button1 = new Button("Button 1", DataFlowDirection.Input);
        internal Button Button2 = new Button("Button 2", DataFlowDirection.Input);

        private Thread listenerThread;

        public NotebookSpaceNavigator(HidDevice device) : base(device)
        {
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

        private void ListenerThread()
        {
            byte[] report = new byte[7];
            byte[] cReport = new byte[21];
            string[] sReport = new string[21];

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
                Array.Copy(report, 0, cReport, (report[0]-1)*7, report.Length);
                
                sReport = cReport.Select(x => x.ToString().PadRight(3)).ToArray();
                Console.WriteLine(String.Join(" ", sReport));
            }
        }
    }
}
