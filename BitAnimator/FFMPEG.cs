
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

using System;
using System.IO;
using UnityEngine;

public static class FFMPEG
{
	static FFMPEG()
	{
		string ffmpegRootPath;
#if UNITY_EDITOR
		string path = UnityEditor.AssetDatabase.GUIDToAssetPath("bc2a8d7da6b112749a5579bcd4144019");
		if (String.IsNullOrEmpty(path))
			ffmpegRootPath = Environment.CurrentDirectory + "/Assets/ThirdParty/FFMPEG";
		else
			ffmpegRootPath = Environment.CurrentDirectory + "/" + path;
#else
		ffmpegRootPath = Environment.CurrentDirectory + "/ThirdParty/FFMPEG";
#endif

		if (Directory.Exists(ffmpegRootPath))
		{
			FFmpegPath = ffmpegRootPath + "/ffmpeg.exe";
			FFprobePath = ffmpegRootPath + "/ffprobe.exe";
			FFplayPath = ffmpegRootPath + "/ffplay.exe";
			IsInstalled = File.Exists(FFmpegPath) && File.Exists(FFprobePath) && File.Exists(FFplayPath);
		}
		else
		{
			Debug.LogError("FFMPEG not found at path: " + ffmpegRootPath);
		}
	}
	public static bool IsInstalled { get; private set; }
	public static string FFmpegPath { get; private set; }
	public static string FFprobePath { get; private set; }
	public static string FFplayPath { get; private set; }
}
