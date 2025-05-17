
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

using System;
using System.IO;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AudioVisualization
{
	public class AudioClipContainer
	{
		public AudioClip audioClip;
		public string assetPath;
		public string tempWavePath;
		public int samples;
		public int frequency;
		public int channels;
		public bool preloadAudioData;
		byte[] audioData;
		public string name { get => audioClip.name; set => audioClip.name = value; }

		public AudioClipContainer() { }
		public AudioClipContainer(AudioClip audioClip)
		{
			Replace(audioClip);
		}
		public bool Replace(AudioClip newAudioClip)
		{
			if(audioClip != newAudioClip)
			{
				audioClip = newAudioClip;
				samples = audioClip.samples;
				frequency = audioClip.frequency;
				channels = FFMPEG.IsInstalled ? 1 : audioClip.channels;
#if UNITY_EDITOR
				assetPath = UnityEditor.AssetDatabase.GetAssetPath(audioClip);
#endif
				tempWavePath = Path.Combine(Application.temporaryCachePath, audioClip.name + ".fp32");
				preloadAudioData = FFMPEG.IsInstalled ? false : audioClip.preloadAudioData;
				audioData = null;
				return true;
			}
			else
				return false;
		}
		void LoadBuffer()
		{
			using (FileStream fileStream = File.OpenRead(tempWavePath))
			{
				int requeredBytes = samples * sizeof(float);
				audioData = new byte[requeredBytes];
				fileStream.Read(audioData, 0, requeredBytes);
			}
		}
		public void LoadAudioData()
		{
			if (FFMPEG.IsInstalled)
			{
				if (audioData != null)
					return;

				if (File.Exists(tempWavePath))
				{
					LoadBuffer();
					return;
				}

				Process ffmpeg = new Process();
				ffmpeg.StartInfo.FileName = FFMPEG.FFmpegPath;
				ffmpeg.StartInfo.Arguments = String.Format("-i \"{0}\" -ac 1 -f f32le \"{1}\"", assetPath, tempWavePath);
				ffmpeg.StartInfo.CreateNoWindow = true;
				ffmpeg.StartInfo.UseShellExecute = false;
				ffmpeg.StartInfo.RedirectStandardOutput = false;
				ffmpeg.StartInfo.RedirectStandardError = true;
				ffmpeg.StartInfo.WorkingDirectory = Environment.CurrentDirectory;
				ffmpeg.Start();
				string errors = ffmpeg.StandardError.ReadToEnd();
				ffmpeg.WaitForExit();
				if (ffmpeg.ExitCode == 0)
				{
					LoadBuffer();
				}
				else
				{
					Debug.LogError(errors);
				}
			}
			else
			{
				audioClip.LoadAudioData();
			}
		}
		public bool GetData(float[] data, int offsetSamples)
		{
			if (FFMPEG.IsInstalled)
			{
				Buffer.BlockCopy(audioData, offsetSamples * sizeof(float), data, 0, data.Length * sizeof(float));
				return true;
			}
			else
			{
				return audioClip.GetData(data, offsetSamples);
			}
		}
		public int GetInstanceID()
		{
			return audioClip.GetInstanceID();
		}
		public override bool Equals(object audioClip)
		{
			if (audioClip is AudioClip)
			{
				return (AudioClip)audioClip == this.audioClip;
			}
			else if (audioClip is AudioClipContainer)
			{
				return (AudioClipContainer)audioClip == this;
			}
			else
				return false;
		}
		public override int GetHashCode()
		{
			return audioClip.GetHashCode();
		}
		/*public static explicit operator AudioClip(AudioClipContainer container)
		{
			return container?.audioClip;
		}
		public static explicit operator AudioClipContainer(AudioClip clip)
		{
			return new AudioClipContainer(clip);
		}*/
		public static bool operator ==(AudioClipContainer container, AudioClipContainer another)
		{
			return container?.audioClip == another?.audioClip;
		}
		public static bool operator !=(AudioClipContainer container, AudioClipContainer another)
		{
			return container?.audioClip != another?.audioClip;
		}
		/*public static bool operator ==(AudioClipContainer container, AudioClip clip)
		{
			if (ReferenceEquals(container, null))
				return null == clip;
			else
				return container.audioClip == clip;
		}
		public static bool operator !=(AudioClipContainer container, AudioClip clip)
		{
			if (ReferenceEquals(container, null))
				return null != clip;
			else
				return container.audioClip != clip;
		}*/
		/*public static bool operator ==(AudioClipContainer container, object obj)
		{
			return ReferenceEquals(container, obj);
		}
		public static bool operator !=(AudioClipContainer container, object obj)
		{
			return !ReferenceEquals(container, obj);
		}*/
	}
}
