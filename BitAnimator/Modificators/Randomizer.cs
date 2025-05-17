
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
	public class Randomizer: Modificator
	{
		public float randomize = 0.05f;
		public Randomizer() { }
#if UNITY_EDITOR
		override public void DrawProperty()
		{
			randomize = EditorGUILayout.Slider(new GUIContent("Randomize", "Generate random values by input volume"), randomize, 0.0f, 1.0f);
		}
#endif
	}
}
