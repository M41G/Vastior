// Vastior manager - modern Windows folder picker via the native IFileOpenDialog
// COM API (FOS_PICKFOLDERS), i.e. the Explorer-style dialog. No third-party
// dependencies. Falls back to the classic FolderBrowserDialog only if the COM
// dialog is unavailable. Returns the chosen path, or null if the user cancels.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Vastior
{
	static class VastiorFolderPicker
	{
		const uint FOS_PICKFOLDERS = 0x00000020;
		const uint FOS_FORCEFILESYSTEM = 0x00000040;
		const uint SIGDN_FILESYSPATH = 0x80058000;

		static readonly Guid ClsidFileOpenDialog = new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7");
		static readonly Guid IidShellItem = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");

		public static string PickFolder(IntPtr owner, string title, string initialDir)
		{
			try
			{
				return PickModern(owner, title, initialDir);
			}
			catch
			{
				// Any COM failure (very old Windows, locked-down host) -> classic dialog.
				return PickLegacy(title, initialDir);
			}
		}

		static string PickModern(IntPtr owner, string title, string initialDir)
		{
			IFileOpenDialog dialog = (IFileOpenDialog)Activator.CreateInstance(Type.GetTypeFromCLSID(ClsidFileOpenDialog));
			try
			{
				uint options;
				dialog.GetOptions(out options);
				dialog.SetOptions(options | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM);

				if (!string.IsNullOrEmpty(title))
				{
					dialog.SetTitle(title);
				}
				if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir))
				{
					IShellItem start;
					SHCreateItemFromParsingName(initialDir, IntPtr.Zero, IidShellItem, out start);
					if (start != null)
					{
						dialog.SetFolder(start);
						Marshal.ReleaseComObject(start);
					}
				}

				int hr = dialog.Show(owner);
				if (hr != 0)
				{
					return null; // user cancelled (or non-success HRESULT)
				}

				IShellItem result;
				dialog.GetResult(out result);
				string path;
				result.GetDisplayName(SIGDN_FILESYSPATH, out path);
				Marshal.ReleaseComObject(result);
				return path;
			}
			finally
			{
				Marshal.ReleaseComObject(dialog);
			}
		}

		static string PickLegacy(string title, string initialDir)
		{
			using (FolderBrowserDialog d = new FolderBrowserDialog())
			{
				d.Description = title;
				if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir))
				{
					d.SelectedPath = initialDir;
				}
				return d.ShowDialog() == DialogResult.OK ? d.SelectedPath : null;
			}
		}

		[DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
		static extern void SHCreateItemFromParsingName(
			[MarshalAs(UnmanagedType.LPWStr)] string path,
			IntPtr pbc,
			[MarshalAs(UnmanagedType.LPStruct)] Guid riid,
			[MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

		// Minimal COM declarations. Unused vtable slots are reserved as parameter-
		// less stubs so the methods we DO call land on the correct slot.
		[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("D57C7288-D4AD-4768-BE02-9D969532D960")]
		interface IFileOpenDialog
		{
			[PreserveSig] int Show(IntPtr parent);          // IModalWindow
			void SetFileTypes();
			void SetFileTypeIndex();
			void GetFileTypeIndex();
			void Advise();
			void Unadvise();
			void SetOptions(uint fos);
			void GetOptions(out uint fos);
			void SetDefaultFolder();
			void SetFolder([MarshalAs(UnmanagedType.Interface)] IShellItem psi);
			void GetFolder();
			void GetCurrentSelection();
			void SetFileName();
			void GetFileName();
			void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
			void SetOkButtonLabel();
			void SetFileNameLabel();
			void GetResult([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);
			void AddPlace();
			void SetDefaultExtension();
			void Close();
			void SetClientGuid();
			void ClearClientData();
			void SetFilter();
		}

		[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
		interface IShellItem
		{
			void BindToHandler();
			void GetParent();
			void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
			void GetAttributes();
			void Compare();
		}
	}
}
