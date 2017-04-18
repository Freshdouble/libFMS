using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO.Ports;
using libSMP;


namespace libFMS
{
    public class FMS
    {
        private SerialPort port;
        private SMP smp;

        private Mutex mut;
        private Thread portgrabber;

        public static string[] getPortNames()
        {
            return SerialPort.GetPortNames();
        }

        public FMS(string portName, int baudRate, Parity par, int databits, bool useRS = true)
        {
            smp = new SMP(useRS);
            smp.send += Smp_send;
            openConnection(portName, baudRate, par, databits);
        }

        private ushort Smp_send(byte[] buffer, int length)
        {
            port.Write(buffer, 0, length);
            return (ushort)length;
        }

        ~FMS()
        {
            close();
        }

        public void openConnection(string portName, int baudRate, Parity par, int databits)
        {
            if(!connected)
            {
                port = new SerialPort(portName, baudRate, par, databits);
                port.Open();
                mut = new Mutex();
                portgrabber = new Thread(new ThreadStart(this.serialPortGrabber));
                portgrabber.Start();
            }
        }

        public void close()
        {
            if (port != null)
            {
                port.Close();
                port = null;
            }
            mut.Dispose();
            portgrabber.Join();
        }

        public uint send(byte[] data, int length)
        {
            uint ret = smp.SendData(data, (uint)length);
            return ret;
        }

        public int receivedFrames
        {
            get
            {
                mut.WaitOne();
                int ret = smp.NumberMessagesToReceive;
                mut.ReleaseMutex();
                return ret;
            }
        }

        public SMP.Message NextReceivedMessage
        {
            get
            {
                mut.WaitOne();
                SMP.Message ret = smp.NextReceivedMessage;
                mut.ReleaseMutex();
                return ret;
            }
        }

        public byte[] getReceivedMessage()
        {
            return NextReceivedMessage.message;
        }

        public bool connected
        {
            get
            {
                return port != null && port.IsOpen;
            }
        }

        private void receiveBytes(byte[] buffer, int length)
        {
            mut.WaitOne();
            smp.RecieveInBytes(buffer, (uint)length);
            mut.ReleaseMutex();
        }

        private void serialPortGrabber()
        {
            while(connected)
            {
                int bytesToRead;
                if ((bytesToRead = port.BytesToRead) > 0)
                {
                    byte[] buffer = new byte[bytesToRead];
                    port.Read(buffer, 0, bytesToRead);
                    receiveBytes(buffer, buffer.Length);
                }
                Thread.Sleep(100);
            }
        }
    }
}
