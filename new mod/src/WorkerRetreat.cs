using System;
using Coatsink.Common;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace BetterKingdom
{
	/// <summary>
	/// 夜晚城墙外的工人集体撤退：
	/// 只要有一个在城墙外的工人受惊（ShouldFlee 为真），就让所有城墙外的工人一起放弃手头工作、往回跑；
	/// 天亮（IsNight 变 false）自动解除，工人照常出去干活。
	/// </summary>
	public class WorkerRetreatBehaviour : MonoBehaviour
	{
		public WorkerRetreatBehaviour(IntPtr ptr) : base(ptr)
		{
		}

		private float _nextCheck;

		private void Update()
		{
			float now = Time.unscaledTime;
			bool due = now >= this._nextCheck;
			if (!due)
			{
				return;
			}
			this._nextCheck = now + 0.4f;
			WorkerPanic.Refresh();
			SaveRollback.Tick();
		}
	}

	internal static class WorkerPanic
	{
		internal static bool Active;

		internal static void Refresh()
		{
			try
			{
				Managers managers = SingletonMonoBehaviour<Managers>.Inst;
				if (managers == null)
				{
					WorkerPanic.Active = false;
					return;
				}
				Director director = managers.director;
				if (director == null)
				{
					WorkerPanic.Active = false;
					return;
				}
				bool isNight;
				try
				{
					isNight = director.IsNight;
				}
				catch
				{
					isNight = false;
				}
				if (!isNight)
				{
					WorkerPanic.Active = false;
					return;
				}
				bool already = WorkerPanic.Active;
				if (already)
				{
					return;
				}
				Kingdom kingdom = managers.kingdom;
				if (kingdom == null)
				{
					return;
				}
				Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<Worker> workers =
					UnityEngine.Object.FindObjectsByType<Worker>(FindObjectsSortMode.None);
				if (workers == null)
				{
					return;
				}
				for (int i = 0; i < workers.Length; i++)
				{
					Worker worker = workers[i];
					bool nullWorker = worker == null;
					if (nullWorker)
					{
						continue;
					}
					float x;
					try
					{
						x = worker.transform.position.x;
					}
					catch
					{
						continue;
					}
					bool inside;
					try
					{
						inside = kingdom.IsWithinWalls(x);
					}
					catch
					{
						continue;
					}
					if (inside)
					{
						continue;
					}
					bool flee;
					try
					{
						flee = worker.ShouldFlee();
					}
					catch
					{
						continue;
					}
					if (flee)
					{
						WorkerPanic.Active = true;
						return;
					}
				}
			}
			catch
			{
			}
		}
	}

	[HarmonyPatch(typeof(Worker), "ShouldFlee")]
	internal static class WorkerShouldFleeRetreatPatch
	{
		private static void Postfix(Worker __instance, ref bool __result)
		{
			bool already = __result;
			if (already)
			{
				return;
			}
			bool panic = WorkerPanic.Active;
			if (!panic)
			{
				return;
			}
			try
			{
				Managers managers = SingletonMonoBehaviour<Managers>.Inst;
				bool noManagers = managers == null;
				if (noManagers)
				{
					return;
				}
				Kingdom kingdom = managers.kingdom;
				bool noKingdom = kingdom == null;
				if (noKingdom)
				{
					return;
				}
				float x = __instance.transform.position.x;
				bool inside = kingdom.IsWithinWalls(x);
				if (inside)
				{
					return;
				}
				__result = true;
			}
			catch
			{
			}
		}
	}
}
