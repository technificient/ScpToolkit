﻿using System;
using System.ComponentModel;

namespace ScpControl
{
    public partial class UsbDevice : ScpDevice, IDsDevice
    {
        protected byte m_BatteryStatus = 0;
        protected byte[] m_Buffer = new byte[64];
        protected byte m_CableStatus = 0;
        protected byte m_ControllerId;
        protected string m_Instance = string.Empty, m_Mac = string.Empty;
        protected bool m_IsDisconnect;
        protected DateTime m_Last = DateTime.Now, m_Tick = DateTime.Now, m_Disconnect = DateTime.Now;
        protected byte[] m_Local = new byte[6];
        protected byte[] m_Master = new byte[6];
        protected byte m_Model = 0;
        protected uint m_Packet;
        protected byte m_PlugStatus = 0;
        protected bool m_Publish = false;
        protected ReportEventArgs m_ReportArgs = new ReportEventArgs();
        protected DsState m_State = DsState.Disconnected;

        protected UsbDevice(string Guid) : base(Guid)
        {
            InitializeComponent();
        }

        public UsbDevice()
        {
            InitializeComponent();
        }

        public UsbDevice(IContainer container)
        {
            container.Add(this);

            InitializeComponent();
        }

        public virtual bool IsShutdown
        {
            get { return m_IsDisconnect; }
            set { m_IsDisconnect = value; }
        }

        public virtual DsModel Model
        {
            get { return (DsModel) m_Model; }
        }

        public virtual DsPadId PadId
        {
            get { return (DsPadId) m_ControllerId; }
            set
            {
                m_ControllerId = (byte) value;

                m_ReportArgs.Pad = PadId;
            }
        }

        public virtual DsConnection Connection
        {
            get { return DsConnection.USB; }
        }

        public virtual DsState State
        {
            get { return m_State; }
        }

        public virtual DsBattery Battery
        {
            get { return (DsBattery) m_BatteryStatus; }
        }

        public virtual byte[] BD_Address
        {
            get { return m_Local; }
        }

        public virtual string Local
        {
            get { return m_Mac; }
        }

        public virtual string Remote
        {
            get
            {
                return string.Format("{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}", m_Master[0], m_Master[1], m_Master[2],
                    m_Master[3], m_Master[4], m_Master[5]);
            }
        }

        public override bool Start()
        {
            if (IsActive)
            {
                Array.Copy(m_Local, 0, m_ReportArgs.Report, (int) DsOffset.Address, m_Local.Length);

                m_ReportArgs.Report[(int) DsOffset.Connection] = (byte) Connection;
                m_ReportArgs.Report[(int) DsOffset.Model] = (byte) Model;

                m_State = DsState.Connected;
                m_Packet = 0;

                HID_Worker.RunWorkerAsync();
                tmUpdate.Enabled = true;

                Rumble(0, 0);
                Log.DebugFormat("-- Started Device Instance [{0}] Local [{1}] Remote [{2}]", m_Instance, Local, Remote);
            }

            return State == DsState.Connected;
        }

        public virtual bool Rumble(byte large, byte small)
        {
            return false;
        }

        public virtual bool Pair(byte[] master)
        {
            return false;
        }

        public virtual bool Disconnect()
        {
            return true;
        }

        public event EventHandler<ReportEventArgs> Report;

        protected virtual void Publish()
        {
            m_ReportArgs.Report[0] = m_ControllerId;
            m_ReportArgs.Report[1] = (byte) m_State;

            if (Report != null) Report(this, m_ReportArgs);
        }

        protected virtual void Process(DateTime now)
        {
        }

        protected virtual void Parse(byte[] Report)
        {
        }

        protected virtual bool Shutdown()
        {
            Stop();

            return RestartDevice(m_Instance);
        }

        public override bool Stop()
        {
            if (IsActive)
            {
                tmUpdate.Enabled = false;
                m_State = DsState.Reserved;

                Publish();
            }

            return base.Stop();
        }

        public override bool Close()
        {
            if (IsActive)
            {
                base.Close();

                tmUpdate.Enabled = false;
                m_State = DsState.Disconnected;

                Publish();
            }

            return !IsActive;
        }

        public override string ToString()
        {
            switch (m_State)
            {
                case DsState.Disconnected:

                    return string.Format("Pad {0} : Disconnected", m_ControllerId + 1);

                case DsState.Reserved:

                    return string.Format("Pad {0} : {1} {2} - Reserved", m_ControllerId + 1, Model, Local);

                case DsState.Connected:

                    return string.Format("Pad {0} : {1} {2} - {3} {4:X8} {5}", m_ControllerId + 1, Model,
                        Local,
                        Connection,
                        m_Packet,
                        Battery
                        );
            }

            throw new Exception();
        }

        private void HID_Worker_Thread(object sender, DoWorkEventArgs e)
        {
            var transfered = 0;
            var buffer = new byte[64];

            Log.Debug("-- USB Device : HID_Worker_Thread Starting");

            while (IsActive)
            {
                try
                {
                    if (ReadIntPipe(buffer, buffer.Length, ref transfered) && transfered > 0)
                    {
                        Parse(buffer);
                    }
                }
                catch (Exception ex)
                {
                    Log.ErrorFormat("Unexpected error: {0}", ex);
                }
            }

            Log.Debug("-- USB Device : HID_Worker_Thread Exiting");
        }

        private void On_Timer(object sender, EventArgs e)
        {
            lock (this)
            {
                Process(DateTime.Now);
            }
        }
    }
}