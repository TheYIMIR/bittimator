
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
    public class Shift : Modificator
    {
		public float averageLevel = 0.5f;
		public override void Initialize(BitAnimator bitAnimator, BitAnimator.RecordSlot slot)
		{
			InitializeSingleInstance<Shift>(bitAnimator, slot);
		}
#if UNITY_EDITOR
		static GUIContent averageLevelLabel = new GUIContent("Average level", "Fade/Attack balance\nzero means that only boosts will affect the peaks\notherwise, 1 means that only damping will affect");
		public override void DrawProperty()
        {
			GUILayout.Label("Shift - calculates volume difference\nbeetween frames");
			EditorGUI.BeginChangeCheck();
			float averageLevel2 = EditorGUILayout.Slider(averageLevelLabel, averageLevel, 0.0f, 1.0f);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(this, "Shift");
				averageLevel = averageLevel2;
				EditorUtility.SetDirty(this);
			}
		}
#endif
	}
}