
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

using UnityEngine;

namespace UnityEngine
{
	public static class HyperbolicMath
    {
		public static float Sinh(float x)
        {
			return (Mathf.Exp(x) - Mathf.Exp(-x))*0.5f;
		}
		public static float Cosh(float x)
        {
			return (Mathf.Exp(x) + Mathf.Exp(-x))*0.5f;
		}
	}
}