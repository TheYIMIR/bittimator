
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
	public class MainTone : Modificator
	{
		public float quantile = 0.1f;
		public override ExecutionQueue Queue { get { return ExecutionQueue.SpectrumMerge; } }
		public MainTone() { }
		public override void Initialize(BitAnimator bitAnimator, BitAnimator.RecordSlot slot)
		{
			InitializeSingleInstance<MainTone>(bitAnimator, slot);
		}
#if UNITY_EDITOR
		override public void DrawProperty()
		{
			quantile = EditorGUILayout.Slider(new GUIContent("Quantile", "frequency band size to calculate main/average tone"), quantile, 0, 1);
		}
#endif
	}
}