using System;
namespace Fiddler
{
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event, Inherited = false, AllowMultiple = false)]
	public sealed class CodeDescription : Attribute
	{
		private string sDesc;
		public string Description
		{
			get
			{
				return this.sDesc;
			}
		}
		public CodeDescription(string desc)
		{
			this.sDesc = desc;
		}
	}
}
