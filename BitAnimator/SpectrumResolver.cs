
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

using System;
using UnityEngine;

namespace AudioVisualization
{
	[Serializable]
	public abstract class SpectrumResolver : ScriptableObject
	{
		[NonSerialized] public BitAnimator bitAnimator;
#if UNITY_EDITOR
		public virtual void DrawProperty() { }
#endif
	}
}
