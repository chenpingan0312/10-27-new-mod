using System;
using Coatsink.Common;
using HarmonyLib;

namespace BetterKingdom
{
	/// <summary>
	/// 派遣骑士小队进攻时的选队优先级：
	/// 1) 小队里弓箭手越多越优先（numArchers，最多 4）；
	/// 2) 弓箭手数量相同时，进阶骑士（_wasPromoted）优先。
	///
	/// 实现方式：替换 Formation.Recruit() 内部使用的比较委托 _Recruit_b__62_1。
	/// </summary>
	[HarmonyPatch(typeof(Formation), "_Recruit_b__62_1")]
	internal static class FormationRecruitPriorityPatch
	{
		private static bool Prefix(Formation.IFormationUnit x, Formation.IFormationUnit y, ref int __result)
		{
			try
			{
				Knight knightX = x.TryCast<Knight>();
				Knight knightY = y.TryCast<Knight>();
				bool notKnight = knightX == null || knightY == null;
				if (notKnight)
				{
					return true;
				}
				int archersX = knightX.numArchers;
				int archersY = knightY.numArchers;
				bool archerDiff = archersX != archersY;
				if (archerDiff)
				{
					// 降序：弓箭手多的排在前面
					__result = archersY.CompareTo(archersX);
					return false;
				}
				bool promotedX = knightX._wasPromoted;
				bool promotedY = knightY._wasPromoted;
				bool promotedDiff = promotedX != promotedY;
				if (promotedDiff)
				{
					// 进阶骑士优先
					__result = promotedX ? -1 : 1;
					return false;
				}
				__result = 0;
				return false;
			}
			catch
			{
				return true;
			}
		}
	}
}
