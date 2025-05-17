
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace AudioVisualization
{
	[EngineInfo("Legacy renderer", Renderer: true)]
	public class LegacySpectrumRenderer : LegacyProgram, ISpectrumRenderer
	{
		const int maxSize = 8192;
		protected static readonly int _Time = Shader.PropertyToID("_Time");
		protected static readonly int _MaskTex = Shader.PropertyToID("_MaskTex");
		protected static readonly int _BPM = Shader.PropertyToID("_BPM");
		protected static readonly int _BeatOffset = Shader.PropertyToID("_BeatOffset");
		protected static readonly int _LowFrequency = Shader.PropertyToID("_LowFrequency");
		protected static readonly int _HighFrequency = Shader.PropertyToID("_HighFrequency");
		protected static readonly int _Multiply = Shader.PropertyToID("_Multiply");
		protected static readonly int _AudioClipTime = Shader.PropertyToID("_AudioClipTime");
		protected static readonly int _TimeStart = Shader.PropertyToID("_TimeStart");
		protected static readonly int _TimeEnd = Shader.PropertyToID("_TimeEnd");
		protected static readonly int _RenderTargetSize = Shader.PropertyToID("_RenderTargetSize");
		protected List<CacheBlock> Cache = new List<CacheBlock>();
		protected List<CacheBlock> oldCache = new List<CacheBlock>();
		PlotGraphic plotType;
		Action syncCache;
		CacheBlock newBlock;
		Texture2D tempTexture;
		Texture2D maskTexture;
		byte[] tempData;
		LegacyTask task;
		Material background;
		Material peaksMaterial;
		Material histogrammMaterial;
		Material spectrumMaterial;
		Color32[] frequencyMask;
		object taskGuard = new object();

		float timeScale = 5.0f;
		float oldMaximum;
		float maximum;
		int chunksPerBatch;
		int textureWidth;
		public float Maximum { get { return Mathf.Max(maximum, oldMaximum); } }
		public float RMS { get; set; }
		public bool ApplyMods { get; set; }
		public float ViewScale 
		{
			get { return timeScale; }
			set 
			{
				timeScale = value;
				peaksMaterial.SetFloat("_TimeScale", timeScale);
				spectrumMaterial.SetFloat("_TimeScale", timeScale);
			} 
		}

		public LegacySpectrumRenderer()
		{
			background = new Material(Shader.Find("Unlit/Color"));
			histogrammMaterial = new Material(Shader.Find("Unlit/BitAnimatorRenderer/Histogram"));
			peaksMaterial = new Material(Shader.Find("Unlit/BitAnimatorRenderer/Peaks"));
			spectrumMaterial = new Material(Shader.Find("Unlit/BitAnimatorRenderer/Spectrum"));
		}
		public override void Initialize(EngineSettings settings, Plan plan, AudioClip audio)
		{
			plan.mode |= Mode.LogFrequency;
			base.Initialize(settings, plan, audio);
			float chunksPerSecond = frequency / FFTWindow * plan.multisamples;
			float halfWindowTime = 0.5f * FFTWindow / frequency;
			/*
			int calculatedMemory = FFTWindow * 21 / 1024 * chunks / 1024;
			int maxRAM = maxUseRAM - audioClip.samples * 4 / 1024 / 1024;
			if(maxRAM <= 0)
				throw new OutOfMemoryException("Failed to initialize spectrum renderer");

			blocks = (calculatedMemory - 1) / maxRAM + 1;
			batches = (chunks - 1) / blocks + 1;
			*/
			peaksMaterial.SetFloat("_BPM", bpm);
			peaksMaterial.SetFloat("_BeatOffset", beatOffset / 1000.0f);
			peaksMaterial.SetFloat("_ChunksPerSecond", chunksPerSecond);
			peaksMaterial.SetFloat("_HalfWindowTime", halfWindowTime);
			peaksMaterial.SetInt("_Mode", (int)plan.mode | 2048);
			histogrammMaterial.SetFloat("_Frequency", frequency);
			histogrammMaterial.SetFloat("_FFTWindow", FFTWindow);
			histogrammMaterial.SetInt("_Mode", (int)plan.mode);
			spectrumMaterial.SetFloat("_Frequency", frequency);
			spectrumMaterial.SetFloat("_ChunksPerSecond", chunksPerSecond);
			spectrumMaterial.SetFloat("_HalfWindowTime", halfWindowTime);
			spectrumMaterial.SetInt("_Mode", (int)plan.mode | 2048);
			background.SetColor("_Color", Color.black);
		}

		public void Render(Texture2D texture)
		{
			//LoadAudio(0, chunks);
			//CalculateSpectrum(0, 0, chunks);
			int outputChanels = texture.height;
			float hzPerBin = frequency / FFTWindow;
			int start = Mathf.FloorToInt(task.slot.startFreq / hzPerBin);
			int end = Mathf.CeilToInt(task.slot.endFreq / hzPerBin);
			int window = end - start;

			float[] spectrumResized = new float[outputChanels];
			float[] spectrum = new float[window];
			float[] resultSpectrum = new float[texture.width * outputChanels];

			int calculatedMemory = (chunks * RealWindow * 4 + audioClip.samples * 4) / 1024 / 1024;
			int blocks = (calculatedMemory - 1) / maxUseRAM + 1;
			int batches = (chunks - 1) / blocks + 1;
			if (spectrumMap != null)
				spectrumMapPin.Free();
			spectrumMap = new float[RealWindow * batches];
			spectrumMapPin = GCHandle.Alloc(spectrumMap, GCHandleType.Pinned);
			cache = new FileInfo(spectrumCache);
			for (int block = 0; block < blocks; block++)
			{
				int offset = batches * block;
				loadOffset = offset;
				loadLength = Math.Min(batches, chunks - offset);
				EnqueueTask(LoadSpectrum, new SpectrumPartRequest(0, loadOffset, loadLength));
				while (IsWorking())
					System.Threading.Thread.Sleep(0);
				for (int i = 0; i < spectrumChunks; i++)
				{
					//Scaling to better visualise
					for (int x = 0; x < window; x++)
						spectrum[x] = spectrumMap[i * RealWindow + x + start];
					
					if ((plan.mode & Mode.LogFrequency) != 0)
						spectrum = LinearToLogFreq(spectrum);

					//Scale height (frequencies)
					Array.Clear(spectrumResized, 0, spectrumResized.Length);
					/* Old - iteration by output
					 * int n = spectrum.Length / outputChanels;
					for (int x = 0; x < outputChanels; x++)
						for (int j = 0; j < n; j++)
							spectrumResized[x] += spectrum[x * n + j] / n;*/
					//Inerations by input
					float step = (outputChanels - 1.0f) / window;
					if(outputChanels == 1)
					{
						spectrumResized[0] = spectrum.Sum();
					}
					else
					{
						for(int x = 0; x < window; x++)
						{
							float value = spectrum[x] * step;
							float position = x * step;
							int index = Mathf.FloorToInt(position);
							float frac = position - index;
							spectrumResized[index] += (1.0f - frac) * value;
							spectrumResized[index + 1] += frac * value;
						}
					}
					//Scale width (time)
					if (texture.width < chunks)
					{
						float timeStep = (texture.width - 1.0f) / chunks;
						float time = (i + loadOffset) * timeStep;
						int column = Mathf.FloorToInt(time);
						float slice = time - column;
						for (int x = 0; x < outputChanels; x++)
						{
							float value = spectrumResized[x] * timeStep;
							resultSpectrum[column * outputChanels + x] += (1.0f - slice) * value;
							resultSpectrum[(column + 1) * outputChanels + x] += slice * value;
						}
					}
					else
					{
						for (int x = 0; x < outputChanels; x++)
							resultSpectrum[i * outputChanels + x] = spectrumResized[x];
					}
				}
			}
			if (cacheFile != null)
				cacheFile.Dispose();
			cacheFile = null;
			cache = null;
			//Normalize
			if (task.legacyMods.FirstOrDefault(mod => mod.GetType() == typeof(LegacyNormalize)) != default(ILegacyModificator))
			{
				float maximum = resultSpectrum.Max();
				for (int x = 0; x < resultSpectrum.Length; x++)
					resultSpectrum[x] /= maximum;
			}

			//Scaling with curve
			LegacyRemap remap = task.legacyMods.FirstOrDefault(mod => mod.GetType() == typeof(LegacyRemap)) as LegacyRemap;
			AnimationCurve remapCurve = remap.mod.remap;
			if (remap != null && outputChanels > 1)
				for (int x = 0; x < texture.width; x++)
					for (int y = 0; y < outputChanels; y++)
						resultSpectrum[x * outputChanels + y] *= remapCurve.Evaluate(y / (outputChanels - 1.0f));

			//Create spectrum heatmap
			Gradient gradient = task.slot.colors;
			for (int x = 0; x < texture.width; x++)
				for (int y = 0; y < outputChanels; y++)
				{
					float value = resultSpectrum[x * outputChanels + y];
					Color color = gradient.Evaluate(value);
					color.a = value;
					texture.SetPixel(x, y, color);
				}
			texture.Apply();
		}
		public void Render(RenderTexture renderTarget, float time, float multiply)
		{
			switch(plotType)
			{
				case PlotGraphic.Peaks:         RenderPeaks(renderTarget, time, multiply); break;
				case PlotGraphic.Spectrum:   RenderSpectrum(renderTarget, time, multiply); break;
				case PlotGraphic.Histogram: RenderHistogram(renderTarget, time, multiply); break;
			}
			PrintLogs();
		}
		public void RenderBPM(RenderTexture texture, float time)
		{
		}
		public void RenderHistogram(RenderTexture texture, float time, float multiply)
		{
			histogrammMaterial.SetFloat(_Multiply, multiply);
			int offset = Mathf.FloorToInt(time * frequency) - FFTWindow / 2;
			offset = Math.Max(0, offset);
			Texture2D tex = Cache[0].texture as Texture2D;
			if (audioClip.samples - offset >= FFTWindow)
			{
				lock (jobGuard)
				{
					LoadAudio(offset, FFTWindow);
					if (fft != IntPtr.Zero)
						NativeCore.CalculateSpectrum(fft, monoSound, (uint)monoSound.Length, spectrumMap, (uint)spectrumMap.Length, 0, 1, 0);
					else
						CalculateSpectrum(0, 1, 0);
					ResizeSpectrum(textureWidth);
					//maximum = spectrumMap.Max();
					Buffer.BlockCopy(spectrumMap, 0, tempData, 0, tempData.Length);
					//Texture2D tex = new Texture2D(RealWindow, 1, TextureFormat.RFloat, false, true);
					tempTexture.LoadRawTextureData(tempData);
					tempTexture.Apply();

					Graphics.CopyTexture(tempTexture, 0, 0, tex, 0, 0);
					tex.Apply();
					//Texture2D.DestroyImmediate(tempTexture);
				}
			}
			Graphics.Blit(tex, texture, histogrammMaterial);
		}
		void ResizeSpectrum(int newWidth)
		{
			if (RealWindow == newWidth)
				return;
			if (RealWindow % newWidth != 0)
				throw new ArgumentException("[LegacySpectrumRenderer] Resize with a fractional scale is not supported");
			if (fft != IntPtr.Zero)
			{
				NativeCore.ResizeSpectrum(fft, spectrumMap, (uint)spectrumMap.Length, (uint)newWidth);
				return;
			}

			int stride = RealWindow / newWidth;
			float scale = 1.0f / stride;
			int count = spectrumMap.Length / stride;
			for (int i = 0; i < count; i++)
			{
				float sum = 0f;
				int end = i * stride + stride;
				for (int j = i * stride; j < end; j++)
				{
					sum += spectrumMap[j];
				}
				spectrumMap[i] = sum * scale;
			}
		}
		public void RenderPeaks(RenderTexture renderTarget, float time, float multiply)
		{
			peaksMaterial.SetFloat(_Multiply, multiply);
			peaksMaterial.SetFloat(_AudioClipTime, time);
			peaksMaterial.SetVector(_RenderTargetSize, new Vector4(renderTarget.width, renderTarget.height, 1.0f / renderTarget.width, 1.0f / renderTarget.height));
			BlockRenderer(renderTarget, peaksMaterial, time);
		}
		void RenderSpectrum(RenderTexture renderTarget, float time, float multiply)
		{
			spectrumMaterial.SetFloat(_AudioClipTime, time);
			spectrumMaterial.SetFloat(_Multiply, multiply);
			spectrumMaterial.SetVector(_RenderTargetSize, new Vector4(renderTarget.width, renderTarget.height, 1.0f / renderTarget.width, 1.0f / renderTarget.height));
			BlockRenderer(renderTarget, spectrumMaterial, time);
		}
		void LoadSpectrumPartSync()
		{
			int currentThreads = activeThreads;
			LoadSpectrum(new SpectrumPartRequest(0, loadOffset, loadLength));
			lock(jobEvent)
				while(activeThreads > currentThreads)
					Monitor.Wait(jobEvent);
		}
		void LoadPeaks()
		{
			lock (jobGuard)
			{
				firstPass = true;
				if (!task.isPeaksLoaded)
				{
					for (int block = 0; block < blocks; block++)
					{
						loadOffset = chunksPerBatch * block;
						loadLength = Math.Min(chunksPerBatch, chunks - loadOffset);
						LoadSpectrumPartSync();
						MergeBand(task);
					}
					task.isPeaksLoaded = true;
					if (ApplyMods)
					{
						ApplyModificators(task);
					}
					else
					{
						task.values = task.rawValues;
					}
				}
				float timePerChunk = FFTWindow / frequency / plan.multisamples;
				float halfWindowTime = 0.5f * FFTWindow / frequency;
				newBlock.timeStart = newBlock.offset * timePerChunk + halfWindowTime;
				newBlock.timeEnd = newBlock.End * timePerChunk + halfWindowTime;
				newBlock.maxValue = 1;
				//newBlock.maxValue = task.values.Skip(newBlock.offset).Take(newBlock.length).Max();
				Buffer.BlockCopy(task.values, newBlock.offset * sizeof(float), tempData, 0, newBlock.length * sizeof(float));
					
				if (BitAnimator.debugMode)
					OnLoadPeaks();
				else
					syncCache = OnLoadPeaks;
			}
		}
		void OnLoadPeaks()
		{
			tempTexture.LoadRawTextureData(tempData);
			tempTexture.Apply();
			Texture2D texture = new Texture2D(newBlock.length, 1, TextureFormat.RFloat, true, true);
			texture.filterMode = FilterMode.Point;
			texture.wrapMode = TextureWrapMode.Clamp;
			Graphics.CopyTexture(tempTexture, 0, 0, 0, 0, newBlock.length, 1, texture, 0, 0, 0, 0);
			texture.Apply();

			newBlock.creationTime = Time.realtimeSinceStartup;
			newBlock.texture = texture;
			Cache.Insert(~Cache.BinarySearch(newBlock), newBlock);

			maximum = Mathf.Max(maximum, newBlock.maxValue);
			if(Cache.Count > 16)
				MergeCache(PlotGraphic.Peaks);
		}
		void MergeCache(PlotGraphic type)
		{
			long maxVRAM = maxUseVRAM * 1024L / 2;
			long usedVRAM = Cache.Sum(c => c.texture.width * c.texture.height * sizeof(float) / 1024);
			maxVRAM = (maxVRAM - usedVRAM) * 1024L;
			UnityEngine.Debug.Assert(maxVRAM > 0, "VRAM usage >= maximum");
			for(int i = 0; i < Cache.Count - 1; i++)
			{
				CacheBlock cache0 = Cache[i];
				CacheBlock cache1 = Cache[i + 1];
				if(cache0.Distance(cache1) == 0)
				{
					int sumLength = cache1.End - cache0.offset;
					UnityEngine.Debug.Assert(cache0.End < cache1.End, "Right TextureCache has inside left TextureCache!");
					if(sumLength > maxSize)
						continue;
					RectInt rect;
					switch (type)
					{
						case PlotGraphic.Spectrum: rect = new RectInt(0, cache0.texture.height, cache0.texture.width, sumLength); break;
						case PlotGraphic.Peaks:    rect = new RectInt(cache0.texture.width, 0, sumLength, cache0.texture.height); break;
						default: rect = new RectInt(); break;
					}
					if (rect.width * rect.height * sizeof(float) > maxVRAM)
						continue;
					Texture2D newTexture = new Texture2D(rect.width, rect.height, TextureFormat.RFloat, true, true);
					newTexture.filterMode = cache0.texture.filterMode;
					newTexture.wrapMode = cache0.texture.wrapMode;
					CacheBlock newCache = new CacheBlock
					{
						offset = cache0.offset,
						length = sumLength,
						timeStart = cache0.timeStart,
						timeEnd = cache1.timeEnd,
						maxValue = Mathf.Max(cache0.maxValue, cache1.maxValue),
						creationTime = Mathf.Max(cache0.creationTime, cache1.creationTime),
						texture = newTexture
					};
					Graphics.CopyTexture(cache0.texture, 0, 0, 0, 0, cache0.texture.width, cache0.texture.height, newCache.texture, 0, 0, 0, 0);
					Graphics.CopyTexture(cache1.texture, 0, 0, 0, 0, cache1.texture.width, cache1.texture.height, newCache.texture, 0, 0, rect.x, rect.y);
					newTexture.Apply();

					Cache.RemoveRange(i, 2);
					Cache.Insert(i, newCache);
					cache0.Dispose();
					cache1.Dispose();
				}
			}
		}
		void LoadSpectrumTexture()
		{
			lock (jobGuard)
			{
				firstPass = true;
				loadOffset = newBlock.offset;
				loadLength = newBlock.length;
				LoadSpectrumPartSync();
				ResizeSpectrum(textureWidth);
				float timePerChunk = FFTWindow / frequency / plan.multisamples;
				float halfWindowTime = 0.5f * FFTWindow / frequency;
				/*float max = 0;
				int count = textureWidth * loadLength;
				for (int i = 0; i < count; i++)
					if (spectrumMap[i] > max)
						max = spectrumMap[i];*/

				newBlock.timeStart = newBlock.offset * timePerChunk + halfWindowTime;
				newBlock.timeEnd = newBlock.End * timePerChunk + halfWindowTime;
				newBlock.maxValue = 1;
				Buffer.BlockCopy(spectrumMap, 0, tempData, 0, textureWidth * newBlock.length * sizeof(float));

				if (BitAnimator.debugMode)
					OnLoadSpectrumTexture();
				else
					syncCache = OnLoadSpectrumTexture;
			}
		}
		void OnLoadSpectrumTexture()
		{
			Texture2D texture = new Texture2D(textureWidth, newBlock.length, TextureFormat.RFloat, true, true)
			{
				anisoLevel = 16,
				filterMode = FilterMode.Trilinear,
				wrapMode = TextureWrapMode.Clamp,
			};

			tempTexture.LoadRawTextureData(tempData);
			tempTexture.Apply();
			Graphics.CopyTexture(tempTexture, 0, 0, 0, 0, textureWidth, newBlock.length, texture, 0, 0, 0, 0);
			texture.Apply();

			newBlock.creationTime = Time.realtimeSinceStartup;
			newBlock.texture = texture;
			Cache.Insert(~Cache.BinarySearch(newBlock), newBlock);

			maximum = Mathf.Max(maximum, newBlock.maxValue);
			if(Cache.Count > 16)
				MergeCache(PlotGraphic.Spectrum);
		}
		bool FreeVRAM(int kbytes, int startExcludeChunk, int endExcludeChunk)
		{
			foreach(CacheBlock cache in oldCache.OrderBy(c => c.creationTime))
			{
				kbytes -= cache.texture.width * cache.texture.height * sizeof(float) / 1024;
				cache.Dispose();
				if(kbytes <= 0)
					break;
			}
			oldCache.RemoveAll(cache => cache.texture == null);
			if(kbytes <= 0)
				return true;

			foreach(CacheBlock cache in Cache.OrderBy(c => c.creationTime))
			{
				if(cache.Overlap(startExcludeChunk, endExcludeChunk))
					continue;

				kbytes -= cache.texture.width * cache.texture.height * sizeof(float) / 1024;
				cache.Dispose();
				if (kbytes <= 0)
					break;
			}
			Cache.RemoveAll(cache => cache.texture == null);
			return kbytes <= 0;
		}
		void TryLoad(int offset, int count, int startChunk, int endChunk)
		{
			int maxVRAM = maxUseVRAM * 1024 / 2;
			int usedVRAM = Cache.Sum(c => c.texture.width * c.texture.height * sizeof(float) / 1024);
			int requestedVRAM = 1;
			if(plotType == PlotGraphic.Spectrum)
				requestedVRAM = RealWindow;

			requestedVRAM *= count * sizeof(float) / 1024;
			if(maxVRAM < usedVRAM + requestedVRAM)
			{
				if(!FreeVRAM(usedVRAM + requestedVRAM - maxVRAM, startChunk, endChunk))
					return;

				oldMaximum = oldCache.Count == 0 ? 0 : oldCache.Max(block => block.maxValue);
				maximum = Cache.Count == 0 ? 0 : Cache.Max(block => block.maxValue);
			}
			newBlock = new CacheBlock(offset, count);
			Action job = plotType == PlotGraphic.Spectrum ? (Action)LoadSpectrumTexture: (Action)LoadPeaks;
			if (BitAnimator.debugMode)
				job();
			else
				EnqueueTask(job);
		}
		bool ScanAndLoadBlocks(int startChunk, int endChunk)
		{
			int chunk = startChunk;
			while (chunk < endChunk)
			{
				int index = Cache.BinarySearch(new CacheBlock(chunk));
				index = (index >= 0 ? index : ~index);
				int next = Mathf.Min(chunk + chunksPerBatch, index < Cache.Count ? Cache[index].offset : chunks);
				int prev = Mathf.Max(chunk - chunksPerBatch, 0 < index ? Cache[index - 1].End : 0);
				if (next - chunk < chunk - prev)
					prev = Mathf.Max(next - chunksPerBatch, prev);

				if (next - prev > chunksPerBatch)
					next = Mathf.Min(prev + chunksPerBatch, chunks);

				int length = next - prev;
				UnityEngine.Debug.Assert(length <= maxSize, "Trying to create too large cache (length > maxSize)");
				if (length > 0)
				{
					TryLoad(prev, length, startChunk, endChunk);
					return true;
				}
				chunk = index < Cache.Count ? Cache[index].End : endChunk;
			}
			return false;
		}
		void BlockRenderer(RenderTexture renderTarget, Material material, float time)
		{
			float chunksPerSecond = frequency / FFTWindow * plan.multisamples;
			float halfWindowTime = 0.5f * FFTWindow / frequency;

			float startTime = time - timeScale / 2.0f - halfWindowTime;
			float endTime = time + timeScale / 2.0f;

			int startChunk = Mathf.FloorToInt(startTime * chunksPerSecond);
			int centerChunk = Mathf.FloorToInt(time * chunksPerSecond);
			int endChunk = Mathf.CeilToInt(endTime * chunksPerSecond);

			//first pass - draw empty block
			Graphics.Blit(null, renderTarget, background);

			startChunk = Mathf.Max(0, startChunk);
			endChunk = Mathf.Min(chunks, endChunk);
			int visibleChunks = endChunk - startChunk;

			//second pass - draw previous data
			foreach(CacheBlock cache in oldCache)
			{
				if(cache.OverlapTime(startTime, endTime))
				{
					material.SetFloat(_TimeStart, cache.timeStart);
					material.SetFloat(_TimeEnd, cache.timeEnd);
					Graphics.Blit(cache.texture, renderTarget, material);
				}
			}

			//third pass - draw actual data
			int rendered = 0;
			foreach(CacheBlock block in Cache)
			{
				int visibleBlockPart = block.OverlappedRange(startChunk, endChunk);
				if (visibleBlockPart > 0)
				{
					material.SetFloat(_TimeStart, block.timeStart);
					material.SetFloat(_TimeEnd, block.timeEnd);
					Graphics.Blit(block.texture, renderTarget, material);
					rendered += visibleBlockPart;
				}
			}
			
			if(rendered < visibleChunks)
			{
				// There are detected an empty space
				if (IsWorking()) // Skip if a job creating new cache
				{
					Progress = (float)rendered / visibleChunks;
					return;
				}
				else
				{
					Progress = 1.0f;
				}

				if (syncCache != null)
				{
					syncCache();
					syncCache = null;
				}
				if (Cache.Count == 0)
				{
					int loadLength = Mathf.Min(chunksPerBatch, visibleChunks);
					TryLoad(startChunk, loadLength, startChunk, endChunk);
				}
				else if (!ScanAndLoadBlocks(centerChunk, endChunk))
				{
					ScanAndLoadBlocks(startChunk, endChunk);
				}
			}
			else if(oldCache.Count > 0)  // Delete old cached data when it was fully replaced by a new data
			{
				foreach(CacheBlock cache in oldCache)
					cache.Dispose();
				oldCache.Clear();
				oldMaximum = 0;
			}
			else
			{
				Progress = 1.0f;
			}
		}
		void ISpectrumRenderer.SetTask(BitAnimator.RecordSlot slot, PlotGraphic type)
		{
			if (activeThreads > 0)
			{
				InteruptWork();
			}
			if (type == plotType)
			{
				oldCache.AddRange(Cache);
				oldCache.Sort();
				Cache.Clear();
				oldMaximum = maximum;
				maximum = 0;
			}
			else
			{
				plotType = type;
				ClearCache();
			}
			if (spectrumMap != null)
				spectrumMapPin.Free();

			if (task != null)
				task.Dispose();

			syncCache = null;

			task = new LegacyTask(this, slot, chunks);
			if (!ApplyMods)
				task.legacyMods = new ILegacyModificator[0];

			cache = new FileInfo(spectrumCache);

			int calculatedMemory = (chunks * RealWindow * 8 + audioClip.samples * 4) / 1024 / 1024;
			blocks = (calculatedMemory - 1) / maxUseRAM + 1;
			chunksPerBatch = Mathf.Min(maxSize, (chunks - 1) / (blocks * 8) + 1);
			textureWidth = Math.Min(maxSize, RealWindow);
			switch (plotType)
			{
			case PlotGraphic.Peaks:
				spectrumMap = new float[RealWindow * chunksPerBatch];
				tempData = new byte[chunksPerBatch * sizeof(float)];
				tempTexture = new Texture2D(chunksPerBatch, 1, TextureFormat.RFloat, false, true);
				break;
			case PlotGraphic.Spectrum:
				int vram_calculatedMemory = chunks * RealWindow / 1024 * sizeof(float) / 1024;
				//vram_calculatedMemory *= 8; // we want to use 8 blocks in the same time for smooth transition beetwen blocks 
				int vram_blocks = (vram_calculatedMemory - 1) / maxUseVRAM + 1;
				int vram_batches = (chunks - 1) / (vram_blocks * 8) + 1;
				chunksPerBatch = Mathf.Min(chunksPerBatch, vram_batches);

				spectrumMap = new float[RealWindow * chunksPerBatch];
				
				tempTexture = new Texture2D(textureWidth, chunksPerBatch, TextureFormat.RFloat, false, true);
				tempData = new byte[textureWidth * chunksPerBatch * sizeof(float)];
				GenerateFrequencyFilterTexture();
				spectrumMaterial.SetTexture("_MaskTex", maskTexture);
				break;
			case PlotGraphic.Histogram:
				ClearCache();
				chunksPerBatch = 1;
				tempTexture = new Texture2D(textureWidth, 1, TextureFormat.RFloat, false, true);
				Texture2D newTexture = new Texture2D(textureWidth, 1, TextureFormat.RFloat, true, true);
				newTexture.filterMode = FilterMode.Point;
				newTexture.wrapMode = TextureWrapMode.Clamp;
				CacheBlock cache = new CacheBlock();
				cache.texture = newTexture;
				Cache.Add(cache);
				spectrumMap = new float[RealWindow];
				tempData = new byte[textureWidth * sizeof(float)];
				GenerateFrequencyFilterTexture();
				histogrammMaterial.SetTexture("_MaskTex", maskTexture);
				break;
			}
			blocks = (chunks - 1) / chunksPerBatch + 1;
			spectrumMapPin = GCHandle.Alloc(spectrumMap, GCHandleType.Pinned);
			peaksMaterial.SetFloat("_TimeScale", timeScale);
			spectrumMaterial.SetFloat("_TimeScale", timeScale);
		}
		void GenerateFrequencyFilterTexture()
		{
			Color32[] colors = new Color32[textureWidth];
			maskTexture = new Texture2D(textureWidth, 1, TextureFormat.RGBA32, true, true);
			maskTexture.filterMode = FilterMode.Point;
			maskTexture.wrapMode = TextureWrapMode.Clamp;
			if (task.legacyResolver is LegacyMultiBandResolver)
			{
				float[] window = ((LegacyMultiBandResolver)task.legacyResolver).window;
				int end = maskTexture.width;
				int stride = window.Length / maskTexture.width;
				for (int i = 0; i < end; i++)
				{
					float v = window[i*stride];
					Color color;
					color.r = v;
					color.g = 0f;
					color.b = -v;
					color.a = 0.3f * Mathf.Min(1.0f, Mathf.Abs(v));
					colors[i] = color;
				}
			}
			else if (task.legacyResolver is LegacyDefaultSpectrumResolver)
			{
				DefaultSpectrumResolver resolver = ((LegacyDefaultSpectrumResolver)task.legacyResolver).resolver;
				float hzPerPixel = frequency / (maskTexture.width * 2);
				int start = Mathf.Max(0, Mathf.FloorToInt(resolver.startFreq / hzPerPixel));
				int end = Mathf.Min(maskTexture.width, Mathf.CeilToInt(resolver.endFreq / hzPerPixel));
				for (int i = start; i < end; i++)
				{
					Color color;
					color.r = 1f;
					color.g = 0f;
					color.b = 0f;
					color.a = 0.3f;
					colors[i] = color;
				}
			}
			//Buffer.BlockCopy(window, 0, tempData, 0, window.Length * sizeof(float));
			maskTexture.SetPixels32(colors, 0);
			maskTexture.Apply();
		}
		void ClearCache()
		{
			foreach(CacheBlock cache in Cache)
				cache.Dispose();

			foreach(CacheBlock cache in oldCache)
				cache.Dispose();

			Cache.Clear();
			oldCache.Clear();
			oldMaximum = 0;
			maximum = 0;
		}
		public override void Dispose()
		{
			lock(taskGuard)
			{
				if (task != null)
					task.Dispose();
				task = null;
			}
			if(tempTexture != null)
				Texture2D.DestroyImmediate(tempTexture);
			if(maskTexture != null)
				Texture2D.DestroyImmediate(maskTexture);
			tempTexture = null;
			maskTexture = null;
			ClearCache();
			cache = null;
			tempData = null;
			base.Dispose();
		}
	}
}
