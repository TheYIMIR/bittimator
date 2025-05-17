
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
	//создание неубывающей кривой (каждый пик потихоньку сдвигает позицию к конечному значению)
	[Serializable]
	public class Accumulate : Modificator
	{
		public Accumulate() { }
#if UNITY_EDITOR
		override public void DrawProperty()
		{
			GUILayout.Label(new GUIContent("Accumulate values", "Sum peaks values over time"));
		}
#endif
	}
}