using Aurora.Settings;
using LedCSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aurora.Devices.EVGA
{
    class EVGADevice : Device
    {
        private String devicename = "EVGA";
        private bool isInitialized = false;
        private bool isAttemptingConnection = false;
        //private bool isConnected = false;

        private readonly object action_lock = new object();

        private System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
        private long lastUpdateTime = 0;
        //private VariableRegistry default_registry = null;
        private Thread pipeThread;

        private NamedPipeServerStream pServer;
        private StreamWriter precisionWriter;

        private Color previousColor = Color.Black;

        public bool Initialize()
        {
            lock (action_lock)
            {
                if (!isInitialized && !isAttemptingConnection)
                {
                    isAttemptingConnection = true;
                    pipeThread = new Thread(new ThreadStart(WaitForPrecisionX1));
                    pipeThread.Start();
                    return true;
                }

                return false;
            }
        }

        private void WaitForPrecisionX1()
        {
            pServer = new NamedPipeServerStream("AuroraEVGA", PipeDirection.Out, NamedPipeServerStream.MaxAllowedServerInstances);
            pServer.WaitForConnection();
            precisionWriter = new StreamWriter(pServer);
            isAttemptingConnection = false;
            isInitialized = true;
        }

        public void Shutdown()
        {
            lock (action_lock)
            {
                if (isInitialized || isAttemptingConnection)
                {
                    try
                    {
                        pipeThread?.Abort();
                        precisionWriter?.Dispose();
                        pServer?.Disconnect();
                    }
                    catch {}
                    finally
                    {
                        pServer?.Dispose();

                        pipeThread = null;
                        precisionWriter = null;
                        pServer = null;
                    }
                    this.Reset();
                    isInitialized = false;
                    isAttemptingConnection = false;
                }
            }
        }

        public string GetDeviceDetails()
        {
            if (isAttemptingConnection)
            {
                return devicename + ": Connecting...";
            }
            else if (isInitialized)
            {
                return devicename + ": Connected";
            }
            else
            {
                return devicename + ": Not initialized";
            }
        }

        public string GetDeviceName()
        {
            return devicename;
        }

        public void Reset()
        {
            previousColor = Color.Black;
        }

        public bool Reconnect()
        {
            throw new NotImplementedException();
        }

        public bool IsConnected()
        {
            throw new NotImplementedException();
        }


        private void SendColorToGPU(Color color, bool forced = false)
        {
            if (!previousColor.Equals(color) || forced)
            {
                if (Global.Configuration.allow_peripheral_devices)
                {
                    try
                    {
                        precisionWriter.Write(Convert.ToChar(color.A));
                        precisionWriter.Write(Convert.ToChar(color.R));
                        precisionWriter.Write(Convert.ToChar(color.G));
                        precisionWriter.Write(Convert.ToChar(color.B));
                        precisionWriter.Write(precisionWriter.NewLine);
                        precisionWriter.Flush();

                        previousColor = color;
                    }
                    catch (Exception e)
                    {
                        Global.logger.Error(e);
                        Shutdown();
                    }
                }
            }
        }

        public bool IsInitialized()
        {
            return this.isInitialized;
        }

        public bool UpdateDevice(Dictionary<DeviceKeys, Color> keyColors, DoWorkEventArgs e, bool forced = false)
        {
            try
            {
                SendColorToGPU(keyColors[DeviceKeys.Peripheral_Logo], forced);
                return true;
            }
            catch (Exception exc)
            {
                Global.logger.Error(exc.ToString());
                return false;
            }
        }

        public bool UpdateDevice(DeviceColorComposition colorComposition, DoWorkEventArgs e, bool forced = false)
        {
            watch.Restart();

            bool update_result = UpdateDevice(colorComposition.keyColors, e, forced);

            watch.Stop();
            lastUpdateTime = watch.ElapsedMilliseconds;

            return update_result;
        }

        public bool IsKeyboardConnected()
        {
            return isInitialized;
        }

        public bool IsPeripheralConnected()
        {
            return isInitialized;
        }

        public string GetDeviceUpdatePerformance()
        {
            if (isInitialized)
            {
                return lastUpdateTime + " ms";
            }
            else
            {
                return "";
            }
        }

        public VariableRegistry GetRegisteredVariables()
        {
            return new VariableRegistry();
        }
    }
}
