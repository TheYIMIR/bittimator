
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using UnityEngine;

namespace AudioVisualization
{
	[Serializable]
	public class Localization
	{
		internal static Dictionary<string, GUIContent> table = null;
		[SerializeField] string[] keys;
		[SerializeField] string[] values;
		public void Load(string language)
		{

		}
		public GUIContent this[string n]
		{
			get
			{
				GUIContent result;
				if (table.TryGetValue(n, out result))
					return result;
				else
				{
					result = new GUIContent(n);
					table.Add(n, result);
					return result;
				}
			}
		}
	}
}
