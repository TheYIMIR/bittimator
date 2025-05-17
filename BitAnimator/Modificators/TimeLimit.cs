
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

using System;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AudioVisualization.Modificators
{
	[Serializable]
	public class TimeLimit : Modificator
	{
		public float start;
		public float end;
		public float fadeIn;
		public float fadeOut;
		bool fold;
		public TimeLimit() { }
		public override void Initialize(BitAnimator bitAnimator, BitAnimator.RecordSlot slot) 
		{
			InitializeSingleInstance<TimeLimit>(bitAnimator, slot);
		}
#if UNITY_EDITOR
		override public void DrawProperty()
		{
			GUILayout.Label("Time limit");
			EditorGUI.BeginChangeCheck();
			float fadeIn2 = EditorGUILayout.FloatField("Increasing", fadeIn);
			float start2 = EditorGUILayout.FloatField("Start", start);
			float end2 = EditorGUILayout.FloatField("Fading", end);
			float fadeOut2 = EditorGUILayout.FloatField("End", fadeOut);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(this, "Time limits");
				fadeIn = fadeIn2;
				start = start2;
				end = end2;
				fadeOut = fadeOut2;
				EditorUtility.SetDirty(this);
			}
		}
#endif
	}
}


