
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
	public class CustomPeaks : Modificator
	{
		public AnimationCurve shape = new AnimationCurve(new Keyframe(-0.1f, 0), new Keyframe(0, 1), new Keyframe(0.1f, 0));
		public CustomPeak[] peaks = new CustomPeak[0];
		bool fold;
		bool showAll;
		public override void Initialize(BitAnimator bitAnimator, BitAnimator.RecordSlot slot)
		{
			base.Initialize(bitAnimator, slot);
		}
#if UNITY_EDITOR
		override public void DrawProperty()
		{
			GUILayout.Label("Custom Peaks");
			++EditorGUI.indentLevel;
			shape = EditorGUILayout.CurveField(new GUIContent("Shape", "Common peak shape.\nHorizontal axis - time in seconds"), shape);
			fold = EditorGUILayout.Foldout(fold, new GUIContent("Peaks"), true);
			if (fold)
			{
				++EditorGUI.indentLevel;
				showAll = EditorGUILayout.ToggleLeft("Show additional parameters", showAll);
				for (int i = 0; i < peaks.Length; i++)
				{
					EditorGUILayout.BeginHorizontal();
					peaks[i].time = EditorGUILayout.FloatField("[" + (i + 1) + "] : Time", peaks[i].time);
					if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Minus", "Remove"), EditorStyles.miniButton, GUILayout.MaxWidth(24), GUILayout.MaxHeight(16)))
						ArrayUtility.RemoveAt(ref peaks, i--);
					EditorGUILayout.EndHorizontal();
					if(showAll)
					{
						peaks[i].multiplier = EditorGUILayout.Slider("multiplier", peaks[i].multiplier, 0.0f, 1.0f);
						peaks[i].width = EditorGUILayout.Slider("width", peaks[i].width, 0.0f, 1.0f);
					}
					EditorGUILayout.Separator();
				}
				EditorGUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Plus", "Add peak"), EditorStyles.miniButton, GUILayout.MaxWidth(24), GUILayout.MaxHeight(16)))
					ArrayUtility.Add(ref peaks, new CustomPeak(0, 1, 1));

				EditorGUILayout.EndHorizontal();
				--EditorGUI.indentLevel;
			}
			--EditorGUI.indentLevel;
		}
#endif
		[Serializable]
		public struct CustomPeak
		{
			public float time;
			public float multiplier;
			public float width;
			public CustomPeak(float _time = 0.0f, float _multiplier = 1.0f, float _width = 1.0f) 
			{
				time = _time;
				multiplier = _multiplier;
				width = _width;
			}
		}
	}
}