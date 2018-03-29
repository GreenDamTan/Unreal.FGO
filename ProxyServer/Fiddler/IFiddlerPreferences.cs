using System;
namespace Fiddler
{
	public interface IFiddlerPreferences
	{
		string this[string sName]
		{
			get;
			set;
		}
		void SetBoolPref(string sPrefName, bool bValue);
		void SetInt32Pref(string sPrefName, int iValue);
		void SetStringPref(string sPrefName, string sValue);
		bool GetBoolPref(string sPrefName, bool bDefault);
		string GetStringPref(string sPrefName, string sDefault);
		int GetInt32Pref(string sPrefName, int iDefault);
		void RemovePref(string sPrefName);
		PreferenceBag.PrefWatcher AddWatcher(string sPrefixFilter, EventHandler<PrefChangeEventArgs> pcehHandler);
		void RemoveWatcher(PreferenceBag.PrefWatcher wliToRemove);
	}
}
