using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
namespace Fiddler
{
	public class PreferenceBag : IFiddlerPreferences
	{
		public struct PrefWatcher
		{
			internal readonly EventHandler<PrefChangeEventArgs> fnToNotify;
			internal readonly string sPrefixToWatch;
			internal PrefWatcher(string sPrefixFilter, EventHandler<PrefChangeEventArgs> fnHandler)
			{
				this.sPrefixToWatch = sPrefixFilter;
				this.fnToNotify = fnHandler;
			}
		}
		private readonly StringDictionary _dictPrefs = new StringDictionary();
		private readonly List<PreferenceBag.PrefWatcher> _listWatchers = new List<PreferenceBag.PrefWatcher>();
		private readonly ReaderWriterLock _RWLockPrefs = new ReaderWriterLock();
		private readonly ReaderWriterLock _RWLockWatchers = new ReaderWriterLock();
		private string _sRegistryPath;
		private string _sCurrentProfile = ".default";
		private static char[] _arrForbiddenChars = new char[]
		{
			'*',
			' ',
			'$',
			'%',
			'@',
			'?',
			'!'
		};
		public string CurrentProfile
		{
			get
			{
				return this._sCurrentProfile;
			}
		}
		public string this[string sPrefName]
		{
			get
			{
				string result;
				try
				{
					PreferenceBag.GetReaderLock(this._RWLockPrefs);
					result = this._dictPrefs[sPrefName];
				}
				finally
				{
					PreferenceBag.FreeReaderLock(this._RWLockPrefs);
				}
				return result;
			}
			set
			{
				if (!PreferenceBag.isValidName(sPrefName))
				{
					throw new ArgumentException(string.Format("Preference name must contain 1 to 255 characters from the set A-z0-9-_ and may not contain the word Internal.\n\nCaller tried to set: \"{0}\"", sPrefName));
				}
				if (value == null)
				{
					this.RemovePref(sPrefName);
					return;
				}
				bool flag = false;
				try
				{
					PreferenceBag.GetWriterLock(this._RWLockPrefs);
					if (value != this._dictPrefs[sPrefName])
					{
						flag = true;
						this._dictPrefs[sPrefName] = value;
					}
				}
				finally
				{
					PreferenceBag.FreeWriterLock(this._RWLockPrefs);
				}
				if (flag)
				{
					PrefChangeEventArgs oNotifyArgs = new PrefChangeEventArgs(sPrefName, value);
					this.AsyncNotifyWatchers(oNotifyArgs);
				}
			}
		}
		private static void GetReaderLock(ReaderWriterLock oLock)
		{
			oLock.AcquireReaderLock(-1);
		}
		private static void FreeReaderLock(ReaderWriterLock oLock)
		{
			oLock.ReleaseReaderLock();
		}
		private static void GetWriterLock(ReaderWriterLock oLock)
		{
			oLock.AcquireWriterLock(-1);
		}
		private static void FreeWriterLock(ReaderWriterLock oLock)
		{
			oLock.ReleaseWriterLock();
		}
		internal PreferenceBag(string sRegPath)
		{
			this._sRegistryPath = sRegPath;
			this.ReadRegistry();
		}
		public static bool isValidName(string sName)
		{
			return !string.IsNullOrEmpty(sName) && 256 > sName.Length && !sName.OICContains("internal") && 0 > sName.IndexOfAny(PreferenceBag._arrForbiddenChars);
		}
		private void ReadRegistry()
		{
			if (this._sRegistryPath == null)
			{
				return;
			}
			RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(this._sRegistryPath + "\\" + this._sCurrentProfile, RegistryKeyPermissionCheck.ReadSubTree, RegistryRights.ExecuteKey);
			if (registryKey == null)
			{
				return;
			}
			string[] valueNames = registryKey.GetValueNames();
			try
			{
				PreferenceBag.GetWriterLock(this._RWLockPrefs);
				string[] array = valueNames;
				for (int i = 0; i < array.Length; i++)
				{
					string text = array[i];
					if (text.Length >= 1 && !text.OICContains("ephemeral"))
					{
						try
						{
							this._dictPrefs[text] = (string)registryKey.GetValue(text, string.Empty);
						}
						catch (Exception)
						{
						}
					}
				}
			}
			finally
			{
				PreferenceBag.FreeWriterLock(this._RWLockPrefs);
				registryKey.Close();
			}
		}
		private void WriteRegistry()
		{
			if (CONFIG.bIsViewOnly)
			{
				return;
			}
			if (this._sRegistryPath == null)
			{
				return;
			}
			RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(this._sRegistryPath, RegistryKeyPermissionCheck.ReadWriteSubTree);
			if (registryKey == null)
			{
				return;
			}
			try
			{
				PreferenceBag.GetReaderLock(this._RWLockPrefs);
				registryKey.DeleteSubKey(this._sCurrentProfile, false);
				if (this._dictPrefs.Count >= 1)
				{
					registryKey = registryKey.CreateSubKey(this._sCurrentProfile, RegistryKeyPermissionCheck.ReadWriteSubTree);
					foreach (DictionaryEntry dictionaryEntry in this._dictPrefs)
					{
						string text = (string)dictionaryEntry.Key;
						if (!text.OICContains("ephemeral"))
						{
							registryKey.SetValue(text, dictionaryEntry.Value);
						}
					}
				}
			}
			finally
			{
				PreferenceBag.FreeReaderLock(this._RWLockPrefs);
				registryKey.Close();
			}
		}
		public string[] GetPrefArray()
		{
			string[] result;
			try
			{
				PreferenceBag.GetReaderLock(this._RWLockPrefs);
				string[] array = new string[this._dictPrefs.Count];
				this._dictPrefs.Keys.CopyTo(array, 0);
				result = array;
			}
			finally
			{
				PreferenceBag.FreeReaderLock(this._RWLockPrefs);
			}
			return result;
		}
		public string GetStringPref(string sPrefName, string sDefault)
		{
			string text = this[sPrefName];
			return text ?? sDefault;
		}
		public bool GetBoolPref(string sPrefName, bool bDefault)
		{
			string text = this[sPrefName];
			if (text == null)
			{
				return bDefault;
			}
			bool result;
			if (bool.TryParse(text, out result))
			{
				return result;
			}
			return bDefault;
		}
		public int GetInt32Pref(string sPrefName, int iDefault)
		{
			string text = this[sPrefName];
			if (text == null)
			{
				return iDefault;
			}
			int result;
			if (int.TryParse(text, out result))
			{
				return result;
			}
			return iDefault;
		}
		public void SetStringPref(string sPrefName, string sValue)
		{
			this[sPrefName] = sValue;
		}
		public void SetInt32Pref(string sPrefName, int iValue)
		{
			this[sPrefName] = iValue.ToString();
		}
		public void SetBoolPref(string sPrefName, bool bValue)
		{
			this[sPrefName] = bValue.ToString();
		}
		public void RemovePref(string sPrefName)
		{
			bool flag = false;
			try
			{
				PreferenceBag.GetWriterLock(this._RWLockPrefs);
				flag = this._dictPrefs.ContainsKey(sPrefName);
				this._dictPrefs.Remove(sPrefName);
			}
			finally
			{
				PreferenceBag.FreeWriterLock(this._RWLockPrefs);
			}
			if (flag)
			{
				PrefChangeEventArgs oNotifyArgs = new PrefChangeEventArgs(sPrefName, null);
				this.AsyncNotifyWatchers(oNotifyArgs);
			}
		}
		private void _clearWatchers()
		{
			PreferenceBag.GetWriterLock(this._RWLockWatchers);
			try
			{
				this._listWatchers.Clear();
			}
			finally
			{
				PreferenceBag.FreeWriterLock(this._RWLockWatchers);
			}
		}
		public void Close()
		{
			this._clearWatchers();
			this.WriteRegistry();
		}
		public override string ToString()
		{
			return this.ToString(true);
		}
		public string ToString(bool bVerbose)
		{
			StringBuilder stringBuilder = new StringBuilder(128);
			try
			{
				PreferenceBag.GetReaderLock(this._RWLockPrefs);
				stringBuilder.AppendFormat("PreferenceBag [{0} Preferences. {1} Watchers.]", this._dictPrefs.Count, this._listWatchers.Count);
				if (bVerbose)
				{
					stringBuilder.Append("\n");
					foreach (DictionaryEntry dictionaryEntry in this._dictPrefs)
					{
						stringBuilder.AppendFormat("{0}:\t{1}\n", dictionaryEntry.Key, dictionaryEntry.Value);
					}
				}
			}
			finally
			{
				PreferenceBag.FreeReaderLock(this._RWLockPrefs);
			}
			return stringBuilder.ToString();
		}
		internal string FindMatches(string sFilter)
		{
			StringBuilder stringBuilder = new StringBuilder(128);
			try
			{
				PreferenceBag.GetReaderLock(this._RWLockPrefs);
				foreach (DictionaryEntry dictionaryEntry in this._dictPrefs)
				{
					if (((string)dictionaryEntry.Key).OICContains(sFilter))
					{
						stringBuilder.AppendFormat("{0}:\t{1}\r\n", dictionaryEntry.Key, dictionaryEntry.Value);
					}
				}
			}
			finally
			{
				PreferenceBag.FreeReaderLock(this._RWLockPrefs);
			}
			return stringBuilder.ToString();
		}
		public PreferenceBag.PrefWatcher AddWatcher(string sPrefixFilter, EventHandler<PrefChangeEventArgs> pcehHandler)
		{
			PreferenceBag.PrefWatcher prefWatcher = new PreferenceBag.PrefWatcher(sPrefixFilter.ToLower(), pcehHandler);
			PreferenceBag.GetWriterLock(this._RWLockWatchers);
			try
			{
				this._listWatchers.Add(prefWatcher);
			}
			finally
			{
				PreferenceBag.FreeWriterLock(this._RWLockWatchers);
			}
			return prefWatcher;
		}
		public void RemoveWatcher(PreferenceBag.PrefWatcher wliToRemove)
		{
			PreferenceBag.GetWriterLock(this._RWLockWatchers);
			try
			{
				this._listWatchers.Remove(wliToRemove);
			}
			finally
			{
				PreferenceBag.FreeWriterLock(this._RWLockWatchers);
			}
		}
		private void _NotifyThreadExecute(object objThreadState)
		{
			PrefChangeEventArgs prefChangeEventArgs = (PrefChangeEventArgs)objThreadState;
			string prefName = prefChangeEventArgs.PrefName;
			List<EventHandler<PrefChangeEventArgs>> list = null;
			try
			{
				PreferenceBag.GetReaderLock(this._RWLockWatchers);
				try
				{
					foreach (PreferenceBag.PrefWatcher current in this._listWatchers)
					{
						if (prefName.OICStartsWith(current.sPrefixToWatch))
						{
							if (list == null)
							{
								list = new List<EventHandler<PrefChangeEventArgs>>();
							}
							list.Add(current.fnToNotify);
						}
					}
				}
				finally
				{
					PreferenceBag.FreeReaderLock(this._RWLockWatchers);
				}
				if (list != null)
				{
					foreach (EventHandler<PrefChangeEventArgs> current2 in list)
					{
						try
						{
							current2(this, prefChangeEventArgs);
						}
						catch (Exception eX)
						{
							FiddlerApplication.ReportException(eX);
						}
					}
				}
			}
			catch (Exception eX2)
			{
				FiddlerApplication.ReportException(eX2);
			}
		}
		private void AsyncNotifyWatchers(PrefChangeEventArgs oNotifyArgs)
		{
			ThreadPool.UnsafeQueueUserWorkItem(new WaitCallback(this._NotifyThreadExecute), oNotifyArgs);
		}
	}
}
