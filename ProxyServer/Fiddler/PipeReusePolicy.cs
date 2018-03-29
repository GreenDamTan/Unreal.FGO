using System;
namespace Fiddler
{
	public enum PipeReusePolicy
	{
		NoRestrictions,
		MarriedToClientProcess,
		MarriedToClientPipe,
		NoReuse
	}
}
