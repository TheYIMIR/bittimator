
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

#if UNITY_EDITOR
using UnityEditor;
#endif
namespace AudioVisualization
{
	public class SpectrumTexture : BitAnimator
	{
		[Tooltip("Export texture resolution (frequencies bands)")]
		public int outputChanels = 32;
		public Texture2D spectrumTextureAsset = null;
		new ISpectrumRenderer engine;
		public SpectrumTexture()
		{
			//selectedEngine = "AudioVisualization.LegacySpectrumRenderer";
			plan.mode = Mode.LogFrequency;
			plan.quality = 1.0f;
		}
		public void CreateSpectrumTexture(string outputPath)
		{
			if(engine != null)
				engine.Dispose();
			engine = null;
			InitializeEngine();
			engine.ApplyMods = true;
			engine.ViewScale = audioClip.length;
			engine.SetTask(recordSlots[0], PlotGraphic.Spectrum);
			int chunks = audioClip.samples / plan.WindowSize * plan.multisamples - (plan.multisamples - 1);
			chunks = Math.Min(8192, Mathf.CeilToInt(chunks * plan.quality / 8.0f) * 8);
			int channels = Math.Min(plan.WindowSize / 2, outputChanels);
			if (spectrumTextureAsset == null)
			{
				spectrumTextureAsset = new Texture2D(chunks, channels, TextureFormat.RGBA32, false);
				spectrumTextureAsset.name = audioClip.name;
			}
			else if(spectrumTextureAsset.width != chunks || spectrumTextureAsset.height != channels)
				spectrumTextureAsset.Reinitialize(chunks, channels, TextureFormat.RGBA32, false);
			engine.Render(spectrumTextureAsset);
			SavePNG(outputPath, ref spectrumTextureAsset);
		}
		
		public static void SavePNG(string filename, ref Texture2D texture)
		{
			byte[] bytes = texture.EncodeToPNG();
			if (bytes != null)
			{
#if UNITY_EDITOR
				texture = AssetDatabase.LoadAssetAtPath<Texture2D>(filename);
				if (texture == null)
				{
					File.WriteAllBytes(filename, bytes);
					AssetDatabase.Refresh();
					TextureImporter ti = TextureImporter.GetAtPath(filename) as TextureImporter;
					if (ti != null)
					{
						ti.maxTextureSize = 8192;
						ti.npotScale = TextureImporterNPOTScale.None;
						ti.textureCompression = TextureImporterCompression.Uncompressed;
						ti.wrapMode = TextureWrapMode.Clamp;
						ti.isReadable = true;
						ti.SaveAndReimport();
					}
					texture = AssetDatabase.LoadAssetAtPath<Texture2D>(filename);
				}
				else
				{
					File.WriteAllBytes(filename, bytes);
					AssetDatabase.Refresh();
				}
#else
				File.WriteAllBytes(filename, bytes);
#endif
			}
		}
		public static void SavePNG(string filename, RenderTexture texture)
		{
			Texture2D tex = new Texture2D(texture.width, texture.height, TextureFormat.RGBAFloat, false);

			RenderTexture oldRT = RenderTexture.active;
			RenderTexture.active = texture;
			tex.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
			tex.Apply();
			RenderTexture.active = oldRT;

			byte[] bytes = tex.EncodeToPNG();
			DestroyImmediate(tex);

			if (bytes != null)
			{
				File.WriteAllBytes(filename, bytes);
#if UNITY_EDITOR
				AssetDatabase.Refresh();
#endif
			}
		}
	}
}
