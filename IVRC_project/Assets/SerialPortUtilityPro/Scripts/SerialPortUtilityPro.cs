using UnityEngine;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#pragma warning disable 0414

namespace SerialPortUtility
{
	[System.Serializable]
	public class SPUPEventObject : UnityEngine.Events.UnityEvent<object> { }
	[System.Serializable]
	public class SPUPSystemEventObject : UnityEngine.Events.UnityEvent<SerialPortUtility.SerialPortUtilityPro, string> { }

	[StructLayout(LayoutKind.Sequential)]
	public class SPUPMudbusData
	{
		public int Address;
		public int Function;
		public byte[] Data;
		public SPUPMudbusData(int addr, int func, byte[] dat)
		{
			Address = addr;
			Function = func;
			Data = dat;
		}
	}

	[HelpURL("https://portutility.com")]
#if UNITY_5_5_OR_NEWER
	[DefaultExecutionOrder(-100)]
#endif
	[AddComponentMenu("SerialPort/SerialPort Utility Pro")]
	public class SerialPortUtilityPro : MonoBehaviour
	{
        //Version Infomation
        public const string VersionString = "2.21 FREE TRIAL";

        //spapmain.cpp
        //SPAP snapConfig
        [StructLayout(LayoutKind.Sequential)]
		private struct SpapConfig
		{
			//Config
			public int BaudRate;    //32bit x6
			public int Parity;
			public int StopBit;
			public int DataBit;
			public int DiscardNull;
			public int IgnoreBreakSignal;

			public int Skip;
		}

		//Define
		public enum OpenSystem
		{
			NumberOrder = 0,

			USB = 1,
			PCI = 2,
			BluetoothSSP = 3,

			TCPSerialEmulatorClient = 10,
			TCPSerialEmulatorServer = 11,
		}
		public enum MethodSystem
		{
			Streaming = 0,
			BinaryStreaming,
			LineFeedDataToString,
			LineFeedDataToBinary,
			FixedLengthDataToString,
			FixedLengthDataToBinary,
			SplitStringToArray,
			SplitStringToDictionary,
			SplitStringToGameObject,
			JSONToClassObject,
			ModbusASCII,
			//ModbusRTU,
		}
		public enum ParityEnum
		{
			No = 0,
			Odd,
			Even,
			Mark,
			Space,
		}
		public enum StopBitEnum
		{
			OneBit = 0,
			TwoBit = 2,
		}
		public enum DataBitEnum
		{
			EightBit = 8,
			SevenBit = 7,
		}

		public enum SourcePath
		{
			AbsolutePath = 0,
			RelativeToStreamingAssetsFolder,
			RelativeToDataFolder,
			RelativeToPeristentDataFolder,
		}

		public enum UpdateMethod
		{
			Update,
			FixedUpdate,
			ManualUpdate,
		}

		//Variable : public
		//Config
		public bool IsAutoOpen = true;
		public OpenSystem OpenMethod = OpenSystem.USB;
		public MethodSystem ReadProtocol = MethodSystem.LineFeedDataToString;

		public string VendorID {
			set { this.VendorID_string = value; }
			get { return this.VendorID_string; }
		}
		public string ProductID
		{
			set { this.ProductID_string = value; }
			get { return this.ProductID_string; }
		}
		public string SerialNumber
		{
			set { this.SerialNumber_search_string = value; }
			get { return this.SerialNumber_search_string; }
		}
		public string IPAddress
		{
			set { this.VendorID_string = value; }
			get { return this.VendorID_string; }
		}
		public string Port
		{
			set { this.ProductID_string = System.Text.RegularExpressions.Regex.Replace(value, "[^0-9]", ""); }
			get { return this.ProductID_string; }
		}
		public string DeviceName
		{
			set { this.SerialNumber_search_string = value; }
			get { return this.SerialNumber_search_string; }
		}

        //Device infomation class
        public class DeviceInfo
        {
            public string Vendor;
            public string Product;
            public string SerialNumber;
        }

		//structure
		public int BaudRate = 9600;
		public ParityEnum Parity = ParityEnum.No;
		public StopBitEnum StopBit = StopBitEnum.OneBit;
		public DataBitEnum DataBit = DataBitEnum.EightBit;
		public bool RecvDiscardNull = false;
		public const bool RecvIgnoreBreakSignal = true;
		public bool AutoRTSCTSHandshake = false;
		public bool StartEnableDTR = true;
		public bool DtrEnabled = false;
		public bool RtsEnabled = false;
		public int Skip = 0;
		public bool BluetoothSSPNoServerMode = false;

		//read structure
		public string FeedCode = "<CR><LF>";
		public string SplitCode = ",";
		public int FixedFeedCount = 10;
		public UpdateMethod UpdateProcessing = UpdateMethod.Update;

		//Event
		public SPUPEventObject ReadCompleteEventObject = new SPUPEventObject();
		public string ReadCompleteEventObjectType = "";
		public GameObject ReadClassMembersObject = null;

		public SPUPSystemEventObject SystemEventObject = new SPUPSystemEventObject();

		//DebugString
		public string GetSerialDebugString {
			get { return SerialDebugString; }
		}
		public void SetDebugConsoleMonitorView(bool enable)
		{
			if (IsOpened()) //Opened
				return;

			DebugConsoleMonitor = enable;
		}

		public SourcePath ExternalConfigPath = SourcePath.RelativeToStreamingAssetsFolder;
		public string ExternalConfigFileName = "serial_config.txt";

		//for inside system : private
		[SerializeField]
		private string VendorID_string = "1234";
		[SerializeField]
		private string ProductID_string = "1234";
		[SerializeField]
		private string SerialNumber_search_string = "";
		[SerializeField]
		private bool EnableTrans = true;

		[SerializeField]
		private bool DebugConsoleMonitor = false;	//Debug Monitor
		[SerializeField]
		private bool ExternalConfig = false;

		//for Editor
		[SerializeField]
		private bool ExpandConfig = true;
		[SerializeField]
		private bool ExpandSPMonitor = false;
		[SerializeField]
		private bool ExpandEventConfig = false;

		private const int STRING_MAXBUFFER = 512;
		private const int SPAPHANDLE_ERROR = (-1);
		private const int SPAPHANDLE_PERMISSION = (-2);
		private const int SerialDebugStringMAX = 100;
		private const int READDATA_ERROR_DISCONNECT = (-1);
		private const int READDATA_ERROR_LICENSEEER = (-3);
		private List<byte> dataBuffer = new List<byte>();
		private bool RtsEnableSave = true;
		private int SerialPortHandle = SPAPHANDLE_ERROR;
		private string SerialDebugString = "";
		private GameObject DebugConsoleObject = null;
		private bool IsErrFinished = false;

		//equal code : system fixed
		private const string equalChar = "=";

		//DLL import
		////////////// Main SPAP DLL System //////////////
		[DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		static extern int spapOpenUSB(string vId, string pId, string serial, SpapConfig config);
		[DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		static extern int spapOpenPCIe(string vId, string dId, string serial, SpapConfig config);
		[DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		static extern int spapOpenBtSPP(string search, bool isServer, SpapConfig config);
		[DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		static extern int spapOpenBtVCOM(string search, SpapConfig config);
		[DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		static extern int spapOpen(SpapConfig config);
		[DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		static extern int spapOpenSerialEmulatorTCP(string sendIP, string port);
		[DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		static extern void spapClose(int handle);
		[DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		static extern void spapShutDown();
		[DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		static extern int spapSetConfig(int handle, SpapConfig config);
		[DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		static extern SpapConfig spapGetConfig(int handle);
		[DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		static extern void spapWrite(int handle, System.IntPtr buffer, int bufferLength);
		[DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		static extern int spapReadDataAvailable(int handle);
		[DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		static extern int spapReadData(int handle, byte[] str, int str_size);
		[DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		static extern void spapSetDTR(int handle, bool enable);
		[DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		static extern void spapSetRTS(int handle, bool enable);
		[DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		static extern bool spapGetDTR(int handle);
		[DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		static extern bool spapGetRTS(int handle);
		[DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		static extern bool spapGetDSR(int handle);
		[DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		static extern bool spapGetCTS(int handle);
		[DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		static extern void spapBreakSignal(int handle, bool enable);

        //List
        [DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        static extern int spapDeviceListAvailable();
        [DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        static extern int spapDeviceList(int deviceNum, [MarshalAs(UnmanagedType.LPStr)]System.Text.StringBuilder deviceInfo, int buffer_size);

        //License System
        [DllImport("spap", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		static extern void spapIsLicensed([MarshalAs(UnmanagedType.LPStr)]StringBuilder email, [MarshalAs(UnmanagedType.LPStr)]StringBuilder code, int size);
		private static bool licensecheck = true;	//asset
		public string LicenseEmail = "";
		public string LicenseCode = "";

		//Android Object
#if UNITY_ANDROID && !UNITY_EDITOR
		private static AndroidJavaObject _androidPlugin = null;
#endif
		//Start
		void Start()
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			// Plugin
			if (_androidPlugin == null) _androidPlugin = new AndroidJavaObject("com.wizapply.libspap.spap");
			// Get Context(Activity) Object
			AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
			AndroidJavaObject unityContext = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
			if (_androidPlugin != null)
				_androidPlugin.Call("SetSPAPContext", unityContext);
#else
			//Unlock
			if(!licensecheck) {
				if (LicenseEmail == string.Empty)
				{
					//License
					StringBuilder owner = new StringBuilder(255);
					StringBuilder code = new StringBuilder(255);
					spapIsLicensed(owner, code, 255);
					LicenseEmail = owner.ToString();
					LicenseCode = code.ToString();

					if (LicenseEmail != "")
						Debug.Log("SerialPort Utility Pro Licensed E-Mail:" + LicenseEmail);
					else
					{
						LicenseEmail = "";
						Debug.Log("SerialPort Utility Pro is FREE VERSION.");
					}

					licensecheck = true;
				}
				else
				{
					Debug.Log("SerialPort Utility Pro Licensed E-Mail:" + LicenseEmail);
					licensecheck = true;
				}
			}
#endif
			if (DebugConsoleMonitor)	// && Debug.isDebugBuild
			{
				if (DebugConsoleObject == null)
				{
					//debug
					GameObject obj = Resources.Load<GameObject>("Prefabs/SPUPDebugConsole");
					DebugConsoleObject = Instantiate(obj);
					DebugConsoleObject.transform.SetParent(this.transform);	//parent set
				}
			}

			//Auto Open
			if (IsAutoOpen)
			{
				Open();
			}
		}

		//End
		void OnDisable()
		{
			Close();
		}

		//Pause
		void OnApplicationPause(bool pauseStatus)
		{
			if (AutoRTSCTSHandshake)
			{
				if (pauseStatus) {
					RtsEnableSave = RtsGetStatus();
					RtsEnabled = false;
				}
				else
					RtsEnabled = RtsEnableSave;
				RtsEnable(RtsEnabled);
			}
		}

		public bool EnabledTransmission
		{
			set { this.EnableTrans = value; }
			get { return this.EnableTrans; }
		}

		/// <summary>
		/// Open Method
		/// </summary>
		public void Open()
		{
			if (IsOpened())	//Opened
				return;

			IsErrFinished = false;

			//External File Configure
			if (ExternalConfig == true)
				ExternalConfigApply(ExternalConfigFileName);

			//method
#if UNITY_ANDROID && !UNITY_EDITOR
			if(_androidPlugin != null)
			{
				AndroidJavaObject config = new AndroidJavaObject("com.wizapply.libspap.SpapConfig");
				config.Set<int>("BaudRate", BaudRate);
				config.Set<int>("Parity", (int)Parity);
				config.Set<int>("DataBit", (int)DataBit);
				config.Set<int>("StopBit", (int)StopBit);
				config.Set<int>("DiscardNull", (RecvDiscardNull == true) ? 1 : 0);
				config.Set<int>("IgnoreBreakSignal", (RecvIgnoreBreakSignal == true) ? 1 : 0);
				config.Set<int>("Skip", Skip);

				switch (OpenMethod)	//Android
				{
					case SerialPortUtilityPro.OpenSystem.USB:
						SerialPortHandle = _androidPlugin.Call<int>("OpenUSB", VendorID, ProductID, SerialNumber_search_string, config);
						break;
					case SerialPortUtilityPro.OpenSystem.BluetoothSSP:
						SerialPortHandle = _androidPlugin.Call<int>("OpenBtSPP", SerialNumber_search_string, config);
						break;
					case SerialPortUtilityPro.OpenSystem.NumberOrder:
						SerialPortHandle = _androidPlugin.Call<int>("Open", config);
						break;
					case SerialPortUtilityPro.OpenSystem.TCPSerialEmulatorClient:
						SerialPortHandle = _androidPlugin.Call<int>("OpenSerialEmulatorTCP", VendorID, ProductID);
						break;
					case SerialPortUtilityPro.OpenSystem.TCPSerialEmulatorServer:
						SerialPortHandle = _androidPlugin.Call<int>("OpenSerialEmulatorTCP", "", ProductID);
						break;
					default:
						SerialPortHandle = SPAPHANDLE_ERROR;
						break;
				}
			}
#else
			//Start
			SpapConfig config;
			config.BaudRate = BaudRate;
			config.Parity = (int)Parity;
			config.DataBit = (int)DataBit;
			config.StopBit = (int)StopBit;
			config.DiscardNull = (RecvDiscardNull == true) ? 1 : 0;
			config.IgnoreBreakSignal = (RecvIgnoreBreakSignal == true) ? 1 : 0;
			config.Skip = Skip;

			switch (OpenMethod)
			{
				case SerialPortUtilityPro.OpenSystem.USB:
					SerialPortHandle = spapOpenUSB(VendorID_string, ProductID_string, SerialNumber_search_string, config);
					break;
				case SerialPortUtilityPro.OpenSystem.PCI:
					SerialPortHandle = spapOpenPCIe(VendorID_string, ProductID_string, SerialNumber_search_string, config);
					break;
				case SerialPortUtilityPro.OpenSystem.BluetoothSSP:
					SerialPortHandle = spapOpenBtVCOM(SerialNumber_search_string, config);
					break;
				case SerialPortUtilityPro.OpenSystem.NumberOrder:
					SerialPortHandle = spapOpen(config);
					break;
				case SerialPortUtilityPro.OpenSystem.TCPSerialEmulatorClient:
					SerialPortHandle = spapOpenSerialEmulatorTCP(VendorID_string, ProductID_string);
					break;
				case SerialPortUtilityPro.OpenSystem.TCPSerialEmulatorServer:
					SerialPortHandle = spapOpenSerialEmulatorTCP("", ProductID_string);
					break;
			}
#endif
			RtsEnabled = RtsGetStatus();	//GetStatus
			DtrEnabled = DtrGetStatus();

			if (StartEnableDTR)
				DtrEnable(true);

			if (AutoRTSCTSHandshake)
			{
				RtsEnabled = true;
				RtsEnable(RtsEnabled);
			}
			RtsEnableSave = RtsEnabled;

			//Error
			if (SerialPortHandle == SPAPHANDLE_ERROR)
			{
				if (this != null)
					Debug.LogError("SerialPort UtilityPro attached to [" + this.name + "] is Open Error!");
				if (SystemEventObject != null)
					SystemEventObject.Invoke(this, "OPEN_ERROR");
			}
#if UNITY_ANDROID && !UNITY_EDITOR
			//Andoid
			else if (SerialPortHandle == SPAPHANDLE_PERMISSION)
			{	//hasParmission
				Invoke("Open", 3.0f);	//Reconnect again in 3 seconds

				if(SystemEventObject != null)
					SystemEventObject.Invoke(this, "PERMISSION_ERROR");
			}
#endif
			else
			{
				if (SystemEventObject != null)
					SystemEventObject.Invoke(this, "OPENED");
			}
			SerialDebugString = "";
			dataBuffer.Clear();
		}

		//Invoke
		public void OpenInvoke(float time)
		{
			Invoke("Open", time);	//Invoke Open
		}

		public void Close()
		{
			if (!IsOpened())	//Closed
				return;

			//Shutdown
#if UNITY_ANDROID && !UNITY_EDITOR
			if (_androidPlugin != null) _androidPlugin.Call("Close", SerialPortHandle);
#else
			spapClose(SerialPortHandle);
#endif
			SerialPortHandle = SPAPHANDLE_ERROR;

			if (SystemEventObject != null)
				SystemEventObject.Invoke(this, "CLOSED");
		}

		// Update is called once per frame : ReadSystem
		void Update()	//forUnity
		{
			if(UpdateProcessing == UpdateMethod.Update)
				ReadUpdate();
		}
		void FixedUpdate()	//forUnity
		{
			if (UpdateProcessing == UpdateMethod.FixedUpdate)
				ReadUpdate();
		}

		// Update
		public void ReadUpdate()
		{
			if (!IsOpened())	//close
				return;

			int size = 0;
#if UNITY_ANDROID && !UNITY_EDITOR
			if(_androidPlugin != null)
				size = _androidPlugin.Call<int>("ReadDataAvailable", SerialPortHandle);
#else
			size = spapReadDataAvailable(SerialPortHandle);
#endif
			if (size > 0)
			{
				byte[] dataArray = null;
				int iRet = 0;
				
#if UNITY_ANDROID && !UNITY_EDITOR
				if(_androidPlugin != null) {
					iRet = (int)_androidPlugin.Call<long>("ReadData", SerialPortHandle, size);
					dataArray = _androidPlugin.Get<byte[]>("m_byteData");
				}
#else
				dataArray = new byte[size];
				iRet = spapReadData(SerialPortHandle, dataArray, size);
#endif
				if (iRet <= 0 || dataArray == null)	//Error
					return;

				//buffer
				dataBuffer.AddRange(dataArray.Take(iRet));

				//for debug Log
				string stringData = System.Text.Encoding.UTF8.GetString(dataArray, 0, iRet);

				if (EnableTrans)
				{
					switch (ReadProtocol)
					{
						case SerialPortUtilityPro.MethodSystem.SplitStringToArray: UpdateSplitToArray();
							break;
						case SerialPortUtilityPro.MethodSystem.FixedLengthDataToString: UpdateFixedCharacters(false);
							break;
						case SerialPortUtilityPro.MethodSystem.FixedLengthDataToBinary: UpdateFixedCharacters(true);
							break;
						case SerialPortUtilityPro.MethodSystem.SplitStringToDictionary: UpdateSplitToDictionary();
							break;
						case SerialPortUtilityPro.MethodSystem.JSONToClassObject: UpdateJsonObject();
							break;
						case SerialPortUtilityPro.MethodSystem.SplitStringToGameObject: UpdateArrayToClassMembers();
							break;
						case SerialPortUtilityPro.MethodSystem.LineFeedDataToString: UpdateLineFeed(false);
							break;
						case SerialPortUtilityPro.MethodSystem.LineFeedDataToBinary: UpdateLineFeed(true);
							break;
						case SerialPortUtilityPro.MethodSystem.BinaryStreaming: UpdateOnStreaming(true);
							break;
						case SerialPortUtilityPro.MethodSystem.ModbusASCII: UpdateModbus(false);
							break;
						//case SerialPortUtilityPro.MethodSystem.ModbusRTU: UpdateModbus(true);
						//	break;
						default: UpdateOnStreaming(false);
							break;
					}

					SerialDebugAddString(" " + ControlCodeToView(stringData));
				}
				else
				{
					SerialDebugAddString(" <Is not valid>" + ControlCodeToView(stringData));
				}
			}
			else if (size == READDATA_ERROR_DISCONNECT)
			{
				//Disconnected
				Close();

				//Android
#if UNITY_ANDROID && !UNITY_EDITOR
				if (!BluetoothSSPNoServerMode && _androidPlugin != null)
				{
					if (OpenMethod == SerialPortUtilityPro.OpenSystem.BluetoothSSP)
					{
						//Server Bluetooth
						SerialPortHandle = _androidPlugin.Call<int>("OpenBtSPP_Server");
						if (SerialPortHandle != SPAPHANDLE_ERROR)
						{
							if (SystemEventObject != null)
								SystemEventObject.Invoke(this, "BT_DISCONNECT_TO_SERVERMODE");

							return;	//ServerMode
						}
					}
				}
#endif
				IsErrFinished = true;
				Debug.LogError("SerialPort UtilityPro attached to [" + this.name + "] is Close!");

				if (SystemEventObject != null)
					SystemEventObject.Invoke(this, "DISCONNECT_ERROR");
			}
			else if (size == READDATA_ERROR_LICENSEEER)
			{
				//Disconnected
				Close();
				IsErrFinished = true;
				//License End
				Debug.LogError("SerialPortUtility Pro became transmission restriction (1MB) for FREE Version.\nYou can buy a License than this if you like it.\n* This limit is resettable by rebooting application. ");
				
				if (SystemEventObject != null)
					SystemEventObject.Invoke(this, "LICENSE_ERROR");
			}
			else if (size == 0)
			{
				if (ReadProtocol == SerialPortUtilityPro.MethodSystem.Streaming && EnableTrans)
					UpdateOnStreaming(false);
				if (ReadProtocol == SerialPortUtilityPro.MethodSystem.BinaryStreaming && EnableTrans)
					UpdateOnStreaming(true);
			}
		}

		public void SerialDebugAddString(string message)
		{
			if(message.Length > 128) message = message.Remove(128);	//max 128
			string debug = string.Format("[{0}]{1}\n", System.DateTime.Now.ToString("MM/dd HH:mm:ss"), message);
			SerialDebugString = SerialDebugString.Insert(0, debug);
			if (SerialDebugString.Split("\n".ToCharArray()).Length >= SerialDebugStringMAX)
			{
				SerialDebugString = SerialDebugString.Remove(SerialDebugString.LastIndexOf('\n'));
			}
		}

        //Converter
		private string ControlCodeToView(string inStr)
		{
			string outStr;
			string[] ctrlStr = { "NUL", "SOH", "STX", "ETX", "EOT", "ENQ", "ACK", "BEL",
                     "BS",  "HT",  "LF",  "VT",  "NP",  "CR",  "SO",  "SI"};
			outStr = System.Text.RegularExpressions.Regex.Replace(inStr, @"\p{Cc}", str =>
			{
				int offset = str.Value[0];
				if (ctrlStr.Length > offset)
				{
					return "<" + ctrlStr[offset] + ">";
				}
				else
				{
					return string.Format("<{0:X2}>", (byte)str.Value[0]);
				}
			});

			return outStr;
		}

        private string ViewToControlCode(string inStr)
        {
			//<NUL>, <CR>, <LF>, <STX>, <ETX>, <HT(TAB)>, <ACK>
			return inStr.Replace("<NUL>", "\0").Replace("<CR>", "\r").Replace("<LF>", "\n").Replace("<STX>", "\x02").Replace("<ETX>", "\x03").Replace("<HT>", "\t").Replace("<ACK>", "\x06");
        }

		/// <summary>
		/// Update Method
		/// </summary>
		private int OnStreamingCounter = 0;
		private int OnStreamingCounterLimit = 0;
		private const int OnStreamingCounterDetect = 10;
		private const int OnStreamingCounterLimitDetect = 120;
		private void UpdateOnStreaming(bool binaryMode)
		{
			if (dataBuffer.Count != 0)
			{
				OnStreamingCounter++;
				OnStreamingCounterLimit++;
				if ((OnStreamingCounter > OnStreamingCounterDetect) ||
					(OnStreamingCounterLimit > OnStreamingCounterLimitDetect))
				{
					if (binaryMode)
						ReadCompleteEventObject.Invoke(dataBuffer.ToArray());
					else {
						string buf = System.Text.Encoding.UTF8.GetString(dataBuffer.ToArray());
						ReadCompleteEventObject.Invoke(buf);
					}
					dataBuffer.Clear();
					OnStreamingCounter = 0;
					OnStreamingCounterLimit = 0;
				}
			}
			else
			{
				OnStreamingCounter = 0;
			}
		}

		private void UpdateSplitToArray()
		{
            //feedcode
            //spritcode change
            string fcode = ViewToControlCode(FeedCode);
			string scode = ViewToControlCode(SplitCode);

            int d;
			string dsbuf = System.Text.Encoding.UTF8.GetString(dataBuffer.ToArray());
			while ((d = dsbuf.IndexOf(fcode, 0)) >= 0)
			{
				List<string> _dataList = new List<string>();
				string str = dsbuf.Substring(0, d);

				string[] str2 = str.Split(scode.ToCharArray(), System.StringSplitOptions.RemoveEmptyEntries);
				_dataList.Clear();
				foreach (string s in str2)
					_dataList.Add(s);
				
				dataBuffer.RemoveRange(0, d + fcode.Length);	//delete
				if (ReadCompleteEventObject != null)
					ReadCompleteEventObject.Invoke(_dataList);

				dsbuf = System.Text.Encoding.UTF8.GetString(dataBuffer.ToArray());
			}
		}

		private int FixedCharactersCounter = 0;
		private int FixedCharactersCounterDetect = 120;
		private void UpdateFixedCharacters(bool binaryMode)
		{
			if (dataBuffer.Count != 0)
			{
				FixedCharactersCounter++;
				if (FixedCharactersCounter > FixedCharactersCounterDetect)
				{
					dataBuffer.Clear();
					FixedCharactersCounter = 0;
				}
			}

			while(dataBuffer.Count >= FixedFeedCount)
			{
				if (binaryMode)
				{
					byte[] dataByte = dataBuffer.GetRange(0, FixedFeedCount).ToArray();
					dataBuffer.RemoveRange(0, FixedFeedCount);

					FixedCharactersCounter = 0;

					if (ReadCompleteEventObject != null)
						ReadCompleteEventObject.Invoke((object)dataByte);
				}
				else
				{
					string str = System.Text.Encoding.UTF8.GetString(dataBuffer.ToArray());
					str = str.Substring(0, FixedFeedCount);
					dataBuffer.RemoveRange(0, FixedFeedCount);

					FixedCharactersCounter = 0;

					if (ReadCompleteEventObject != null)
						ReadCompleteEventObject.Invoke((object)str);
				}
			}
		}

		private void UpdateSplitToDictionary()
		{
			int d;
            string fcode = ViewToControlCode(FeedCode);
			string scode = ViewToControlCode(SplitCode);

			string dsbuf = System.Text.Encoding.UTF8.GetString(dataBuffer.ToArray());
			while ((d = dsbuf.IndexOf(fcode, 0)) >= 0)
			{
				Dictionary<string,string> _dataList = new Dictionary<string,string>();
				string str = dsbuf.Substring(0, d);
				string[] str2 = str.Split(scode.ToCharArray(), System.StringSplitOptions.RemoveEmptyEntries);
				_dataList.Clear();

				for (int i = 0; i < str2.Length; ++i)
				{
					string[] strc = str2[i].Split(equalChar.ToCharArray(), System.StringSplitOptions.RemoveEmptyEntries);
					if (strc.Length == 2)
					{
						string key = strc[0];
						if (_dataList.ContainsKey(key))
							_dataList[key] = strc[1];
						else
							_dataList.Add(key, strc[1]);
					}
				}

				dataBuffer.RemoveRange(0, d + fcode.Length);
				if (ReadCompleteEventObject != null)
					ReadCompleteEventObject.Invoke((object)_dataList);

				dsbuf = System.Text.Encoding.UTF8.GetString(dataBuffer.ToArray());
			}
		}

		private void UpdateArrayToClassMembers()
		{
			int d;
            string fcode = ViewToControlCode(FeedCode);
			string scode = ViewToControlCode(SplitCode);

			string dsbuf = System.Text.Encoding.UTF8.GetString(dataBuffer.ToArray());
			while ((d = dsbuf.IndexOf(fcode, 0)) >= 0)
			{
				Dictionary<string, string> _dataList = new Dictionary<string, string>();
				string str = dsbuf.Substring(0, d);
				string[] str2 = str.Split(scode.ToCharArray(), System.StringSplitOptions.None);
				_dataList.Clear();

				for (int i = 0; i < str2.Length; ++i)
				{
					string[] strc = str2[i].Split(equalChar.ToCharArray(), System.StringSplitOptions.None);
					if (strc.Length == 2)
					{
						string key = strc[0];
						if (_dataList.ContainsKey(key))
							_dataList[key] = strc[1];
						else
							_dataList.Add(key, strc[1]);
					}
				}

				dataBuffer.RemoveRange(0, d + fcode.Length);

				//Set class
				if (ReadClassMembersObject != null)
				{
					foreach (MonoBehaviour listds in ReadClassMembersObject.GetComponents<MonoBehaviour>())
					{
						System.Type t = listds.GetType();
						foreach (System.Reflection.FieldInfo f in t.GetFields())
						{
							try
							{
								string key = string.Format("{0}.{1}", t.ToString(), f.Name);
								if (_dataList.ContainsKey(key))
								{
									f.SetValue(ReadClassMembersObject.GetComponent(t), System.Convert.ChangeType(_dataList[key],f.FieldType));
								}
							}
							catch (System.ArgumentNullException){}
						}
					}
				}

				dsbuf = System.Text.Encoding.UTF8.GetString(dataBuffer.ToArray());
			}
		}

		private void UpdateJsonObject()
		{
			string jsonFeedCode = "}";
			string jsonFeedCodeStart = "{";
			int d;
			string dsbuf = System.Text.Encoding.UTF8.GetString(dataBuffer.ToArray());
			while ((d = dsbuf.IndexOf(jsonFeedCode, 0)) >= 0)
			{
				int st = dsbuf.IndexOf(jsonFeedCodeStart, 0);
				string dataStr = dsbuf.Substring(st, d - st + jsonFeedCode.Length);
				dataBuffer.RemoveRange(0, d + jsonFeedCode.Length);
				if (ReadCompleteEventObject != null)
				{
					try {
						if (ReadCompleteEventObjectType != "")
							ReadCompleteEventObject.Invoke(JsonUtility.FromJson(dataStr, System.Type.GetType(ReadCompleteEventObjectType)));
					} catch(System.ArgumentNullException){
						Debug.Log("Error Object Type");
					}
				}

				dsbuf = System.Text.Encoding.UTF8.GetString(dataBuffer.ToArray());
			}
		}

		private void UpdateLineFeed(bool BinaryMode)
		{
			int d;
            string fcode = ViewToControlCode(FeedCode);

			string dsbuf = System.Text.Encoding.UTF8.GetString(dataBuffer.ToArray());
			while ((d = dsbuf.IndexOf(fcode, 0)) >= 0)
			{
				if (BinaryMode)
				{
					byte[] dataByte = dataBuffer.GetRange(0, d).ToArray();
					dataBuffer.RemoveRange(0, d + fcode.Length);
					if (ReadCompleteEventObject != null)
						ReadCompleteEventObject.Invoke((object)dataByte);
				}
				else
				{
					string dataStr = dsbuf.Substring(0, d);
					dataBuffer.RemoveRange(0, d + fcode.Length);
					if (ReadCompleteEventObject != null)
						ReadCompleteEventObject.Invoke((object)dataStr);
				}

				dsbuf = System.Text.Encoding.UTF8.GetString(dataBuffer.ToArray());
			}
		}

		private void UpdateModbus(bool BinaryMode)
		{
			int d;

			if (BinaryMode)
			{
				//RTU
				//Not yet supported
			}
			else
			{
				//ASCII
				string fcode = "\r\n";	//feedcode = <CR><LF>
				string dsbuf = System.Text.Encoding.UTF8.GetString(dataBuffer.ToArray());
				while ((d = dsbuf.IndexOf(fcode, 0)) >= 0)
				{
					int f_colon = dsbuf.IndexOf(":", 0) + 1;
					SPUPMudbusData dataModbus = null;
					if (f_colon > 0 && f_colon <= d)
					{
						string dataStr = dsbuf.Substring(f_colon, d - f_colon);
						byte[] dataBin = StringToBytes(dataStr);
						if(dataBin.Length >= 3) {
							//LRC
							byte lrc = GetLRC(dataBin, 0, dataBin.Length-1);
							if (lrc == dataBin[dataBin.Length - 1])
							{
								//OK
								int bytelen = dataBin.Length - 3;
								byte[] sendData = new byte[bytelen];	//Address, Function, LRC = 3byte
								System.Array.Copy(dataBin, 2, sendData, 0, bytelen);
								dataModbus = new SPUPMudbusData((int)dataBin[0], (int)dataBin[1], sendData);
							}
						}
					}

					dataBuffer.RemoveRange(0, d + fcode.Length);
					if(dataModbus != null) {
						if (ReadCompleteEventObject != null)
							ReadCompleteEventObject.Invoke((object)dataModbus);
					}
					dsbuf = System.Text.Encoding.UTF8.GetString(dataBuffer.ToArray());
				}
			}
		}

		private byte GetLRC(byte[] bytes, int start, int end)
		{
			byte LRC = 0;
			for (int i = start; i < end; i++)
				LRC -= bytes[i];
			return LRC;
		}
		private byte[] StringToBytes(string str)
		{
			var bs = new List<byte>();
			for (int i = 0; i < str.Length / 2; i++)
				bs.Add(System.Convert.ToByte(str.Substring(i * 2, 2), 16));
			return bs.ToArray();
		}

		//Write
		public bool Write(string writeString)
		{
			if (AutoRTSCTSHandshake)
			{
				if (!CtsHolding()) return false;
			}

			if (!EnableTrans)
				return false;

			Write(System.Text.Encoding.UTF8.GetBytes(writeString));

			return true;
		}
		public bool Write(byte[] writeData)
		{
			if (AutoRTSCTSHandshake)
			{
				if (!CtsHolding()) return false;
			}

			if (!EnableTrans)
				return false;

#if UNITY_ANDROID && !UNITY_EDITOR
			if(_androidPlugin != null)
				_androidPlugin.Call("WriteBuffer", SerialPortHandle, writeData, writeData.Length);
#else
			GCHandle wd = GCHandle.Alloc(writeData, GCHandleType.Pinned);
			spapWrite(SerialPortHandle, wd.AddrOfPinnedObject(), writeData.Length);
			wd.Free();
#endif
			SerialDebugAddString("(SEND) " + ControlCodeToView(System.Text.Encoding.UTF8.GetString(writeData)));

			return true;
		}
		public bool Write(byte writeOneData)
		{
			byte[] writeData = new byte[1] { writeOneData };
			return Write(writeData);
		}
		public bool WriteJSON(object jsonObject)
		{
			if (jsonObject == null)
				return false;

			try
			{
				return Write(JsonUtility.ToJson(jsonObject));
			}
			catch(System.ArgumentException)
			{
				Debug.LogError("The specified object is not convertible for JSON format.");
				return false;
			}
		}

		public bool Write(string writeString, string feedCode)
		{
            string fcode = ViewToControlCode(feedCode);
			return Write(writeString + fcode);
		}
		public bool WriteCRLF(string writeString)
		{
			return Write(writeString + "\r\n");
		}
		public bool WriteCR(string writeString)
		{
			return Write(writeString + "\r");
		}
		public bool WriteLF(string writeString)
		{
			return Write(writeString + "\n");
		}

		public bool Write(List<string> writeString, string splitCode, string feedCode)
		{
			string resString = "";

            string fcode = ViewToControlCode(feedCode);
            foreach (string s in writeString) {
				resString += s + splitCode;
			}

			return Write(resString + fcode);
		}

		public bool Write(Dictionary<string, string> writeString, string splitCode, string feedCode)
		{
			string resString = "";
			List<string> keyList = new List<string>(writeString.Keys);
            string fcode = ViewToControlCode(feedCode);

            foreach (string key in keyList)
				resString += key + equalChar + writeString[key] + splitCode;

			return Write(writeString + fcode);
		}

		public bool Write(GameObject gameObject_string, string splitCode, string feedCode)
		{
			if (gameObject_string != null)
			{
				List<string> sendData = new List<string>();
				foreach (MonoBehaviour listds in ReadClassMembersObject.GetComponents<MonoBehaviour>())
				{
					System.Type t = listds.GetType();
					foreach (System.Reflection.FieldInfo f in t.GetFields())
					{
						if (f.FieldType == typeof(string))
						{
							string key = string.Format("{0}.{1}", t.ToString(), f.Name);
							string data = f.GetValue(gameObject_string.GetComponent(t)) as string;
							sendData.Add(key + equalChar + data);
						}
					}
				}

				//Send
				if (Write(sendData, splitCode, feedCode))
					return false;
			}

			return true;
		}
		public bool Write(SPUPMudbusData mudbus_data, bool binaryMode = false)
		{
			if (binaryMode)
			{
				//none
				return false;
			}
			else
			{
				string sendDataString = "";
				
				sendDataString += string.Format("{0:X2}", mudbus_data.Address);
				sendDataString += string.Format("{0:X2}", mudbus_data.Function);

				for (int i = 0; i < mudbus_data.Data.Length; i++)
					sendDataString += string.Format("{0:X2}", mudbus_data.Data[i]);

				byte[] ba = StringToBytes(sendDataString);
				byte lrc = GetLRC(ba, 0, ba.Length);
				sendDataString += string.Format("{0:X2}", lrc);


				return Write(":" + sendDataString, "<CR><LF>");
			}
		}

		//Proparty
		public bool IsOpened()
		{
			return SerialPortHandle >= 0;
		}
		public bool IsErrorFinished()
		{
			return IsErrFinished;
		}

		public void RtsEnable(bool enable)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if(_androidPlugin != null)
				_androidPlugin.Call("SetRTS", SerialPortHandle, enable);
#else
			spapSetRTS(SerialPortHandle, enable);
#endif
			RtsEnabled = enable;
		}
		public void DtrEnable(bool enable)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if(_androidPlugin != null)
				_androidPlugin.Call("SetDTR", SerialPortHandle, enable);
#else
			spapSetDTR(SerialPortHandle, enable);
#endif
			DtrEnabled = enable;
		}
		public bool RtsGetStatus()
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if(_androidPlugin != null)
				return _androidPlugin.Call<bool>("GetRTS", SerialPortHandle);
			return false;
#else
			return spapGetRTS(SerialPortHandle);
#endif
		}
		public bool DtrGetStatus()
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if(_androidPlugin != null)
				return _androidPlugin.Call<bool>("GetDTR", SerialPortHandle);
			return false;
#else
			return spapGetDTR(SerialPortHandle);
#endif
		}
		public bool CtsHolding()
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if(_androidPlugin != null)
				return _androidPlugin.Call<bool>("GetCTS", SerialPortHandle);
			return false;
#else
			return spapGetCTS(SerialPortHandle);
#endif
		}
		public bool DsrHolding()
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if(_androidPlugin != null)
				return _androidPlugin.Call<bool>("GetDSR", SerialPortHandle);
			return false;
#else
			return spapGetDSR(SerialPortHandle);
#endif
		}

		public bool IsConnected()
		{
			return DsrHolding();
		}

		public void SetBreakSignal(bool enable)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if(_androidPlugin != null)
				_androidPlugin.Call("BreakSignal", SerialPortHandle, enable);
#else
			spapBreakSignal(SerialPortHandle, enable);
#endif
		}

		private void ExternalConfigApply(string filePath)
		{
			//The variable of this object is read and reflected in an external file before Open().
			if (filePath == string.Empty)
				return;

			string fileSetPath = "";

			switch (this.ExternalConfigPath)
			{
				case SourcePath.RelativeToDataFolder: fileSetPath += Application.dataPath;
					break;
				case SourcePath.RelativeToPeristentDataFolder: fileSetPath += Application.persistentDataPath;
					break;
				case SourcePath.RelativeToStreamingAssetsFolder: fileSetPath += Application.streamingAssetsPath;
					break;
				case SourcePath.AbsolutePath:
				default:
					break;
			}

			fileSetPath += "/" + filePath;

			if (!System.IO.File.Exists(fileSetPath))
				return;

			Debug.Log(string.Format("The set of the external file was applied. : {0}",fileSetPath));

			System.IO.StreamReader sr = new System.IO.StreamReader(fileSetPath, Encoding.UTF8);

			while (sr.Peek() != -1)
			{
				string ld = sr.ReadLine();
				ld = ld.Replace("\t", "").Replace(" ", "");	//tab & space delete
				ld = ld.Split('#')[0];	//commnet
				if (ld.Length <= 0)
					continue;	//igune

				string[] str = ld.Split("=".ToCharArray(), System.StringSplitOptions.None);

				if (str.Length != 2)
					continue;	//igune

				System.Type t = this.GetType();
				foreach (System.Reflection.PropertyInfo p in t.GetProperties())
				{
					if (p.Name == str[0])
					{
						try
						{
							if(p.PropertyType == typeof(string))
							{
								p.SetValue(this.GetComponent(t), str[1], null);
								Debug.Log(string.Format("ExternalConfig Apply! {0} = {1}", str[0], str[1]));
							}
						}
						catch (System.ArgumentException)
						{
							Debug.LogError(string.Format("ExternalConfig Error! {0}", str[0]));
						}
					}
				}

				foreach (System.Reflection.FieldInfo f in t.GetFields())
				{
					if (f.Name == str[0])
					{
						try
						{
							if (f.FieldType == typeof(string))
							{
								f.SetValue(this.GetComponent(t), str[1]);
								Debug.Log(string.Format("ExternalConfig Apply!{0} = {1}", str[0], str[1]));
							}
							else if (f.FieldType == typeof(float))
							{
								f.SetValue(this.GetComponent(t), float.Parse(str[1]));
								Debug.Log(string.Format("ExternalConfig Apply!{0} = {1}", str[0], str[1]));
							}
							else if (f.FieldType == typeof(bool))
							{
								f.SetValue(this.GetComponent(t), bool.Parse(str[1]));
								Debug.Log(string.Format("ExternalConfig Apply!{0} = {1}", str[0], str[1]));
							}
							else if (f.FieldType == typeof(int))
							{
								f.SetValue(this.GetComponent(t), int.Parse(str[1]));
								Debug.Log(string.Format("ExternalConfig Apply!{0} = {1}", str[0], str[1]));
							}
							else if (f.FieldType == typeof(ParityEnum))
							{
								ParityEnum ve = (ParityEnum)int.Parse(str[1]);
								f.SetValue(this.GetComponent(t), ve);
								Debug.Log(string.Format("ExternalConfig Apply!{0} = {1}", str[0], str[1]));
							}
							else if (f.FieldType == typeof(StopBitEnum))
							{
								StopBitEnum ve = (StopBitEnum)int.Parse(str[1]);
								f.SetValue(this.GetComponent(t), ve);
								Debug.Log(string.Format("ExternalConfig Apply!{0} = {1}", str[0], str[1]));
							}
							else if (f.FieldType == typeof(DataBitEnum))
							{
								DataBitEnum ve = (DataBitEnum)int.Parse(str[1]);
								f.SetValue(this.GetComponent(t), ve);
								Debug.Log(string.Format("ExternalConfig Apply!{0} = {1}", str[0], str[1]));
							}
						}
						catch (System.ArgumentException)
						{
							Debug.LogError(string.Format("ExternalConfig Error! {0}", str[0]));
						}
					}
				}
			}

			sr.Close();
		}

        //DeviceList
        public static DeviceInfo[] GetConnectedDeviceList(OpenSystem device_format)
        {
            DeviceInfo[] deviceInfo = null;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (device_format == OpenSystem.USB) {
                AndroidJavaObject androidPlugin = new AndroidJavaObject("com.wizapply.libspap.spap");
                // Get Context(Activity) Object
                AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject unityContext = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                if (androidPlugin != null) {
                    string usbList = androidPlugin.CallStatic<string>("GetUSBConnection", unityContext);
                    string[] deviceKind = usbList.Split(';');
                    deviceInfo = new DeviceInfo[deviceKind.Length];
                    int device_i = 0;

                    foreach (string ds in deviceKind)
                    {
                        string[] datu = ds.Split(',');
                        deviceInfo[device_i].Vendor = datu[0];    //VID
                        deviceInfo[device_i].Product = datu[1];    //PID
                        deviceInfo[device_i].SerialNumber = datu[2];    //SerialNumber
                        device_i++;
                    }
                }
            }
			else if (device_format == OpenSystem.BluetoothSSP)
			{
			    AndroidJavaObject androidPlugin = new AndroidJavaObject("com.wizapply.libspap.spap");
                // Get Context(Activity) Object
                AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject unityContext = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                if (androidPlugin != null) {
                    string usbList = androidPlugin.CallStatic<string>("GetBluetoothConnection", unityContext);
                    string[] deviceKind = usbList.Split(';');
                    deviceInfo = new DeviceInfo[deviceKind.Length];
                    int device_i = 0;

                    foreach (string ds in deviceKind)
                    {
                        deviceInfo[device_i].SerialNumber = ds;    //SerialNumber
                        device_i++;
                    }
                }
			}
            else
            {
                Debug.LogError("GetConnectedDeviceList is NOT SUPPORTED!");
            }
#else
			int deviceNum = spapDeviceListAvailable();
            System.Text.StringBuilder[] deviceString = new System.Text.StringBuilder[deviceNum];
            int[] deviceKind = new int[deviceNum];
            for (int i = 0; i < deviceNum; i++)
            {
                deviceString[i] = new System.Text.StringBuilder(1024);
                deviceKind[i] = spapDeviceList(i, deviceString[i], 1024);
            }

            //length
            int deviceInfoNum = 0;
            for (int i = 0; i < deviceNum; i++)
            {
                int openMethod = (int)device_format;
                string[] datu = deviceString[i].ToString().Split(',');
                if (openMethod == deviceKind[i])
                {
                    if (datu[0] == "null")
                        continue;

                    deviceInfoNum++;
                }
            }

            int device_i = 0;
            deviceInfo = new DeviceInfo[deviceInfoNum];
            for (int i = 0; i < deviceNum; i++)
            {
                int openMethod = (int)device_format;
                string[] datu = deviceString[i].ToString().Split(',');
                if (openMethod == deviceKind[i])
                {
                    if (datu[0] == "null")
                        continue;

                    switch (device_format)
                    {
                        case SerialPortUtilityPro.OpenSystem.USB:
                            deviceInfo[device_i] = new DeviceInfo();
                            deviceInfo[device_i].Vendor = datu[0];
                            deviceInfo[device_i].Product = datu[1];
                            deviceInfo[device_i].SerialNumber = datu[2];
                            break;
                        case SerialPortUtilityPro.OpenSystem.PCI:
                            deviceInfo[device_i] = new DeviceInfo();
                            deviceInfo[device_i].Vendor = datu[0];
                            deviceInfo[device_i].Product = datu[1];
                            deviceInfo[device_i].SerialNumber = "";
                            break;
                        case SerialPortUtilityPro.OpenSystem.BluetoothSSP:
                            deviceInfo[device_i] = new DeviceInfo();
                            deviceInfo[device_i].Vendor = "";
                            deviceInfo[device_i].Product = "";
                            deviceInfo[device_i].SerialNumber = datu[0];
                            break;
                    }

                    device_i++;
                }
            }

#endif
            return deviceInfo;
        }
	}
}