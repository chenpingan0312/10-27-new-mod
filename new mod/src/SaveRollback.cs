using System;
using System.Diagnostics;
using System.IO;
using BepInEx.Logging;
using UnityEngine;

namespace BetterKingdom
{
	/// <summary>
	/// 存档回档：滚动备份 global-v35，点"回档"时把最近一份备份写回去并重启游戏。
	/// </summary>
	internal static class SaveRollback
	{
		private const int BackupCount = 3;

		private static ManualLogSource _log;

		private static string _lastStamp = string.Empty;

		private static DateTime _nextCheck = DateTime.MinValue;

		internal static string SavePath
		{
			get
			{
				try
				{
					string root = Application.persistentDataPath;
					string p = Path.Combine(root, "Release", "global-v35");
					bool exists = File.Exists(p);
					if (exists)
					{
						return p;
					}
					// 兜底：遍历 Release 目录下最新的 global-*
					string dir = Path.Combine(root, "Release");
					if (Directory.Exists(dir))
					{
						string[] files = Directory.GetFiles(dir, "global-*");
						if (files.Length > 0)
						{
							return files[0];
						}
					}
					return p;
				}
				catch
				{
					return null;
				}
			}
		}

		private static string BackupPath(int index)
		{
			string save = SaveRollback.SavePath;
			if (save == null)
			{
				return null;
			}
			return save + ".bk" + index;
		}

		internal static bool HasBackup()
		{
			try
			{
				string p = SaveRollback.BackupPath(1);
				return p != null && File.Exists(p);
			}
			catch
			{
				return false;
			}
		}

		/// <summary>每帧调用（内部限流）：存档文件变了就滚动备份一次。</summary>
		internal static void Tick()
		{
			try
			{
				DateTime now = DateTime.UtcNow;
				bool due = now >= SaveRollback._nextCheck;
				if (!due)
				{
					return;
				}
				SaveRollback._nextCheck = now.AddSeconds(20.0);
				string save = SaveRollback.SavePath;
				if (save == null || !File.Exists(save))
				{
					return;
				}
				FileInfo info = new FileInfo(save);
				string stamp = info.Length.ToString() + ":" + info.LastWriteTimeUtc.Ticks.ToString();
				bool changed = stamp != SaveRollback._lastStamp;
				if (!changed)
				{
					return;
				}
				bool first = SaveRollback._lastStamp.Length == 0;
				SaveRollback._lastStamp = stamp;
				if (first)
				{
					// 首次只记录基线，不备份（否则会把"本次进游戏前"的档覆盖掉更旧的备份）
					bool none = !SaveRollback.HasBackup();
					if (none)
					{
						SaveRollback.ShiftAndCopy(save);
					}
					return;
				}
				SaveRollback.ShiftAndCopy(save);
			}
			catch (Exception ex)
			{
				SaveRollback.Log("backup failed: " + ex.Message);
			}
		}

		private static void ShiftAndCopy(string save)
		{
			try
			{
				for (int i = SaveRollback.BackupCount - 1; i >= 1; i--)
				{
					string from = SaveRollback.BackupPath(i);
					string to = SaveRollback.BackupPath(i + 1);
					if (from != null && to != null && File.Exists(from))
					{
						File.Copy(from, to, true);
					}
				}
				string first = SaveRollback.BackupPath(1);
				if (first != null)
				{
					File.Copy(save, first, true);
					SaveRollback.Log("backup ok -> " + first);
				}
			}
			catch (Exception ex)
			{
				SaveRollback.Log("shift failed: " + ex.Message);
			}
		}

		/// <summary>把最近一份备份写回存档，然后重启游戏。</summary>
		internal static bool RollbackAndRestart()
		{
			try
			{
				string save = SaveRollback.SavePath;
				string backup = SaveRollback.BackupPath(1);
				bool ok = save != null && backup != null && File.Exists(backup);
				if (!ok)
				{
					SaveRollback.Log("rollback: no backup");
					return false;
				}
				File.Copy(backup, save, true);
				SaveRollback.Log("rollback ok <- " + backup);
				SaveRollback.Restart();
				return true;
			}
			catch (Exception ex)
			{
				SaveRollback.Log("rollback failed: " + ex.Message);
				return false;
			}
		}

		private static void Restart()
		{
			try
			{
				Process current = Process.GetCurrentProcess();
				string exe = current.MainModule.FileName;
				string dir = Path.GetDirectoryName(exe);
				int pid = current.Id;
				// 用独立进程等待本进程退出后再启动游戏，避免两个实例同时存在导致
				// BepInEx(doorstop) 注入失败（表现为：重启后 mod 完全不加载）
				string cmdText = "@echo off" + Environment.NewLine
					+ ":wait" + Environment.NewLine
					+ "tasklist /fi \"pid eq " + pid + "\" | find \"" + pid + "\" >nul" + Environment.NewLine
					+ "if not errorlevel 1 (" + Environment.NewLine
					+ "  ping -n 1 -w 150 127.0.0.1 >nul" + Environment.NewLine
					+ "  goto wait" + Environment.NewLine
					+ ")" + Environment.NewLine
					+ "ping -n 1 -w 400 127.0.0.1 >nul" + Environment.NewLine
					+ "explorer.exe \"" + exe + "\"" + Environment.NewLine + "exit" + Environment.NewLine;
				File.WriteAllText(Path.Combine(dir, "BetterKingdom_restart.cmd"), cmdText);
				ProcessStartInfo psi = new ProcessStartInfo("cmd.exe");
				psi.Arguments = "/c \"" + Path.Combine(dir, "BetterKingdom_restart.cmd") + "\"";
				psi.WindowStyle = ProcessWindowStyle.Hidden;
				psi.WorkingDirectory = dir;
				psi.UseShellExecute = false;
				psi.CreateNoWindow = true;
				Process.Start(psi);
			}
			catch (Exception ex)
			{
				SaveRollback.Log("restart failed: " + ex.Message);
			}
			try
			{
				Process.GetCurrentProcess().Kill();
			}
			catch
			{
			}
		}

		internal static void Log(string message)
		{
			try
			{
				if (SaveRollback._log == null)
				{
					SaveRollback._log = BepInEx.Logging.Logger.CreateLogSource("BKBackup");
				}
				SaveRollback._log.LogInfo(message);
			}
			catch
			{
			}
		}
	}
}
