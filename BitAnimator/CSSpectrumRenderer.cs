
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace AudioVisualization
{
	[EngineInfo("Compute shaders renderer", Renderer = true)]
	public class CSSpectrumRenderer: ComputeProgram, ISpectrumRenderer
    {
		
		const int maxSize = 8192;
		protected static readonly int _RenderTexture = Shader.PropertyToID("_RenderTexture");
		protected static readonly int _RenderTexture_R = Shader.PropertyToID("_RenderTexture_R");
		protected static readonly int _Time = Shader.PropertyToID("_Time");
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
		protected ComputeShader rendererCS;
		protected Kernel SaveTextureR;
		protected Kernel SaveTextureRGBA;
		protected Kernel MergeTexturesR;
		protected Material background;
		protected Material peaksMaterial;
		protected Material histogrammMaterial;
		protected Material spectrumMaterial;
        protected ComputeTask task;
        protected ComputeBuffer renderBuffer;
		PlotGraphic plotType;
		float timeScale = 5.0f;
		float oldMaximum;
		float maximum;
		int blocks;
		int batches;
		public CSSpectrumRenderer()
		{
#if UNITY_EDITOR
			rendererCS = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/BitAnimator/Shaders/BitAnimatorRenderer.compute");
#else
            rendererCS = Resources.Load<ComputeShader>("Shaders/BitAnimatorRenderer.compute");
#endif
			SaveTextureR = new Kernel(rendererCS, "SaveTextureR");
			SaveTextureRGBA = new Kernel(rendererCS, "SaveTextureRGBA");
			MergeTexturesR = new Kernel(rendererCS, "MergeTexturesR");

			background = new Material(Shader.Find("Unlit/Color"));
			histogrammMaterial = new Material(Shader.Find("Unlit/BitAnimatorRenderer/Histogram"));
			peaksMaterial = new Material(Shader.Find("Unlit/BitAnimatorRenderer/Peaks"));
			spectrumMaterial = new Material(Shader.Find("Unlit/BitAnimatorRenderer/Spectrum"));
		}
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

		public override void Initialize(EngineSettings settings, Plan _plan, AudioClip clip)
		{
			_plan.mode |= Mode.LogFrequency;
			base.Initialize(settings, _plan, clip);

			int calculatedMemory = FFTWindow * 21 / 1024 * chunks / 1024;
			int maxVRAM = maxUseVRAM - audioClip.samples * 4 / 1024 / 1024;
			if(maxVRAM <= 0)
				throw new OutOfMemoryException("Failed to initialize spectrum renderer");

			blocks = (calculatedMemory - 1) / maxVRAM + 1;
			batches = (chunks - 1) / blocks + 1;

			float chunksPerSecond = frequency / FFTWindow * plan.multisamples;
			float halfWindowTime = 0.5f * FFTWindow / frequency;

			rendererCS.SetInt(_FFTWindow, FFTWindow);
			rendererCS.SetInt(_Multisamples, plan.multisamples);
			rendererCS.SetInt(_Frequency, audioClip.frequency);
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
        void ISpectrumRenderer.Render(Texture2D outTexture)
        {
            task = new ComputeTask(new BitAnimator.RecordSlot(), outTexture.width * outTexture.height);
            RenderTexture tempRT = new RenderTexture(outTexture.width, outTexture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                enableRandomWrite = true
            };
            //разделяем вычисление спектра на блоки
            int calculatedMemory = chunks * FFTWindow / 1024 * 21 / 1024;
            int blocks = (calculatedMemory - 1) / maxUseVRAM + 1;
            int batches = (chunks - 1) / blocks + 1;

            for (int block = 0; block < blocks; block++)
            {
                int offset = batches * block;
                //Вычисляем спектрограмму
                FFT_Execute(0, offset, Math.Min(batches, chunks - 1 - offset));
                //Преобразуем спектр из комплексных чисел в частотно-амплитудный спектр
                ConvertToSpectrum();
                ResolveMultisamples();
                ApplyRemap(output, tempBuffer, spectrumChunks * RealWindow, task.keyframes);
                Resize(tempBuffer, task.values, new RectInt(0, 0, RealWindow, spectrumChunks), new RectInt(0, 0, outTexture.height, spectrumChunks));
                Normalize(task.values);
                Render(task.values, tempRT, new RectInt(offset, 0, spectrumChunks, outTexture.height), task.gradientKeyframes, task.gradientKeys);
            }

            RenderTexture oldRT = RenderTexture.active;
            RenderTexture.active = tempRT;
            outTexture.ReadPixels(new Rect(0, 0, tempRT.width, tempRT.height), 0, 0);
            outTexture.Apply();
            RenderTexture.active = oldRT;
        }
		void ISpectrumRenderer.Render(RenderTexture renderTarget, float time, float multiply)
		{
			switch(plotType)
			{
				case PlotGraphic.Peaks:         RenderPeaks(renderTarget, time, multiply); break;
				case PlotGraphic.Spectrum:   RenderSpectrum(renderTarget, time, multiply); break;
				case PlotGraphic.Histogram: RenderHistogram(renderTarget, time, multiply); break;
			}
		}
		void ISpectrumRenderer.SetTask(BitAnimator.RecordSlot _task, PlotGraphic type)
        {
			if(type == plotType)
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
			if(task != null) task.Dispose();
			task = new ComputeTask(_task, plotType == PlotGraphic.Peaks ? chunks : 1);

			if(!ApplyMods)
			{
				foreach(ICSModificator mod in task.gpuMods)
					mod.Dispose();
				task.gpuMods = new ICSModificator[0];
			}
			peaksMaterial.SetFloat("_TimeScale", timeScale);
			spectrumMaterial.SetFloat("_TimeScale", timeScale);
			spectrumMaterial.SetFloat("_LowFrequency", task.slot.startFreq);
			spectrumMaterial.SetFloat("_HighFrequency", task.slot.endFreq);
			histogrammMaterial.SetFloat("_LowFrequency", task.slot.startFreq);
			histogrammMaterial.SetFloat("_HighFrequency", task.slot.endFreq);
			Progress = 0;
		}
        void Render(ComputeBuffer values, RenderTexture tempRT, RectInt rectInt, ComputeBuffer gradientKeyframes, int gradientKeys)
        {
            throw new NotImplementedException();
        }
		void InitAudiodata()
		{
			if(monoSound == null)
			{
				LoadAudio(0, audioClip.samples);
				rendererCS.SetInt(_Frequency, audioClip.frequency);
			}
		}
        void CopyToRenderTexture(ComputeBuffer buffer, RenderTexture texture, int offset = 0, float scale = 1.0f)
        {
			if(texture.format == RenderTextureFormat.R8 || texture.format == RenderTextureFormat.RFloat || texture.format == RenderTextureFormat.RHalf)
			{
				rendererCS.SetBuffer(SaveTextureR.ID, _Input, buffer);
				rendererCS.SetTexture(SaveTextureR.ID, _RenderTexture_R, texture);
				rendererCS.SetInts(_GridOffset, offset, 0, 0);
				rendererCS.SetInts(_GridSize, texture.width, texture.height, 1);
				rendererCS.SetFloat(_Scale, scale);
				rendererCS.DispatchGrid(SaveTextureR, texture.width, texture.height, 1);
			}
			else if(texture.format == RenderTextureFormat.ARGB32 || texture.format == RenderTextureFormat.ARGBFloat || texture.format == RenderTextureFormat.ARGBHalf)
			{
				rendererCS.SetBuffer(SaveTextureRGBA.ID, _Input, buffer);
				rendererCS.SetTexture(SaveTextureRGBA.ID, _RenderTexture, texture);
				rendererCS.SetInts(_GridOffset, offset, 0, 0);
				rendererCS.SetInts(_GridSize, texture.width, texture.height, 1);
				rendererCS.SetFloat(_Scale, scale);
				rendererCS.DispatchGrid(SaveTextureRGBA, texture.width, texture.height, 1);
			}
			else
				throw new ArgumentException("Unsupported RenderTextureFormat = " + texture.format);
        }
        void RenderHistogram(RenderTexture renderTarget, float time, float multiply)
        {
			InitAudiodata();

			Mode mode = plan.mode | Mode.LogFrequency;
            int updateChunks = 1;
            int samplesOffset = Mathf.FloorToInt(time * audioClip.frequency - updateChunks * FFTWindow / plan.multisamples / 2 - FFTWindow / 2);
            samplesOffset = Math.Max(0, samplesOffset);
            int availableChunks = (monoSound.count - samplesOffset) / FFTWindow * plan.multisamples - (plan.multisamples - 1);
            updateChunks = Math.Min(256, Math.Min(updateChunks, availableChunks));
            if (updateChunks <= 0)
                return;

			FFT_Execute(samplesOffset, 0, updateChunks);
            ConvertToSpectrum();
            if ((mode & Mode.ResolveMultisamles) != 0)
                ResolveMultisamples(output, updateChunks, RealWindow);

            InitializeBuffer(ref renderBuffer, updateChunks * RealWindow, 4);
            ComputeBuffer input = output;
            ComputeBuffer result = renderBuffer;

            if (updateChunks > 1)
            {
                utilsCS.SetBuffer(CopyBuffer.ID, _Input, input);
				utilsCS.SetBuffer(CopyBuffer.ID, _Output, result);
				utilsCS.SetInts(_GridOffset, RealWindow * (updateChunks / 2), 0, 0);
				utilsCS.DispatchGrid(CopyBuffer, RealWindow);
                Swap(ref input, ref result);
            }

			maximum = GetMax(input, RealWindow);

			RenderTexture texture = (Cache.Count > 0 ? Cache[0].texture : null) as RenderTexture;
			if(texture == null || texture.width != RealWindow || texture.height != 1)
			{
				if(texture != null)
					texture.Release();

				texture = new RenderTexture(RealWindow, 1, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
				{
					enableRandomWrite = true,
					filterMode = FilterMode.Point,
					wrapMode = TextureWrapMode.Clamp,
					useMipMap = true,
					autoGenerateMips = false
				};
				texture.Create();
				CacheBlock cache = new CacheBlock();
				cache.texture = texture;
				Cache.Add(cache);
			}
			CopyToRenderTexture(input, texture);
			texture.GenerateMips();

			histogrammMaterial.SetFloat(_Multiply, multiply);
			Graphics.Blit(texture, renderTarget, histogrammMaterial);
		}
		void LoadCurrentPart()
		{
			FFT_Execute(0, loadOffset, loadLength);
			ConvertToSpectrum();
			if(enableConvolution && (plan.mode & Mode.ResolveMultisamles) != 0)
				ResolveMultisamples(output, loadLength, RealWindow);
			MergeBand(task);
		}
		void LoadPeaks(CacheBlock cache)
		{
			InitAudiodata();
			loadOffset = cache.offset;
			loadLength = cache.length;
			RenderTexture texture = new RenderTexture(loadLength, 1, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
			{
				enableRandomWrite = true,
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
				useMipMap = true,
				autoGenerateMips = false
			};
			texture.Create();
			firstPass = true;
			if(ApplyMods)
			{
				int passCount = blocks == 1 ? 1 : (task.gpuMods.Length > 0 ? task.gpuMods.Max(mod => mod.MultipassRequired ? 2 : 1) : 1);
				if(passCount == 1)
				{
					LoadCurrentPart();
				}
				else
				{
					for(int pass = 0; pass < passCount; pass++)
					{
						firstPass = pass == 0;
						for(int block = 0; block < blocks; block++)
						{
							loadOffset = batches * block;
							loadLength = Math.Min(batches, chunks - loadOffset);
							LoadCurrentPart();
						}
					}
				}
				loadOffset = cache.offset;
				loadLength = cache.length;
				ApplyModificators(task);
			}
			else
			{
				LoadCurrentPart();
			}
			CopyToRenderTexture(task.values, texture, loadOffset);
			texture.GenerateMips();
			float timePerChunk = FFTWindow / frequency / plan.multisamples;
			float halfWindowTime = 0.5f * FFTWindow / frequency;
			cache.timeStart = cache.offset * timePerChunk + halfWindowTime;
			cache.timeEnd = cache.End * timePerChunk + halfWindowTime;
			cache.maxValue = GetMax(task.values, loadLength, loadOffset);
			cache.creationTime = Time.realtimeSinceStartup;
			cache.texture = texture;
			Cache.Add(cache);
			Cache.Sort();
			maximum = Mathf.Max(maximum, cache.maxValue);
			if(Cache.Count > 16)
				MergeCache(verticalConcat: false);
		}
		void RenderPeaks(RenderTexture renderTarget, float time, float multiply)
        {
			peaksMaterial.SetFloat(_AudioClipTime, time);
			peaksMaterial.SetFloat(_Multiply, multiply);
			peaksMaterial.SetVector(_RenderTargetSize, new Vector4(renderTarget.width, renderTarget.height, 1.0f / renderTarget.width, 1.0f / renderTarget.height));
			BlockRenderer(renderTarget, peaksMaterial, time);
		}
		void LoadSpectrum(CacheBlock cache)
		{
			firstPass = true;
			FFT_Execute(0, cache.offset, cache.length);
			ConvertToSpectrum();
			if(enableConvolution && (plan.mode & Mode.ResolveMultisamles) != 0)
				ResolveMultisamples(output, cache.length, RealWindow);

			RenderTexture texture = new RenderTexture(RealWindow, cache.length, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
			{
				anisoLevel = 16,
				enableRandomWrite = true,
				filterMode = FilterMode.Trilinear,
				wrapMode = TextureWrapMode.Clamp,
				useMipMap = true,
				autoGenerateMips = false
			};
			texture.Create();
			CopyToRenderTexture(output, texture);
			texture.GenerateMips();
			float timePerChunk = FFTWindow / frequency / plan.multisamples;
			float halfWindowTime = 0.5f * FFTWindow / frequency;
			cache.timeStart = cache.offset * timePerChunk + halfWindowTime;
			cache.timeEnd = cache.End * timePerChunk + halfWindowTime;
			cache.maxValue = GetMax(output, cache.length * RealWindow);
			cache.creationTime = Time.realtimeSinceStartup;
			cache.texture = texture;
			Cache.Add(cache);
			Cache.Sort();
			maximum = Mathf.Max(maximum, cache.maxValue);
			if(Cache.Count > 16)
				MergeCache(verticalConcat: true);
		}
		bool FreeVRAM(int kbytes, int startExcludeChunk, int endExcludeChunk)
		{
			foreach(CacheBlock block in oldCache.OrderBy(c => c.creationTime))
			{
				kbytes -= block.texture.width * block.texture.height * sizeof(float) / 1024;
				(block.texture as RenderTexture).Release();
				block.texture = null;
				if(kbytes <= 0)
					break;
			}
			oldCache.RemoveAll(cache => cache.texture == null);
			if(kbytes <= 0)
				return true;

			foreach(CacheBlock block in Cache.OrderBy(c => c.creationTime))
			{
				if(block.Overlap(startExcludeChunk, endExcludeChunk))
					continue;

				kbytes -= block.texture.width * block.texture.height * sizeof(float) / 1024;
				(block.texture as RenderTexture).Release();
				block.texture = null;
				if(kbytes <= 0)
					break;
			}
			Cache.RemoveAll(cache => cache.texture == null);
			return kbytes <= 0;
		}
		CacheBlock TryLoad(int offset, int count, int startChunk, int endChunk)
		{
			int maxVRAM = maxUseVRAM * 1024 / 2;
			int usedVRAM = Cache.Sum(c => c.texture.width * c.texture.height * sizeof(float) / 1024);
			int requestedVRAM = 1;
			if(plotType == PlotGraphic.Spectrum)
				requestedVRAM = RealWindow;

			requestedVRAM *= count * sizeof(float) / 1024;
			if(maxVRAM < usedVRAM + requestedVRAM)
			{
				if(FreeVRAM(usedVRAM + requestedVRAM - maxVRAM, startChunk, endChunk))
				{
					oldMaximum = oldCache.Count == 0 ? 0 : oldCache.Max(block => block.maxValue);
					maximum = Cache.Count == 0 ? 0 : Cache.Max(block => block.maxValue);
				}
				else
					return null;
			}
			CacheBlock cache = new CacheBlock
			{
				offset = offset,
				length = count,
			};
			if(plotType == PlotGraphic.Spectrum)
				LoadSpectrum(cache);
			else 
				LoadPeaks(cache);

			return cache;
		}
		void MergeCache(bool verticalConcat)
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
					int width = verticalConcat ? cache0.texture.width : sumLength;
					int height = verticalConcat ? sumLength : cache0.texture.height;
					if(width * height * sizeof(float) > maxVRAM)
						continue;
					RenderTexture newTexture = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
					{
						enableRandomWrite = true,
						filterMode = cache0.texture.filterMode,
						wrapMode = cache0.texture.wrapMode,
						useMipMap = true,
						autoGenerateMips = false
					};
					newTexture.Create();
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
					if(verticalConcat)
						Graphics.CopyTexture(cache1.texture, 0, 0, 0, 0, cache1.texture.width, cache1.texture.height, newCache.texture, 0, 0, 0, cache0.texture.height);
					else
						Graphics.CopyTexture(cache1.texture, 0, 0, 0, 0, cache1.texture.width, cache1.texture.height, newCache.texture, 0, 0, cache0.texture.width, 0);
					(newCache.texture as RenderTexture).GenerateMips();

					Cache.RemoveRange(i, 2);
					Cache.Insert(i, newCache);
					(cache0.texture as RenderTexture).Release();
					(cache1.texture as RenderTexture).Release();
				}
			}
		}
		void BlockRenderer(RenderTexture renderTarget, Material material, float time)
		{
			InitAudiodata();
			float chunksPerSecond = frequency / FFTWindow * plan.multisamples;
			float halfWindowTime = 0.5f * FFTWindow / frequency;

			float startTime = time - timeScale / 2.0f + halfWindowTime;
			float endTime = startTime + timeScale;

			int startChunk = Mathf.FloorToInt(startTime * chunksPerSecond);
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
			int index = Cache.BinarySearch(new CacheBlock(startChunk));
			index = (index >= 0 ? index : ~index);
			int rendered = 0;
			for(int i = Mathf.Max(0, index - 1); i < Cache.Count && Cache[i].offset < endChunk; i++)
			{
				CacheBlock block = Cache[i];
				material.SetFloat(_TimeStart, block.timeStart);
				material.SetFloat(_TimeEnd, block.timeEnd);
				Graphics.Blit(block.texture, renderTarget, material);
				rendered += Mathf.Min(block.End, endChunk) - Mathf.Max(block.offset, startChunk);
			}
			Progress = (float)rendered / visibleChunks;
			
			if(rendered < visibleChunks)
			{
				int maxLoad = Mathf.Min(maxSize, batches);
				if(Cache.Count == 0)
				{
					int loadLength = Mathf.Min(maxLoad, visibleChunks);
					TryLoad(startChunk, loadLength, startChunk, endChunk);
				}
				else
				{
					int chunk = startChunk;
					while(chunk < endChunk)
					{
						index = Cache.BinarySearch(new CacheBlock(chunk));
						index = (index >= 0 ? index : ~index);
						int next = Mathf.Min(chunk + maxLoad, index < Cache.Count ? Cache[index].offset : chunks);
						int prev = Mathf.Max(chunk - maxLoad, 0 < index ? Cache[index - 1].End : 0);
						if(next - chunk < chunk - prev)
							prev = Mathf.Max(next - maxLoad, prev);
						int length = next - prev;
						if(length > 0)
						{
							CacheBlock block = TryLoad(prev, length, startChunk, endChunk);
							if(block == null)
								break;
							maxLoad -= length;
							if(maxLoad <= 0)
								break;
							chunk = block.End;
							continue;
						}
						chunk = index < Cache.Count ? Cache[index].End : endChunk;
					}
				}
			}
			else if(oldCache.Count > 0)
			{
				foreach(CacheBlock cache in oldCache)
					(cache.texture as RenderTexture).Release();
				oldCache.Clear();
				oldMaximum = 0;
			}
		}
		void RenderSpectrum(RenderTexture renderTarget, float time, float multiply)
        {
			spectrumMaterial.SetFloat(_AudioClipTime, time);
			spectrumMaterial.SetFloat(_Multiply, multiply);
			spectrumMaterial.SetVector(_RenderTargetSize, new Vector4(renderTarget.width, renderTarget.height, 1.0f / renderTarget.width, 1.0f / renderTarget.height));
			BlockRenderer(renderTarget, spectrumMaterial, time);
		}
        void Resize(ComputeBuffer input, ComputeBuffer output, RectInt rectInt1, RectInt rectInt2)
        {
            throw new NotImplementedException();
        }
		void ClearCache()
		{
			foreach(CacheBlock cache in Cache)
				(cache.texture as RenderTexture).Release();

			foreach(CacheBlock cache in oldCache)
				(cache.texture as RenderTexture).Release();

			Cache.Clear();
			oldCache.Clear();
			oldMaximum = 0;
			maximum = 0;
		}
        public override void Dispose()
        {
			ClearCache();

			if (renderBuffer != null) renderBuffer.Dispose();
            if (task != null) task.Dispose();
			
			renderBuffer = null;
            task = null;
            base.Dispose();
        }
	}
}
