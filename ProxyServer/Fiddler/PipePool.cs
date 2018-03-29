using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
namespace Fiddler
{
	internal class PipePool
	{
		internal static uint MSEC_PIPE_POOLED_LIFETIME = 120000u;
		internal static uint MSEC_POOL_CLEANUP_INTERVAL = 30000u;
		private readonly Dictionary<string, Stack<ServerPipe>> thePool;
		private long lngLastPoolPurge;
		internal PipePool()
		{
			PipePool.MSEC_PIPE_POOLED_LIFETIME = (uint)FiddlerApplication.Prefs.GetInt32Pref("fiddler.network.timeouts.serverpipe.reuse", 120000);
			this.thePool = new Dictionary<string, Stack<ServerPipe>>();
			FiddlerApplication.Janitor.assignWork(new SimpleEventHandler(this.ScavengeCache), PipePool.MSEC_POOL_CLEANUP_INTERVAL);
		}
		internal void ScavengeCache()
		{
			if (this.thePool.Count < 1)
			{
				return;
			}
			List<ServerPipe> list = new List<ServerPipe>();
			Dictionary<string, Stack<ServerPipe>> obj;
			Monitor.Enter(obj = this.thePool);
			try
			{
				List<string> list2 = new List<string>();
				ulong num = Utilities.GetTickCount() - (ulong)PipePool.MSEC_PIPE_POOLED_LIFETIME;
				foreach (KeyValuePair<string, Stack<ServerPipe>> current in this.thePool)
				{
					Stack<ServerPipe> value = current.Value;
					Stack<ServerPipe> obj2;
					Monitor.Enter(obj2 = value);
					try
					{
						if (value.Count > 0)
						{
							ServerPipe serverPipe = value.Peek();
							if (serverPipe.ulLastPooled < num)
							{
								list.AddRange(value);
								value.Clear();
							}
							else
							{
								if (value.Count > 1)
								{
									ServerPipe[] array = value.ToArray();
									if (array[array.Length - 1].ulLastPooled < num)
									{
										value.Clear();
										for (int i = array.Length - 1; i >= 0; i--)
										{
											if (array[i].ulLastPooled < num)
											{
												list.Add(array[i]);
											}
											else
											{
												value.Push(array[i]);
											}
										}
									}
								}
							}
						}
						if (value.Count == 0)
						{
							list2.Add(current.Key);
						}
					}
					finally
					{
						Monitor.Exit(obj2);
					}
				}
				foreach (string current2 in list2)
				{
					this.thePool.Remove(current2);
				}
			}
			finally
			{
				Monitor.Exit(obj);
			}
			foreach (BasePipe current3 in list)
			{
				current3.End();
			}
		}
		internal void Clear()
		{
			this.lngLastPoolPurge = DateTime.Now.Ticks;
			if (this.thePool.Count < 1)
			{
				return;
			}
			List<ServerPipe> list = new List<ServerPipe>();
			Dictionary<string, Stack<ServerPipe>> obj;
			Monitor.Enter(obj = this.thePool);
			try
			{
				foreach (KeyValuePair<string, Stack<ServerPipe>> current in this.thePool)
				{
					Stack<ServerPipe> value;
					Monitor.Enter(value = current.Value);
					try
					{
						list.AddRange(current.Value);
					}
					finally
					{
						Monitor.Exit(value);
					}
				}
				this.thePool.Clear();
			}
			finally
			{
				Monitor.Exit(obj);
			}
			foreach (ServerPipe current2 in list)
			{
				current2.End();
			}
		}
		internal string InspectPool()
		{
			StringBuilder stringBuilder = new StringBuilder(8192);
			stringBuilder.AppendFormat("ServerPipePool\nfiddler.network.timeouts.serverpipe.reuse: {0}ms\nContents\n--------\n", PipePool.MSEC_PIPE_POOLED_LIFETIME);
			Dictionary<string, Stack<ServerPipe>> obj;
			Monitor.Enter(obj = this.thePool);
			try
			{
				foreach (string current in this.thePool.Keys)
				{
					Stack<ServerPipe> stack = this.thePool[current];
					stringBuilder.AppendFormat("\t[{0}] entries for '{1}'.\n", stack.Count, current);
					Stack<ServerPipe> obj2;
					Monitor.Enter(obj2 = stack);
					try
					{
						foreach (ServerPipe current2 in stack)
						{
							stringBuilder.AppendFormat("\t\t{0}\n", current2.ToString());
						}
					}
					finally
					{
						Monitor.Exit(obj2);
					}
				}
			}
			finally
			{
				Monitor.Exit(obj);
			}
			stringBuilder.Append("\n--------\n");
			return stringBuilder.ToString();
		}
		internal ServerPipe TakePipe(string sPoolKey, int iPID, int HackiForSession)
		{
			if (!CONFIG.bReuseServerSockets)
			{
				return null;
			}
			Dictionary<string, Stack<ServerPipe>> obj;
			Monitor.Enter(obj = this.thePool);
			Stack<ServerPipe> stack;
			try
			{
				if ((iPID == 0 || !this.thePool.TryGetValue(string.Format("PID{0}*{1}", iPID, sPoolKey), out stack) || stack.Count < 1) && (!this.thePool.TryGetValue(sPoolKey, out stack) || stack.Count < 1))
				{
					ServerPipe result = null;
					return result;
				}
			}
			finally
			{
				Monitor.Exit(obj);
			}
			Stack<ServerPipe> obj2;
			Monitor.Enter(obj2 = stack);
			ServerPipe result2;
			try
			{
				if (stack.Count == 0)
				{
					ServerPipe result = null;
					return result;
				}
				result2 = stack.Pop();
			}
			catch (Exception eX)
			{
				FiddlerApplication.ReportException(eX);
				ServerPipe result = null;
				return result;
			}
			finally
			{
				Monitor.Exit(obj2);
			}
			return result2;
		}
		internal void PoolOrClosePipe(ServerPipe oPipe)
		{
			if (!CONFIG.bReuseServerSockets)
			{
				oPipe.End();
				return;
			}
			if (oPipe.ReusePolicy != PipeReusePolicy.NoReuse)
			{
				if (oPipe.ReusePolicy != PipeReusePolicy.MarriedToClientPipe)
				{
					if (this.lngLastPoolPurge > oPipe.dtConnected.Ticks)
					{
						oPipe.End();
						return;
					}
					if (oPipe.sPoolKey != null && oPipe.sPoolKey.Length >= 2)
					{
						oPipe.ulLastPooled = Utilities.GetTickCount();
						Dictionary<string, Stack<ServerPipe>> obj;
						Monitor.Enter(obj = this.thePool);
						Stack<ServerPipe> stack;
						try
						{
							if (!this.thePool.TryGetValue(oPipe.sPoolKey, out stack))
							{
								stack = new Stack<ServerPipe>();
								this.thePool.Add(oPipe.sPoolKey, stack);
							}
						}
						finally
						{
							Monitor.Exit(obj);
						}
						Stack<ServerPipe> obj2;
						Monitor.Enter(obj2 = stack);
						try
						{
							stack.Push(oPipe);
						}
						finally
						{
							Monitor.Exit(obj2);
						}
						return;
					}
					oPipe.End();
					return;
				}
			}
			oPipe.End();
		}
	}
}
