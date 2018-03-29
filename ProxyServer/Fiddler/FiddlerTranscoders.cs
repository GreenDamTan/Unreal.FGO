using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Policy;
using System.Text;
namespace Fiddler
{
	public class FiddlerTranscoders : IDisposable
	{
		internal Dictionary<string, TranscoderTuple> m_Importers = new Dictionary<string, TranscoderTuple>();
		internal Dictionary<string, TranscoderTuple> m_Exporters = new Dictionary<string, TranscoderTuple>();
		internal bool hasImporters
		{
			get
			{
				return this.m_Importers != null && this.m_Importers.Count > 0;
			}
		}
		internal bool hasExporters
		{
			get
			{
				return this.m_Exporters != null && this.m_Exporters.Count > 0;
			}
		}
		internal FiddlerTranscoders()
		{
		}
		internal string[] getImportFormats()
		{
			this.EnsureTranscoders();
			if (!this.hasImporters)
			{
				return new string[0];
			}
			string[] array = new string[this.m_Importers.Count];
			this.m_Importers.Keys.CopyTo(array, 0);
			return array;
		}
		internal string[] getExportFormats()
		{
			this.EnsureTranscoders();
			if (!this.hasExporters)
			{
				return new string[0];
			}
			string[] array = new string[this.m_Exporters.Count];
			this.m_Exporters.Keys.CopyTo(array, 0);
			return array;
		}
		public override string ToString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("IMPORT FORMATS");
			string[] importFormats = this.getImportFormats();
			for (int i = 0; i < importFormats.Length; i++)
			{
				string arg = importFormats[i];
				stringBuilder.AppendFormat("\t{0}\n", arg);
			}
			stringBuilder.AppendLine("\nEXPORT FORMATS");
			string[] exportFormats = this.getExportFormats();
			for (int j = 0; j < exportFormats.Length; j++)
			{
				string arg2 = exportFormats[j];
				stringBuilder.AppendFormat("\t{0}\n", arg2);
			}
			return stringBuilder.ToString();
		}
		public bool ImportTranscoders(string sAssemblyPath)
		{
			Evidence evidence = Assembly.GetExecutingAssembly().Evidence;
			try
			{
				if (!File.Exists(sAssemblyPath))
				{
					bool result = false;
					return result;
				}
				Assembly assemblyInput;
				if (CONFIG.bRunningOnCLRv4)
				{
					assemblyInput = Assembly.LoadFrom(sAssemblyPath);
				}
				else
				{
					assemblyInput = Assembly.LoadFrom(sAssemblyPath, evidence);
				}
				if (!this.ScanAssemblyForTranscoders(assemblyInput))
				{
					bool result = false;
					return result;
				}
			}
			catch (Exception ex)
			{
				FiddlerApplication.Log.LogFormat("Failed to load Transcoders from {0}; exception {1}", new object[]
				{
					sAssemblyPath,
					ex.Message
				});
				bool result = false;
				return result;
			}
			return true;
		}
		public bool ImportTranscoders(Assembly assemblyInput)
		{
			try
			{
				if (!this.ScanAssemblyForTranscoders(assemblyInput))
				{
					bool result = false;
					return result;
				}
			}
			catch (Exception ex)
			{
				FiddlerApplication.Log.LogFormat("Failed to load Transcoders from {0}; exception {1}", new object[]
				{
					assemblyInput.Location,
					ex.Message
				});
				bool result = false;
				return result;
			}
			return true;
		}
		private void ScanPathForTranscoders(string sPath)
		{
			try
			{
				if (Directory.Exists(sPath))
				{
					Evidence evidence = Assembly.GetExecutingAssembly().Evidence;
					bool boolPref;
					if (boolPref = FiddlerApplication.Prefs.GetBoolPref("fiddler.debug.extensions.verbose", false))
					{
						FiddlerApplication.Log.LogFormat("Searching for Transcoders under {0}", new object[]
						{
							sPath
						});
					}
					FileInfo[] files = new DirectoryInfo(sPath).GetFiles("*.dll");
					FileInfo[] array = files;
					for (int i = 0; i < array.Length; i++)
					{
						FileInfo fileInfo = array[i];
						if (!Utilities.IsNotExtension(fileInfo.Name))
						{
							if (boolPref)
							{
								FiddlerApplication.Log.LogFormat("Looking for Transcoders inside {0}", new object[]
								{
									fileInfo.FullName.ToString()
								});
							}
							Assembly assemblyInput;
							try
							{
								if (CONFIG.bRunningOnCLRv4)
								{
									assemblyInput = Assembly.LoadFrom(fileInfo.FullName);
								}
								else
								{
									assemblyInput = Assembly.LoadFrom(fileInfo.FullName, evidence);
								}
							}
							catch (Exception eX)
							{
								FiddlerApplication.LogAddonException(eX, "Failed to load " + fileInfo.FullName);
								goto IL_F1;
							}
							this.ScanAssemblyForTranscoders(assemblyInput);
						}
						IL_F1:;
					}
				}
			}
			catch (Exception ex)
			{
				FiddlerApplication.DoNotifyUser(string.Format("[Fiddler] Failure loading Transcoders: {0}", ex.Message), "Transcoders Load Error");
			}
		}
		private bool ScanAssemblyForTranscoders(Assembly assemblyInput)
		{
			bool result = false;
			bool boolPref = FiddlerApplication.Prefs.GetBoolPref("fiddler.debug.extensions.verbose", false);
			try
			{
				if (!Utilities.FiddlerMeetsVersionRequirement(assemblyInput, "Importers and Exporters"))
				{
					FiddlerApplication.Log.LogFormat("Assembly {0} did not specify a RequiredVersionAttribute. Aborting load of transcoders.", new object[]
					{
						assemblyInput.CodeBase
					});
					bool result2 = false;
					return result2;
				}
				Type[] exportedTypes = assemblyInput.GetExportedTypes();
				for (int i = 0; i < exportedTypes.Length; i++)
				{
					Type type = exportedTypes[i];
					if (!type.IsAbstract && type.IsPublic && type.IsClass)
					{
						if (!typeof(ISessionImporter).IsAssignableFrom(type))
						{
							goto IL_235;
						}
						try
						{
							if (!FiddlerTranscoders.AddToImportOrExportCollection(this.m_Importers, type))
							{
								FiddlerApplication.Log.LogFormat("WARNING: SessionImporter {0} from {1} failed to specify any ImportExportFormat attributes.", new object[]
								{
									type.Name,
									assemblyInput.CodeBase
								});
							}
							else
							{
								result = true;
								if (boolPref)
								{
									FiddlerApplication.Log.LogFormat("    Added SessionImporter {0}", new object[]
									{
										type.FullName
									});
								}
							}
							goto IL_235;
						}
						catch (Exception ex)
						{
							FiddlerApplication.DoNotifyUser(string.Format("[Fiddler] Failure loading {0} SessionImporter from {1}: {2}\n\n{3}\n\n{4}", new object[]
							{
								type.Name,
								assemblyInput.CodeBase,
								ex.Message,
								ex.StackTrace,
								ex.InnerException
							}), "Extension Load Error");
							goto IL_235;
						}
						IL_167:
						try
						{
							if (!FiddlerTranscoders.AddToImportOrExportCollection(this.m_Exporters, type))
							{
								FiddlerApplication.Log.LogFormat("WARNING: SessionExporter {0} from {1} failed to specify any ImportExportFormat attributes.", new object[]
								{
									type.Name,
									assemblyInput.CodeBase
								});
							}
							else
							{
								result = true;
								if (boolPref)
								{
									FiddlerApplication.Log.LogFormat("    Added SessionExporter {0}", new object[]
									{
										type.FullName
									});
								}
							}
						}
						catch (Exception ex2)
						{
							FiddlerApplication.DoNotifyUser(string.Format("[Fiddler] Failure loading {0} SessionExporter from {1}: {2}\n\n{3}\n\n{4}", new object[]
							{
								type.Name,
								assemblyInput.CodeBase,
								ex2.Message,
								ex2.StackTrace,
								ex2.InnerException
							}), "Extension Load Error");
						}
						goto IL_22A;
						IL_235:
						if (typeof(ISessionExporter).IsAssignableFrom(type))
						{
							goto IL_167;
						}
					}
					IL_22A:;
				}
			}
			catch (Exception ex3)
			{
				FiddlerApplication.DoNotifyUser(string.Format("[Fiddler] Failure loading Importer/Exporter from {0}: {1}", assemblyInput.CodeBase, ex3.Message), "Extension Load Error");
				bool result2 = false;
				return result2;
			}
			return result;
		}
		private void EnsureTranscoders()
		{
		}
		public TranscoderTuple GetExporter(string sExportFormat)
		{
			this.EnsureTranscoders();
			if (this.m_Exporters == null)
			{
				return null;
			}
			TranscoderTuple result;
			if (!this.m_Exporters.TryGetValue(sExportFormat, out result))
			{
				return null;
			}
			return result;
		}
		public TranscoderTuple GetImporter(string sImportFormat)
		{
			this.EnsureTranscoders();
			if (this.m_Importers == null)
			{
				return null;
			}
			TranscoderTuple result;
			if (!this.m_Importers.TryGetValue(sImportFormat, out result))
			{
				return null;
			}
			return result;
		}
		private static bool AddToImportOrExportCollection(Dictionary<string, TranscoderTuple> oCollection, Type type_0)
		{
			bool result = false;
			ProfferFormatAttribute[] array = (ProfferFormatAttribute[])Attribute.GetCustomAttributes(type_0, typeof(ProfferFormatAttribute));
			if (array != null && array.Length > 0)
			{
				result = true;
				ProfferFormatAttribute[] array2 = array;
				for (int i = 0; i < array2.Length; i++)
				{
					ProfferFormatAttribute profferFormatAttribute = array2[i];
					if (!oCollection.ContainsKey(profferFormatAttribute.FormatName))
					{
						oCollection.Add(profferFormatAttribute.FormatName, new TranscoderTuple(profferFormatAttribute.FormatDescription, type_0));
					}
				}
			}
			return result;
		}
		public void Dispose()
		{
			if (this.m_Exporters != null)
			{
				this.m_Exporters.Clear();
			}
			if (this.m_Importers != null)
			{
				this.m_Importers.Clear();
			}
			this.m_Importers = (this.m_Exporters = null);
		}
	}
}
