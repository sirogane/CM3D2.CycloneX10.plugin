using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CycloneX10Csharp
{
    ///------------------------------------------------
    ///CycloneX10Classについて
    ///------------------------------------------------
    ///CycloneX10 又は 臨界点-rinkaiten-を動作させるクラス
    //詳しくは以下のURLを参照
    //http://cyclone.co.jp/
    //http://www.waffle1999.com/onahole/rinkaiten.html

    public class CycloneX10Class
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct HIDD_ATTRIBUTES
        {
            public int Size;
            public ushort VendorID;
            public ushort ProductID;
            public short VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVICE_INTERFACE_DATA
        {
            public Int32 cbSize;
            public Guid interfaceClassGuid;
            public Int32 flags;
            public UIntPtr reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 1)]
        struct DEVICE_INTERFACE_DETAIL_DATA
        {
            public int cbSize;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string DevicePath;
        }

        [DllImport("hid.dll", SetLastError = true)]
        static extern void HidD_GetHidGuid(out Guid guid);

        [DllImport("hid.dll", SetLastError = true)]
        static extern void HidD_GetAttributes(SafeFileHandle hidDeviceObject, out HIDD_ATTRIBUTES attributes);

        [DllImport("setupapi.dll")]
        static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, [MarshalAs(UnmanagedType.LPTStr)] string enumerator, IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern Boolean SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, uint memberIndex, ref DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr hDevInfo, ref DEVICE_INTERFACE_DATA deviceInterfaceData, ref DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData, uint deviceInterfaceDetailDataSize, ref uint requiredSize, IntPtr deviceInfoData);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int WriteFile(SafeFileHandle file, byte[] buffer, uint numberBytesToWrite, out uint numberOfBytesWritten, IntPtr overlapped);

        /// 現在のパターン
        private int _pattern = 0;
        public int pattern
        {
            get { return _pattern; }
            set
            {
                //値だけ更新をするので注意
                _pattern = Clamp(value, 0, 9);
                //振動の更新
                StatusUpDate();
            }
        }
        /// 現在のレベル
        private int _level = 0;
        public int level
        {
            get { return _level; }
            set
            {
                //値だけ更新をするので注意
                _level = Clamp(value, 0, 9);
                //振動の更新
                StatusUpDate();
            }
        }
        //Deviceの取得ができているかどうか
        private bool _DeviceEnable = false;
        public bool IsDeviceEnable
        {
            get { return _DeviceEnable; }
        }
        //ポーズの状態を取得
        private bool _Pause = false;
        public bool IsPause
        {
            get { return _Pause; }
            set { 
                _Pause = value;
                SetPause(_Pause);
            }
        }

        /// 開放
        void OnDestroy()
        {
            Stop();
        }

        ///最後のパターンとレベル
        private int Old_pattern = 0;
        private int Old_level = 0;
        private bool Old_Pause = false;
        //ハンドル
        private SafeFileHandle fileHandle;

        public bool OpenDevice(int nVendorID = 0x0483, int nProductID = 0x5750)
        {
            Guid hidGuid;
            HidD_GetHidGuid(out hidGuid);

            IntPtr deviceInfoSet = SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero, 22);
            try
            {
                DEVICE_INTERFACE_DATA did = new DEVICE_INTERFACE_DATA();
                did.cbSize = Marshal.SizeOf(did);

                for (int i = 0; ; i++)
                {
                    //現在接続されているＵＳＢ装置の一覧から特定のＵＳＢ装置の情報を入手する
                    if (!SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, (uint)i, ref did))
                    {
                        if (Marshal.GetLastWin32Error() != 259)
                        {
                        }
                        break;
                    }

                    DEVICE_INTERFACE_DETAIL_DATA didd = new DEVICE_INTERFACE_DETAIL_DATA();
                    didd.cbSize = IntPtr.Size == 4 ? 4 + Marshal.SystemDefaultCharSize : 8;

                    uint requiredSize = 0;
                    SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref did, ref didd, (uint)Marshal.SizeOf(didd), ref requiredSize, IntPtr.Zero);
                    //同期して読み書きするファイルハンドルを作成する
                    using (SafeFileHandle fileHandle = CreateFile(didd.DevicePath, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
                    {
                        HIDD_ATTRIBUTES attributes;
                        HidD_GetAttributes(fileHandle, out attributes);
                        //ベンダーIDとプロダクトIDが一致するかどうか
                        if (attributes.VendorID == nVendorID && attributes.ProductID == nProductID)
                        {
                            //無事にハンドルの取得
                            this.fileHandle = CreateFile(didd.DevicePath, 0xc0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                            _DeviceEnable = true;
                            return true;
                        }
                    }
                }
            }
            finally
            {
                if (deviceInfoSet.ToInt64() != -1)
                {
                    //デバイスインフォメーションを破棄する.
                    SetupDiDestroyDeviceInfoList(deviceInfoSet);
                }
            }
            //取得に失敗した場合
            _DeviceEnable = false;
            return false;
        }

        public void StatusUpDate()
        {
            //デバイスが取得できていない場合は無視をする
            if (!IsDeviceEnable) { return; }

            //パターン、Level、ポーズが変わった場合変更をする
            if (Old_pattern != pattern || Old_level != level || Old_Pause != _Pause)
            {
                byte[] buffer = new byte[] { 0x00, 0x3C, 0x30, 0x31, 0x35, 0x32, PausByte(_Pause), (byte)(0x30 + pattern), (byte)(0x30 + level), 0x30, 0x30, 0x01, 0x02, 0x03, 0x68, 0x3E };
                uint numberOfBytesWritten;
                WriteFile(fileHandle, buffer, (uint)buffer.Length, out numberOfBytesWritten, IntPtr.Zero);

                Old_pattern = pattern;
                Old_level = level;
                Old_Pause = _Pause;
            }
        }

        /// パターンとレベルを更新する
        public void SetPatternAndLevel(int SetPattern, int SetLevel)
        {
            //デバイスが取得できていない場合は無視をする
            if (!IsDeviceEnable) { return; }


            //パターンかレベルに変更,もしくはPauseがかかっている場合
            if (Old_pattern != SetPattern || Old_level != SetLevel)
            {
                SetPattern = Clamp(SetPattern, 0, 9);
                SetLevel = Clamp(SetLevel, 0, 9);

                _Pause = false;

                byte[] buffer = new byte[] { 0x00, 0x3C, 0x30, 0x31, 0x35, 0x32, PausByte(_Pause), (byte)(0x30 + SetPattern), (byte)(0x30 + SetLevel), 0x30, 0x30, 0x01, 0x02, 0x03, 0x68, 0x3E };
                uint numberOfBytesWritten;
                WriteFile(fileHandle, buffer, (uint)buffer.Length, out numberOfBytesWritten, IntPtr.Zero);

                _pattern = SetPattern;
                _level = SetLevel;
                Old_pattern = pattern;
                Old_level = level;
                Old_Pause = false;
            }
        }
        //ポーズ&ポーズ切り替え
        public void Pause()
        {
            //デバイスが取得できていない場合は無視をする
            if (!IsDeviceEnable) { return; }
            //ポーズ状態を逆転させる
            SetPause(!IsPause);
        }
        //ポーズ状態を設定する
        private void SetPause(bool Flag)
        {
            _Pause = Flag;

            //ポーズ
            byte[] buffer = new byte[] { 0x00, 0x3C, 0x30, 0x31, 0x35, 0x32, PausByte(_Pause), (byte)(0x30 + pattern), (byte)(0x30 + level), 0x30, 0x30, 0x01, 0x02, 0x03, 0x68, 0x3E };
            uint numberOfBytesWritten;
            WriteFile(fileHandle, buffer, (uint)buffer.Length, out numberOfBytesWritten, IntPtr.Zero);
            Old_pattern = pattern;
            Old_level = level;
            Old_Pause = _Pause;

        }
        //停止をする
        public void Stop()
        {
            //デバイスが取得できていない場合は無視をする
            if (!IsDeviceEnable) { return; }
            //停止
            SetPatternAndLevel(0, 0);
        }

        /// 値の最大最小を制限する
        private static int Clamp(int value, int Min, int Max)
        {
            if (value < Min)
            {
                return Min;
            }
            else if (Max < value)
            {
                return Max;
            }
            else
            {
                return value;
            }
        }
        //ポーズ用のバイトを返す
        public byte PausByte(bool Flag)
        {
            if (Flag)
            {
                return 0x31;//ポーズ
            }
            else
            {
                return 0x30;//ポーズ中止
            }
        }
    }
}
