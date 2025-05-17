
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AudioVisualization.Modificators
{
	[Serializable]
	public class BeatFinder : Modificator
	{
		public float searchTimeRange = 4;
		public override ExecutionQueue Queue { get { return ExecutionQueue.SpectrumMerge; } }
		public BeatFinder() { }
		public override void Initialize(BitAnimator bitAnimator, BitAnimator.RecordSlot slot)
		{
			InitializeSingleInstance<BeatFinder>(bitAnimator, slot);
		}
#if UNITY_EDITOR
		static GUIContent label = new GUIContent("Search range", "window width for analize local BPM (value in seconds)");
		override public void DrawProperty()
		{
			GUILayout.Label("Beat Finder", EditorStyles.boldLabel);

			EditorGUI.BeginChangeCheck();
			float searchTimeRange2 = Mathf.Max(0.0f, EditorGUILayout.FloatField(label, searchTimeRange));
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(this, "Search range");
				searchTimeRange = searchTimeRange2;
				EditorUtility.SetDirty(this);
			}
		}
#endif
	}
}