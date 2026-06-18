// Vastior manager - in-process install / uninstall (no PowerShell, no scripts).
//
// Runs the full install / uninstall flow directly from the app, with these
// safeguards:
//   * Validates the Grim Dawn folder and that it is writable.
//   * Backs up saves first (local + Steam Cloud) to the Desktop, VERIFIES the
//     copy, and ABORTS the install if it does not verify - before any game file
//     is touched.
//   * Backs up any pre-existing non-Vastior file before overwriting it.
//   * Generates winmm_orig.dll from the copy of winmm already on this PC.
//   * Writes a JSON manifest so uninstall removes only what was installed and
//     restores anything it replaced; every manifest path is contained to the
//     game folder before use.
//
// It never reads, writes, or deletes characters, shared stash, saves, or Steam
// Cloud data (save backups are read-only copies and are never removed).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace Vastior
{
	static class VastiorInstaller
	{
		const string ModName = "Vastior";
		const string ModVersion = "1.0";
		const string AppId = "219990";
		const string ManifestName = "Vastior_install.txt";
		const string NotesName = "Vastior_Setup_Notes.txt";
		const string LogName = "Vastior_install.log";

		// Max directory recursion depth for the save backup (cycle / DoS guard).
		const int kMaxDirDepth = 64;

		sealed class PayloadItem
		{
			public readonly string Src, Dest;
			public readonly bool Overwrite, X64;
			public PayloadItem(string src, string dest, bool overwrite, bool x64)
			{
				Src = src; Dest = dest; Overwrite = overwrite; X64 = x64;
			}
		}

		static readonly PayloadItem[] Payload = new PayloadItem[]
		{
			new PayloadItem("Vastior.dll",     "Vastior.dll",     true,  false),
			new PayloadItem("winmm.dll",       "winmm.dll",       true,  false),
			new PayloadItem("Vastior.ini",     "Vastior.ini",     false, false),
			new PayloadItem("x64\\Vastior.dll", "x64\\Vastior.dll", true,  true),
			new PayloadItem("x64\\winmm.dll",   "x64\\winmm.dll",   true,  true)
		};

		static readonly string[] ModFiles =
		{
			"winmm.dll", "winmm_orig.dll", "Vastior.dll", "Vastior.ini",
			"x64\\winmm.dll", "x64\\winmm_orig.dll", "x64\\Vastior.dll"
		};
		static readonly string[] MetaFiles = { ManifestName, NotesName, LogName };

		static void Log(Action<string> cb, string level, string message)
		{
			if (cb != null)
			{
				cb("[" + level + "] " + message);
			}
		}

		// =============================================================== install
		public static bool Install(string gd, string appDir, Action<string> log)
		{
			return Install(gd, appDir, true, log);
		}

		// backupSaves is always true from the app; the parameter exists so the
		// automated test harness can exercise install without copying real saves.
		public static bool Install(string gd, string appDir, bool backupSaves, Action<string> log)
		{
			if (!IsGrimDawn(gd))
			{
				Log(log, "ERROR", "Not a Grim Dawn folder: " + gd);
				return false;
			}
			gd = Path.GetFullPath(gd);
			string payloadDir = Path.Combine(appDir, "files");
			bool hasX64 = File.Exists(Path.Combine(gd, "x64\\Grim Dawn.exe"));

			List<PayloadItem> targets = new List<PayloadItem>();
			foreach (PayloadItem it in Payload)
			{
				if (!it.X64 || hasX64)
				{
					targets.Add(it);
				}
			}
			foreach (PayloadItem it in targets)
			{
				if (!File.Exists(Path.Combine(payloadDir, it.Src)))
				{
					Log(log, "ERROR", "Missing payload file: " + it.Src + " (keep Vastior.exe with its files\\ folder).");
					return false;
				}
			}

			if (!TestWriteAccess(gd))
			{
				Log(log, "ERROR", "Grim Dawn folder is not writable. Re-run Vastior as administrator, or grant your");
				Log(log, "ERROR", "user Modify permission on:  " + gd);
				return false;
			}

			Log(log, "OK", "Grim Dawn folder: " + gd);
			Log(log, "INFO", ModName + " installer v" + ModVersion + " starting.");

			string sys32 = Sys32Winmm();
			if (sys32 == null)
			{
				Log(log, "ERROR", "Could not find the 32-bit system winmm.dll.");
				return false;
			}
			string sys64 = null;
			if (hasX64)
			{
				sys64 = Sys64Winmm();
				if (sys64 == null)
				{
					Log(log, "WARN", "64-bit system winmm not found - x64 client skipped.");
					hasX64 = false;
					targets.RemoveAll(delegate(PayloadItem it) { return it.X64; });
				}
			}

			string ts = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

			if (backupSaves)
			{
				Log(log, "STEP", "Backing up saves (precaution)...");
				string saveRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Grim Dawn Backups\\GD_Save_Backup_" + ts);
				if (!BackupSaves(saveRoot, log))
				{
					Log(log, "ERROR", "Save backup did not verify - install ABORTED (no game files changed).");
					Log(log, "ERROR", "Free disk space / fix permissions and try again.");
					return false;
				}
			}
			else
			{
				Log(log, "WARN", "Save backup skipped.");
			}

			string fileBackup = Path.Combine(gd, "_Vastior_backup_" + ts);
			List<string> installed = new List<string>();
			List<string[]> backedUp = new List<string[]>();   // [destRel, backupFullPath]
			bool priorRoot = File.Exists(Path.Combine(gd, "winmm_orig.dll"));
			bool priorX64 = File.Exists(Path.Combine(gd, "x64\\winmm_orig.dll"));

			try
			{
				foreach (PayloadItem it in targets)
				{
					Place(gd, Path.Combine(payloadDir, it.Src), it.Dest, it.Overwrite, fileBackup, priorRoot, priorX64, installed, backedUp, log);
				}
				PlaceOriginalWinmm(gd, sys32, "winmm_orig.dll", priorRoot, fileBackup, installed, backedUp, log);
				if (hasX64)
				{
					PlaceOriginalWinmm(gd, sys64, "x64\\winmm_orig.dll", priorX64, fileBackup, installed, backedUp, log);
				}
				WriteManifest(gd, hasX64, installed, backedUp, fileBackup, log);
				WriteNotes(gd, hasX64);
			}
			catch (Exception ex)
			{
				Log(log, "ERROR", "Install failed: " + ex.Message);
				Log(log, "INFO", "Use Uninstall to clean up anything that was partially placed.");
				return false;
			}

			Log(log, "OK", "DONE. " + ModName + (hasX64 ? " installed (32-bit + x64)." : " installed (32-bit)."));
			Log(log, "INFO", "Launch Grim Dawn normally; mouse-wheel to zoom.");
			return true;
		}

		static void Place(string gd, string srcFull, string destRel, bool overwrite, string fileBackup,
			bool priorRoot, bool priorX64, List<string> installed, List<string[]> backedUp, Action<string> log)
		{
			string dst = Path.Combine(gd, destRel);
			bool exists = File.Exists(dst);
			if (exists && !overwrite)
			{
				Log(log, "INFO", "Kept existing " + destRel + " (not overwritten).");
				return;
			}
			// One-pass: if this looks like a prior Vastior install, our own files
			// are not "originals" worth backing up.
			bool ours = destRel.StartsWith("x64\\", StringComparison.OrdinalIgnoreCase) ? priorX64 : priorRoot;
			if (exists && !ours)
			{
				string bk = Path.Combine(fileBackup, destRel);
				Directory.CreateDirectory(Path.GetDirectoryName(bk));
				File.Copy(dst, bk, true);
				backedUp.Add(new string[] { destRel, bk });
				Log(log, "INFO", "Backed up existing " + destRel);
			}
			Directory.CreateDirectory(Path.GetDirectoryName(dst));
			File.Copy(srcFull, dst, true);
			installed.Add(destRel);
			Log(log, "OK", "Installed " + destRel);
		}

		static void PlaceOriginalWinmm(string gd, string srcSys, string destRel, bool prior, string fileBackup,
			List<string> installed, List<string[]> backedUp, Action<string> log)
		{
			string dst = Path.Combine(gd, destRel);
			if (File.Exists(dst) && !prior)
			{
				string bk = Path.Combine(fileBackup, destRel);
				Directory.CreateDirectory(Path.GetDirectoryName(bk));
				File.Copy(dst, bk, true);
				backedUp.Add(new string[] { destRel, bk });
			}
			Directory.CreateDirectory(Path.GetDirectoryName(dst));
			File.Copy(srcSys, dst, true);
			installed.Add(destRel);
			Log(log, "OK", "Created " + destRel + " (from your system winmm).");
		}

		// The manifest is a plain, human-readable text file (no JSON library, no
		// external dependency) recording exactly what was installed so uninstall can
		// reverse it. Lines are key=value; installed files use "file=<relative>" and
		// overwrite backups use "backup=<relative>|<absolute backup path>"
		// ('|' is illegal in Windows paths, so it is a safe separator).
		static void WriteManifest(string gd, bool hasX64, List<string> installed, List<string[]> backedUp, string fileBackup, Action<string> log)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("# Vastior install manifest - records exactly what was installed.\r\n");
			sb.Append("# Used by uninstall. Do not edit.\r\n");
			sb.Append("mod=").Append(ModName).Append("\r\n");
			sb.Append("version=").Append(ModVersion).Append("\r\n");
			sb.Append("installedUtc=").Append(DateTime.UtcNow.ToString("o")).Append("\r\n");
			sb.Append("grimDawnPath=").Append(gd).Append("\r\n");
			sb.Append("x64=").Append(hasX64 ? "true" : "false").Append("\r\n");
			if (Directory.Exists(fileBackup))
			{
				sb.Append("overwriteBackupFolder=").Append(fileBackup).Append("\r\n");
			}
			foreach (string f in installed)
			{
				sb.Append("file=").Append(f).Append("\r\n");
			}
			foreach (string[] b in backedUp)
			{
				sb.Append("backup=").Append(b[0]).Append("|").Append(b[1]).Append("\r\n");
			}
			File.WriteAllText(Path.Combine(gd, ManifestName), sb.ToString(), new UTF8Encoding(false));
			Log(log, "OK", "Wrote install manifest.");
		}

		sealed class Manifest
		{
			public readonly List<string> InstalledFiles = new List<string>();
			public readonly List<string[]> OverwriteBackups = new List<string[]>();   // [relativeName, backupFullPath]
			public string OverwriteBackupFolder;
		}

		static Manifest ParseManifest(string path)
		{
			Manifest m = new Manifest();
			foreach (string raw in File.ReadAllLines(path))
			{
				string line = raw.Trim();
				if (line.Length == 0 || line[0] == '#')
				{
					continue;
				}
				int eq = line.IndexOf('=');
				if (eq <= 0)
				{
					continue;
				}
				string key = line.Substring(0, eq);
				string value = line.Substring(eq + 1);
				if (key == "file")
				{
					m.InstalledFiles.Add(value);
				}
				else if (key == "backup")
				{
					int bar = value.IndexOf('|');
					if (bar > 0)
					{
						m.OverwriteBackups.Add(new string[] { value.Substring(0, bar), value.Substring(bar + 1) });
					}
				}
				else if (key == "overwriteBackupFolder")
				{
					m.OverwriteBackupFolder = value;
				}
			}
			return m;
		}

		static void WriteNotes(string gd, bool hasX64)
		{
			string x64Line = hasX64 ? "  x64\\Vastior.dll, x64\\winmm.dll, x64\\winmm_orig.dll\r\n" : "";
			string notes =
				ModName + " v" + ModVersion + " - installed " + DateTime.Now + "\r\n\r\n" +
				"Wider camera zoom for Grim Dawn. Loads automatically - just launch the game.\r\n" +
				"Edit Vastior.ini in the game folder to tweak, then relaunch.\r\n\r\n" +
				"Installed files (game folder):\r\n" +
				"  winmm.dll, winmm_orig.dll, Vastior.dll, Vastior.ini\r\n" +
				x64Line + "\r\n" +
				"Saves are never touched. A precautionary copy is on your Desktop (\"Grim Dawn\r\n" +
				"Backups\"). Some antivirus may flag winmm.dll/Vastior.dll (false positive for a\r\n" +
				"camera mod) - allow them, or use Vastior's Uninstall button to remove everything.\r\n";
			File.WriteAllText(Path.Combine(gd, NotesName), notes, new UTF8Encoding(false));
		}

		// ============================================================= uninstall
		public static bool Uninstall(string gd, Action<string> log)
		{
			if (!IsGrimDawn(gd))
			{
				Log(log, "ERROR", "Not a Grim Dawn folder: " + gd);
				return false;
			}
			gd = Path.GetFullPath(gd);
			Log(log, "OK", "Grim Dawn folder: " + gd);

			string manPath = Path.Combine(gd, ManifestName);
			Manifest man = null;
			if (File.Exists(manPath))
			{
				try { man = ParseManifest(manPath); }
				catch { man = null; }
			}

			List<string> toRemove = new List<string>();
			if (man != null && man.InstalledFiles.Count > 0)
			{
				foreach (string n in man.InstalledFiles)
				{
					if (Array.IndexOf(ModFiles, n) >= 0)
					{
						toRemove.Add(n);
					}
				}
				Log(log, "INFO", "Using install manifest to remove exactly what was installed.");
			}
			else
			{
				Log(log, "WARN", "No manifest - removing the standard Vastior files present.");
				foreach (string n in ModFiles)
				{
					string full = Path.Combine(gd, n);
					if (!File.Exists(full))
					{
						continue;
					}
					// Never remove a winmm.dll that has no winmm_orig.dll beside it:
					// that would be the real system library, not our proxy.
					if (string.Equals(Path.GetFileName(n), "winmm.dll", StringComparison.OrdinalIgnoreCase))
					{
						string origBeside = Path.Combine(Path.GetDirectoryName(full), "winmm_orig.dll");
						if (!File.Exists(origBeside))
						{
							Log(log, "WARN", "Leaving " + n + " (no winmm_orig.dll beside it).");
							continue;
						}
					}
					toRemove.Add(n);
				}
			}

			if (toRemove.Count == 0 && !File.Exists(manPath))
			{
				Log(log, "OK", ModName + " does not appear to be installed here.");
				return true;
			}

			foreach (string n in toRemove)
			{
				RemoveOne(Path.Combine(gd, n), n, log);
			}

			RestoreOverwriteBackups(gd, man, log);
			RemoveInstallBackupFolder(gd, man, log);

			foreach (string m in MetaFiles)
			{
				RemoveOne(Path.Combine(gd, m), m, log);
			}

			Log(log, "OK", "DONE. " + ModName + " removed - game is back to stock.");
			Log(log, "INFO", "Your save backups on the Desktop were left untouched.");
			return true;
		}

		static void RestoreOverwriteBackups(string gd, Manifest man, Action<string> log)
		{
			if (man == null)
			{
				return;
			}
			foreach (string[] entry in man.OverwriteBackups)
			{
				string name = entry[0];
				string backup = entry[1];
				if (string.IsNullOrEmpty(backup) || !File.Exists(backup))
				{
					continue;
				}
				if (!TestSafeRel(name))
				{
					Log(log, "WARN", "Skipping unsafe manifest path: " + name);
					continue;
				}
				if (!TestBackupSource(gd, backup))
				{
					Log(log, "WARN", "Refusing to restore from an unsafe backup path.");
					continue;
				}
				string dst = Path.Combine(gd, name);
				if (!TestInside(gd, dst))
				{
					Log(log, "WARN", "Refusing to restore outside the game folder.");
					continue;
				}
				if (!NoReparseCrossing(gd, dst))
				{
					Log(log, "WARN", "Refusing to restore through a junction/symlink.");
					continue;
				}
				Directory.CreateDirectory(Path.GetDirectoryName(dst));
				File.Copy(backup, dst, true);
				Log(log, "OK", "Restored original " + name);
			}
		}

		static void RemoveInstallBackupFolder(string gd, Manifest man, Action<string> log)
		{
			if (man == null)
			{
				return;
			}
			string bf = man.OverwriteBackupFolder;
			if (string.IsNullOrEmpty(bf))
			{
				return;
			}
			if (Directory.Exists(bf) && TestInside(gd, bf) && NoReparseCrossing(gd, bf) && Path.GetFileName(bf).StartsWith("_Vastior_backup_", StringComparison.OrdinalIgnoreCase))
			{
				try { Directory.Delete(bf, true); Log(log, "OK", "Removed install-backup folder."); }
				catch (Exception ex) { Log(log, "WARN", "Could not remove backup folder: " + ex.Message); }
			}
			else
			{
				Log(log, "WARN", "Refusing to remove backup folder (failed containment check).");
			}
		}

		// ================================================================ helpers
		static bool IsGrimDawn(string gd)
		{
			if (string.IsNullOrEmpty(gd) || !Directory.Exists(gd))
			{
				return false;
			}
			try { return File.Exists(Path.Combine(gd, "Grim Dawn.exe")) || File.Exists(Path.Combine(gd, "x64\\Grim Dawn.exe")); }
			catch { return false; }
		}

		static bool TestWriteAccess(string dir)
		{
			try
			{
				string probe = Path.Combine(dir, ".v_" + Guid.NewGuid().ToString("N") + ".tmp");
				File.WriteAllText(probe, "x");
				File.Delete(probe);
				return true;
			}
			catch { return false; }
		}

		// 32-bit winmm: SysWOW64 on a 64-bit OS, otherwise System32 (32-bit OS).
		static string Sys32Winmm()
		{
			string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
			string[] candidates = { Path.Combine(win, "SysWOW64\\winmm.dll"), Path.Combine(win, "System32\\winmm.dll") };
			foreach (string p in candidates)
			{
				if (File.Exists(p))
				{
					return p;
				}
			}
			return null;
		}

		// 64-bit winmm: System32 for a 64-bit process; Sysnative dodges WOW64
		// redirection for a 32-bit process. Null on a 32-bit OS.
		static string Sys64Winmm()
		{
			if (!Environment.Is64BitOperatingSystem)
			{
				return null;
			}
			string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
			string p = Environment.Is64BitProcess ? Path.Combine(win, "System32\\winmm.dll") : Path.Combine(win, "Sysnative\\winmm.dll");
			return File.Exists(p) ? p : null;
		}

		static void RemoveOne(string full, string label, Action<string> log)
		{
			if (!File.Exists(full))
			{
				return;
			}
			try { File.Delete(full); Log(log, "OK", "Removed " + label); }
			catch (Exception ex) { Log(log, "WARN", "Could not remove " + label + ": " + ex.Message); }
		}

		// Reject rooted paths and any ".." segment (manifest path containment).
		static bool TestSafeRel(string rel)
		{
			if (string.IsNullOrEmpty(rel) || Path.IsPathRooted(rel))
			{
				return false;
			}
			foreach (string seg in rel.Replace('/', '\\').Split('\\'))
			{
				if (seg == "..")
				{
					return false;
				}
			}
			return true;
		}

		static bool TestBackupSource(string root, string path)
		{
			if (string.IsNullOrEmpty(path) || !TestInside(root, path))
			{
				return false;
			}
			string relative = Path.GetFullPath(path).Substring((Path.GetFullPath(root).TrimEnd('\\') + "\\").Length);
			string firstSegment = relative.Split('\\')[0];
			return firstSegment.StartsWith("_Vastior_backup_", StringComparison.OrdinalIgnoreCase);
		}

		static bool TestInside(string root, string path)
		{
			try
			{
				string r = Path.GetFullPath(root).TrimEnd('\\') + "\\";
				string fp = Path.GetFullPath(path);
				return fp.StartsWith(r, StringComparison.OrdinalIgnoreCase);
			}
			catch { return false; }
		}

		static bool IsReparsePoint(string path)
		{
			try
			{
				if (!File.Exists(path) && !Directory.Exists(path))
				{
					return false;
				}
				return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
			}
			catch { return false; }
		}

		// True only if `target` can be reached from `root` without crossing a
		// junction/symlink, so a planted reparse point cannot redirect a write or
		// delete to land outside the game folder (TestInside is string-prefix only).
		static bool NoReparseCrossing(string root, string target)
		{
			try
			{
				string r = Path.GetFullPath(root).TrimEnd('\\');
				string cur = Path.GetFullPath(target);
				while (cur.Length > r.Length && cur.StartsWith(r, StringComparison.OrdinalIgnoreCase))
				{
					if (IsReparsePoint(cur))
					{
						return false;
					}
					string parent = Path.GetDirectoryName(cur);
					if (string.IsNullOrEmpty(parent) || string.Equals(parent, cur, StringComparison.OrdinalIgnoreCase))
					{
						break;
					}
					cur = parent;
				}
				return true;
			}
			catch { return false; }
		}

		// ---- save backup (read-only copy + verify) ----------------------------
		static bool BackupSaves(string backupRoot, Action<string> log)
		{
			List<string[]> sources = new List<string[]>();   // [label, path]

			string local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games\\Grim Dawn\\save");
			if (Directory.Exists(local))
			{
				sources.Add(new string[] { "Local", local });
			}
			try
			{
				RegistryKey k = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam");
				if (k != null)
				{
					string sp = k.GetValue("SteamPath") as string;
					if (!string.IsNullOrEmpty(sp))
					{
						string ud = Path.Combine(sp.Replace('/', '\\'), "userdata");
						if (Directory.Exists(ud))
						{
							foreach (string acct in Directory.GetDirectories(ud))
							{
								string remote = Path.Combine(acct, AppId + "\\remote");
								if (Directory.Exists(remote))
								{
									sources.Add(new string[] { "SteamCloud_" + Path.GetFileName(acct), remote });
								}
							}
						}
					}
				}
			}
			catch { }

			if (sources.Count == 0)
			{
				Log(log, "WARN", "No save folders found to back up.");
				return true;
			}

			bool ok = true;
			foreach (string[] s in sources)
			{
				string dest = Path.Combine(backupRoot, s[0]);
				try
				{
					CopyDir(s[1], dest);
					int srcCount; long srcBytes; DirStats(s[1], out srcCount, out srcBytes);
					int dstCount; long dstBytes; DirStats(dest, out dstCount, out dstBytes);
					if (srcCount == dstCount && srcBytes == dstBytes)
					{
						Log(log, "OK", "Verified save backup: " + s[0] + " (" + dstCount + " files)");
					}
					else
					{
						Log(log, "ERROR", "Save backup INCOMPLETE for " + s[0]);
						ok = false;
					}
				}
				catch (Exception ex)
				{
					Log(log, "ERROR", "Save backup failed for " + s[0] + ": " + ex.Message);
					ok = false;
				}
			}
			return ok;
		}

		static void CopyDir(string source, string dest)
		{
			CopyDir(source, dest, 0);
		}

		static void CopyDir(string source, string dest, int depth)
		{
			if (depth > kMaxDirDepth)
			{
				return;
			}
			Directory.CreateDirectory(dest);
			foreach (string f in Directory.GetFiles(source))
			{
				File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), true);
			}
			foreach (string d in Directory.GetDirectories(source))
			{
				// Do not follow junctions/symlinks: avoids cycles (stack overflow)
				// and copying data that lives outside the save folder.
				if (IsReparsePoint(d))
				{
					continue;
				}
				CopyDir(d, Path.Combine(dest, Path.GetFileName(d)), depth + 1);
			}
		}

		// Mirrors CopyDir's traversal (skips reparse points, same depth cap) so the
		// source and backup file counts/sizes match for the verify step.
		static void DirStats(string dir, out int count, out long bytes)
		{
			count = 0;
			bytes = 0;
			DirStatsWalk(dir, 0, ref count, ref bytes);
		}

		static void DirStatsWalk(string dir, int depth, ref int count, ref long bytes)
		{
			if (depth > kMaxDirDepth)
			{
				return;
			}
			foreach (string f in Directory.GetFiles(dir))
			{
				count++;
				bytes += new FileInfo(f).Length;
			}
			foreach (string d in Directory.GetDirectories(dir))
			{
				if (IsReparsePoint(d))
				{
					continue;
				}
				DirStatsWalk(d, depth + 1, ref count, ref bytes);
			}
		}
	}
}
