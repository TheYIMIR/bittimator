
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

#pragma warning disable 420 //AudioVisualization.ComputeProgram.threads: A volatile field references will not be treated as volatile
namespace AudioVisualization
{
	using DSPLib;
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using UnityEngine;

	[EngineInfo("Compute shaders")]
	public partial class ComputeProgram : Engine
	{
#if UNITY_2018_1_OR_NEWER
		public static readonly int keyframeSize = 32;
#else
		public static readonly int keyframeSize = 20;
#endif

		#region Fields

		protected static readonly int _buffer;
		protected static readonly int _BufferStep;
		protected static readonly int _Channels;
		protected static readonly int _FFTWindow;
		protected static readonly int _RealWindow;
		protected static readonly int _Frequency;
		protected static readonly int _GridOffset;
		protected static readonly int _GridSize;
		protected static readonly int _Input;
		protected static readonly int _Keyframes;
		protected static readonly int _MaximumValues;
		protected static readonly int _MinimumValues;
		protected static readonly int _Multisamples;
		protected static readonly int _N;
		protected static readonly int _Output;
		protected static readonly int _OutputKeyframes;
		protected static readonly int _SampleStep;
		protected static readonly int _Scale;
		protected static readonly int _Source;
		protected static readonly int _Window;
		protected static readonly int _HalfWindowTime;
		protected static readonly int _ChunckTime;
		protected readonly Kernel AbsSpectrum;
		protected readonly Kernel AmplitudeSmooth;
		protected readonly Kernel BeatFinder;
		protected readonly Kernel ComputeResolve;
		protected readonly Kernel ConvertToQuaternions;
		protected readonly Kernel CopyBuffer;
		protected readonly Kernel CreateWindow;
		protected readonly Kernel CurveFilter;
		protected readonly Kernel MultiplyByCurve;
		protected readonly Kernel DampingKernel;
		protected readonly Kernel DecimateKeyframesKernel;
		protected readonly Kernel Derivative;
		protected readonly Kernel DFT_BPM;
		protected readonly Kernel DFT_Execute;
		protected readonly Kernel DivideKernel;
		protected readonly Kernel DolphChebyshevWindow;
		protected readonly Kernel FFT;
		protected readonly Kernel FFT_Init;
		protected readonly Kernel FFT_Part;
		protected readonly Kernel FillKeyframes;
		protected readonly Kernel FinalMax;
		protected readonly Kernel FinalSum;
		protected readonly Kernel FrequencySmooth;
		protected readonly Kernel GetPeaks;
		protected readonly Kernel IFFT;
		protected readonly Kernel IFFT_Part;
		protected readonly Kernel KeyframesCreator;
		protected readonly Kernel MergeChannels2;
		protected readonly Kernel MergeChannelsN;
		protected readonly Kernel MultiplyBuffers;
		protected readonly Kernel MultiplyKernel;
		protected readonly Kernel PartialSumBig;
		protected readonly Kernel PartialSumSmall;
		protected readonly Kernel PowerKernel;
		protected readonly Kernel PrefixSum;
		protected readonly Kernel PrefixSumLocal;
		protected readonly Kernel ReductionMax;
		protected readonly Kernel ReductionSum;
		protected readonly Kernel RemapGradientKernel;
		protected readonly Kernel RemapKernel;
		protected readonly Kernel ResolveMultisamplesKernel;
		protected readonly Kernel SpectrumLinearToLog;
		protected readonly Kernel Transpose;
		protected ComputeBuffer buffer;
		protected ComputeBuffer buffer2;
		protected ComputeShader utilsCS;
		protected ComputeShader spectrumCS;
		protected ComputeShader keyframingCS;
		protected ComputeBuffer deconvolutionBuffer;
		protected ComputeBuffer finalSumBuffer;
		protected ComputeBuffer keyframes;
		protected ComputeBuffer monoSound;
		protected ComputeBuffer normalizeBuffer;
		protected ComputeBuffer output;
		
		protected ComputeTask[] tasks;
		protected ComputeBuffer tempBuffer;
		protected ComputeBuffer tempBufferSmall;
		protected ComputeBuffer window;
		protected volatile int threads;
		protected int loadOffset;
		protected int loadLength;
		protected bool firstPass;
		protected bool enableConvolution;
		protected float resolveCoeff;
		protected float resolveFactor;

		#endregion

		#region Constructors

		public ComputeProgram()
		{
			threads = 0;
#if UNITY_EDITOR
			utilsCS = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/BitAnimator/Shaders/BitAnimatorUtils.compute");
			spectrumCS = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/BitAnimator/Shaders/BitAnimatorFT.compute");
	#if UNITY_2018_1_OR_NEWER
			keyframingCS = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/BitAnimator/Shaders/BitAnimatorKeyframingU2018_1.compute");
	#else
			keyframingCS = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/BitAnimator/Shaders/BitAnimatorKeyframingU5_4.compute");
	#endif
#else
			utilsCS = Resources.Load<ComputeShader>("Shaders/BitAnimatorKernels.compute");
			spectrumCS = Resources.Load<ComputeShader>("Shaders/BitAnimatorFT.compute");
	#if UNITY_2018_1_OR_NEWER
			keyframingCS = Resources.Load<ComputeShader>("Shaders/BitAnimatorKeyframingU2018_1.compute");
	#else
			keyframingCS = Resources.Load<ComputeShader>("Shaders/BitAnimatorKeyframingU5_4.compute");
	#endif
#endif
			CopyBuffer = new Kernel(utilsCS, "CopyBuffer");
			MergeChannels2 = new Kernel(utilsCS, "MergeChannels2");
			MergeChannelsN = new Kernel(utilsCS, "MergeChannelsN");
			Derivative = new Kernel(utilsCS, "Derivative");
			PowerKernel = new Kernel(utilsCS, "PowerKernel");
			Transpose = new Kernel(utilsCS, "Transpose");
			ReductionSum = new Kernel(utilsCS, "ReductionSum");
			FinalSum = new Kernel(utilsCS, "FinalSum");
			ReductionMax = new Kernel(utilsCS, "ReductionMax");
			FinalMax = new Kernel(utilsCS, "FinalMax");
			MultiplyKernel = new Kernel(utilsCS, "MultiplyKernel");
			MultiplyBuffers = new Kernel(utilsCS, "MultiplyBuffers");
			DivideKernel = new Kernel(utilsCS, "DivideKernel");
			PartialSumBig = new Kernel(utilsCS, "PartialSumBig");
			PartialSumSmall = new Kernel(utilsCS, "PartialSumSmall");
			PrefixSum = new Kernel(utilsCS, "PrefixSum");
			PrefixSumLocal = new Kernel(utilsCS, "PrefixSumLocal");
			DampingKernel = new Kernel(utilsCS, "DampingKernel");
			SpectrumLinearToLog = new Kernel(utilsCS, "SpectrumLinearToLog");
			AmplitudeSmooth = new Kernel(utilsCS, "AmplitudeSmooth");
			FrequencySmooth = new Kernel(utilsCS, "FrequencySmooth");
			GetPeaks = new Kernel(utilsCS, "GetPeaks");
			BeatFinder = new Kernel(utilsCS, "BeatFinder");

			FillKeyframes = new Kernel(keyframingCS, "FillKeyframes");
			DecimateKeyframesKernel = new Kernel(keyframingCS, "DecimateKeyframesKernel");
			CurveFilter = new Kernel(keyframingCS, "CurveFilter");
			MultiplyByCurve = new Kernel(keyframingCS, "MultiplyByCurve");
			RemapKernel = new Kernel(keyframingCS, "RemapKernel");
			KeyframesCreator = new Kernel(keyframingCS, "KeyframesCreator");
			RemapGradientKernel = new Kernel(keyframingCS, "RemapGradientKernel");
			ConvertToQuaternions = new Kernel(keyframingCS, "ConvertToQuaternions");

			FFT_Init = new Kernel(spectrumCS, "FFT_Init");
			FFT_Part = new Kernel(spectrumCS, "FFT_Part");
			FFT = new Kernel(spectrumCS, "FFT_Execute");
			IFFT = new Kernel(spectrumCS, "IFFT_Execute");
			IFFT_Part = new Kernel(spectrumCS, "IFFT_Part");
			DFT_BPM = new Kernel(spectrumCS, "DFT_BPM");
			DFT_Execute = new Kernel(spectrumCS, "DFT_Execute");
			AbsSpectrum = new Kernel(spectrumCS, "AbsSpectrum");
			ComputeResolve = new Kernel(spectrumCS, "ComputeResolve");
			ResolveMultisamplesKernel = new Kernel(spectrumCS, "ResolveMultisamplesKernel");
			DolphChebyshevWindow = new Kernel(spectrumCS, "DolphChebyshevWindow");
			CreateWindow = new Kernel(spectrumCS, "CreateWindow");
		}
		static ComputeProgram()
		{
			_GridOffset = Shader.PropertyToID("_GridOffset");
			_GridSize = Shader.PropertyToID("_GridSize");
			_Input = Shader.PropertyToID("_Input");
			_Output = Shader.PropertyToID("_Output");
			_Keyframes = Shader.PropertyToID("_Keyframes");
			_OutputKeyframes = Shader.PropertyToID("_OutputKeyframes");
			_Window = Shader.PropertyToID("_Window");
			_buffer = Shader.PropertyToID("_Buffer");
			_MinimumValues = Shader.PropertyToID("_MinimumValues");
			_MaximumValues = Shader.PropertyToID("_MaximumValues");
			_Source = Shader.PropertyToID("_Source");
			_N = Shader.PropertyToID("_N");
			_FFTWindow = Shader.PropertyToID("_FFTWindow");
			_RealWindow = Shader.PropertyToID("_RealWindow");
			_Channels = Shader.PropertyToID("_Channels");
			_Frequency = Shader.PropertyToID("_Frequency");
			_Scale = Shader.PropertyToID("_Scale");
			_Multisamples = Shader.PropertyToID("_Multisamples");
			_SampleStep = Shader.PropertyToID("_SampleStep");
			_BufferStep = Shader.PropertyToID("_BufferStep");
			_HalfWindowTime = Shader.PropertyToID("_HalfWindowTime");
			_ChunckTime = Shader.PropertyToID("_ChunckTime");
		}

		#endregion

		#region Properties
		public override CoreType Type { get { return CoreType.ComputeShaders; } }
		public long UsedVRAM
		{
			get
			{
				long result = 0;
				result += monoSound == null ? 0 : (long)monoSound.count * monoSound.stride;
				result += window == null ? 0 : (long)window.count * window.stride;
				result += tempBuffer == null ? 0 : (long)tempBuffer.count * tempBuffer.stride;
				result += tempBufferSmall == null ? 0 : (long)tempBufferSmall.count * tempBufferSmall.stride;
				result += buffer == null ? 0 : (long)buffer.count * buffer.stride;
				result += buffer2 == null ? 0 : (long)buffer2.count * buffer2.stride;
				result += output == null ? 0 : (long)output.count * output.stride;
				result += keyframes == null ? 0 : (long)keyframes.count * keyframes.stride;
				result += normalizeBuffer == null ? 0 : (long)normalizeBuffer.count * normalizeBuffer.stride;
				result += finalSumBuffer == null ? 0 : (long)finalSumBuffer.count * finalSumBuffer.stride;
				return result;
			}
		}

		#endregion

		#region Methods

		public static void InitializeBuffer(ref ComputeBuffer buffer, int newCount, int stride)
		{
			if (buffer == null)
				buffer = new ComputeBuffer(newCount, stride);
			else if (buffer.count < newCount)
			{
				buffer.Release();
				buffer = new ComputeBuffer(newCount, stride);
			}
		}
		public override IEnumerator ComputeAnimation(IEnumerable<BitAnimator.RecordSlot> slots)
		{
			Status = "Allocating memory...";
			Progress = 0;
			tasks = slots.Select(slot => new ComputeTask(slot, chunks)).ToArray();
			//разделяем вычисление спектра на блоки
			int calculatedMemory = chunks * FFTWindow / 1024 * 21 / 1024;
			int blocks = (calculatedMemory - 1) / maxUseVRAM + 1;
			int batches = (chunks - 1) / blocks + 1;
			float sumProgress = plan.quality < 1.0f ? 5.0f : 1.0f;
			Status = "Loading audio...";
			LoadAudio(0, audioClip.samples);
			Status = "Creating a spectrum...";
			for (int block = 0; block < blocks; block++)
			{
				loadOffset = batches * block;
				//Вычисляем спектрограмму
				FFT_Execute(0, loadOffset, Math.Min(batches, chunks - 1 - loadOffset));
				//Преобразуем спектр из комплексных чисел в частотно-амплитудный спектр
				ConvertToSpectrum();
				ResolveMultisamples();
				foreach (ComputeTask task in tasks)
				{
					//Интегрируем полосу частот в 1D массив
					MergeBand(task.slot.startFreq, task.slot.endFreq, task.values, loadOffset);
				}
				Progress += 1.0f / blocks / sumProgress;
				yield return null;
			}
			Status = "Computing keyframes...";
			ComputeBuffer mask = new ComputeBuffer(chunks, 4);
			ComputeBuffer maskBuffer = new ComputeBuffer(chunks, 4);

			foreach (ComputeTask task in tasks)
			{
				ApplyModificators(task);

				if (task.slot.type == BitAnimator.PropertyType.Color)
					RemapGradient(task.values, task.keyframes, task.gradientKeyframes, task.Channels, task.gradientKeys, task.slot.loops);
				else
					CreateKeyframes(task.values, task.keyframes, task.slot.minValue, task.slot.maxValue, task.Channels, task.slot.loops);

				//перевод углов Эйлера в кватернионы
				if (task.slot.type == BitAnimator.PropertyType.Quaternion)
					ConvertKeyframesRotation(task.keyframes, task.values.count);
			}
			mask.Dispose();
			maskBuffer.Dispose();
			if (plan.quality < 1.0f)
			{
				Status = "Optimization keyframes...";
				float rmsQuality = Mathf.Pow(10.0f, -6.0f * plan.quality * plan.quality - 1.0f); //quality to RMS   [0..1] => [1e-1 .. 1e-7]
				foreach (ComputeTask task in tasks)
				{
					for (int channel = 0; channel < task.Channels; channel++)
					{
						Keyframe[] keyframes = task.GetKeyframes(channel);
						Interlocked.Increment(ref threads);
						ThreadPool.QueueUserWorkItem((object i) =>
						{
							int c = (int)i;
							task.hostKeyframes[c] = LegacyProgram.DecimateAnimationCurve(keyframes, rmsQuality * task.AmplitudeRange(c));
							Interlocked.Decrement(ref threads);
						}, channel);
					}
					yield return new WaitWhile(IsWorking);
				}
			}
			Status = "Keyframes calculated";
		}
		public override IEnumerator ComputeBPM()
		{
			Status = "Calculation BPM...";
			Progress = 0;
			yield return null;

			ComputeBuffer peaks = new ComputeBuffer(chunks, 4);
			ComputeBuffer bpmsBuffer = new ComputeBuffer(512, 4);
			//разделяем вычисление спектра на блоки
			int calculatedMemory = chunks * FFTWindow / 1024 * 21 / 1024;
			int blocks = (calculatedMemory - 1) / maxUseVRAM + 1;
			int batches = (chunks - 1) / blocks + 1;

			Status = "Loading audio...";
			LoadAudio(0, audioClip.samples);
			Status = "Creating a spectrum...";
			for (int block = 0; block < blocks; block++)
			{
				loadOffset = batches * block;
				int count = Math.Min(batches, chunks - 1 - loadOffset);
				//Вычисляем спектрограмму
				FFT_Execute(0, loadOffset, count);
				//Преобразуем спектр из комплексных чисел в частотно-амплитудный спектр
				ConvertToSpectrum();
				ResolveMultisamples();

				//Интегрируем полосу частот в 1D массив
				MergeBand(0, audioClip.frequency / 2, peaks, loadOffset);

				yield return null;
			}
			spectrumCS.SetBuffer(DFT_BPM.ID, _Input, peaks);
			spectrumCS.SetBuffer(DFT_BPM.ID, _Output, bpmsBuffer);
			spectrumCS.SetInts(_GridOffset, 0, 0, 0);
			spectrumCS.SetInts(_GridSize, chunks, 1, 1);
			spectrumCS.Dispatch(DFT_BPM.ID, 256, 1, 1);
			peaks.Dispose();
			Vector2[] bpmPhase = new Vector2[256];
			bpmsBuffer.GetData(bpmPhase);
			bpmsBuffer.Dispose();
			float maxAmp = 0;
			int idxMax = 0;
			float[] bpms = new float[256];
			for (int i = 0; i < 256; i++)
			{
				float amp = bpmPhase[i].x;
				bpms[i] = amp;
				if (amp > maxAmp)
				{
					maxAmp = amp;
					idxMax = i;
				}
			}
			float phase = bpmPhase[idxMax].y;
			if(BitAnimator.debugMode)
			{
				StringBuilder str = new StringBuilder();
				for(int i = 0; i < 256; i++)
					bpms[i] /= maxAmp;
				int[] keys = Enumerable.Range(0, 256).ToArray();
				Array.Sort(bpms, keys);
				for(int i = 255; i >= 0; i--)
				{
					str.AppendFormat("BPM = {0} : match = {1:F6} offset = {2:F2}\n", keys[i] + 40, bpms[i], 60.0f / (keys[i] + 40) * bpmPhase[idxMax].y / (2.0f * Mathf.PI) * 1000.0f);
				}
				str.AppendLine("Phase = " + phase / (2.0f * Mathf.PI));
				Debug.Log(str);
			}
			if (phase < 0)
				phase += 2.0f * Mathf.PI;

			bpm = idxMax + 40;
			beatOffset = 60.0f / bpm * phase / (2.0f * Mathf.PI) * 1000.0f;
		}
		public override void Dispose()
		{
			FreeMemory();
			if (monoSound != null) monoSound.Dispose();
			if (deconvolutionBuffer != null) deconvolutionBuffer.Dispose();
			if (window != null) window.Dispose();
			monoSound = null;
			deconvolutionBuffer = null;
			window = null;
		}
		public void FreeMemory()
		{
			if (tempBuffer != null) tempBuffer.Dispose();
			if (tempBufferSmall != null) tempBufferSmall.Dispose();
			if (buffer != null) buffer.Dispose();
			if (buffer2 != null) buffer2.Dispose();
			if (output != null) output.Dispose();
			if (normalizeBuffer != null) normalizeBuffer.Dispose();
			if (finalSumBuffer != null) finalSumBuffer.Dispose();
			if (keyframes != null) keyframes.Dispose();
			keyframes = null;
			buffer = null;
			buffer2 = null;
			tempBufferSmall = null;
			tempBuffer = null;
			finalSumBuffer = null;
			normalizeBuffer = null;
			output = null;
		}
		public override IEnumerable<Task> GetTasks()
		{
			return tasks.Cast<Task>();
		}
		public override void Initialize(EngineSettings settings, Plan _plan, AudioClip clip)
		{
			if (plan != _plan)
			{
				Status = "Initialization...";
				plan = _plan;
				FFTWindow = 1 << plan.windowLogSize;
				RealWindow = FFTWindow / 2;
				utilsCS.SetInt(_RealWindow, RealWindow);

				InitializeBuffer(ref window, FFTWindow, 4);
				spectrumCS.SetInt(_FFTWindow, FFTWindow);
				spectrumCS.SetInt(_Multisamples, plan.multisamples);
				spectrumCS.SetFloat(_Scale, plan.windowParam);
				if (plan.filter == DSP.Window.Type.DolphChebyshev)
				{
					spectrumCS.SetBuffer(DolphChebyshevWindow.ID, _Output, window);
					spectrumCS.Dispatch(DolphChebyshevWindow.ID, FFTWindow, 1, 1);
					Normalize(window);
				}
				else
				{
					spectrumCS.SetBuffer(CreateWindow.ID, _Output, window);
					spectrumCS.SetInt(_N, (int)plan.filter);
					spectrumCS.DispatchGrid(CreateWindow, FFTWindow);
				}
				InitializeBuffer(ref finalSumBuffer, 1, 4);
				utilsCS.SetBuffer(FinalSum.ID, _Input, window);
				utilsCS.SetBuffer(FinalSum.ID, _Output, finalSumBuffer);
				utilsCS.SetInts(_GridSize, FFTWindow, 1, 1);
				utilsCS.Dispatch(FinalSum.ID, 1, 1, 1);
				float[] windowSum = new float[1];
				finalSumBuffer.GetData(windowSum);
				windowScale = FFTWindow / windowSum[0];
				if (enableConvolution && plan.multisamples > 1)
				{
					int deconvSize = plan.multisamples / 2 * 2 + 1;
					InitializeBuffer(ref deconvolutionBuffer, deconvSize * deconvSize, 4);
					spectrumCS.SetBuffer(ComputeResolve.ID, _Window, window);
					spectrumCS.SetBuffer(ComputeResolve.ID, _Output, deconvolutionBuffer);
					spectrumCS.Dispatch(ComputeResolve.ID, 1, plan.multisamples / 2 + 1, 1);

					float[] windowParts = new float[deconvSize * deconvSize];
					deconvolutionBuffer.GetData(windowParts, 0, 0, deconvSize);
					double[][] matrix = MatrixMath.CreateToeplitz(windowParts.Take(deconvSize).ToArray());
					double[][] inverseM = MatrixMath.MatrixInverse(matrix);
					for (int i = 0; i < deconvSize; i++)
						for (int j = 0; j < deconvSize; j++)
							windowParts[i * deconvSize + j] = (float)inverseM[i][j];

					deconvolutionBuffer.SetData(windowParts);
				}
				if (audioClip != null)
					InitializeAudioClip(false);

				Status = "Initialized";
			}
			if (audioClip.Replace(clip))
			{
				frequency = audioClip.frequency;
				InitializeAudioClip(true);
			}
		}
		void InitializeAudioClip(bool clearCachedAudio)
		{
			chunks = audioClip.samples / FFTWindow * plan.multisamples - (plan.multisamples - 1);
			float chunkTime = FFTWindow / frequency / plan.multisamples;
			float halfWindowTime = FFTWindow / frequency / 2;
			keyframingCS.SetFloat(_ChunckTime, chunkTime);
			keyframingCS.SetFloat(_HalfWindowTime, halfWindowTime);
			if(clearCachedAudio && monoSound != null)
			{
				monoSound.Dispose();
				monoSound = null;
			}
		}
		internal static void Swap<T>(ref T lhs, ref T rhs)
		{
			T temp = lhs;
			lhs = rhs;
			rhs = temp;
		}
		internal void ApplyFading(ComputeBuffer input, ComputeBuffer output, int count, float fadeSpeed)
		{
			utilsCS.SetInts(_GridOffset, 0, 0, 0);
			utilsCS.SetInts(_GridSize, count, 1, 1);
			float chunkTime = (float)FFTWindow / plan.multisamples / frequency;
			bool swap = true;
			for (int offset = 1; offset < count; offset *= 2)
			{
				float factor = Mathf.Exp(-fadeSpeed * chunkTime * offset);
				utilsCS.SetBuffer(DampingKernel.ID, _Input, input);
				utilsCS.SetBuffer(DampingKernel.ID, _Output, output);
				utilsCS.SetFloat(_Scale, factor);
				utilsCS.SetInt(_N, offset);
				utilsCS.DispatchGrid(DampingKernel, count);
				Swap(ref input, ref output);
				swap ^= true;
			}
			if (swap)
			{
				utilsCS.SetBuffer(CopyBuffer.ID, _Input, input);
				utilsCS.SetBuffer(CopyBuffer.ID, _Output, output);
				utilsCS.DispatchGrid(CopyBuffer, count);
			}
		}
		internal void ApplyRemap(ComputeBuffer input, ComputeBuffer output, int count, ComputeBuffer remap)
		{
			keyframingCS.SetBuffer(RemapKernel.ID, _Input, input);
			keyframingCS.SetBuffer(RemapKernel.ID, _Output, output);
			keyframingCS.SetBuffer(RemapKernel.ID, _Keyframes, remap);
			keyframingCS.SetInts(_GridOffset, 0, 0, 0);
			keyframingCS.SetInts(_GridSize, count, 1, remap.count);
			keyframingCS.DispatchGrid(RemapKernel, count);
		}
		internal void CalculateLoudnessVelocity(ComputeBuffer input, ComputeBuffer output)
		{
			utilsCS.SetBuffer(Derivative.ID, _Input, input);
			utilsCS.SetBuffer(Derivative.ID, _Output, output);
			utilsCS.SetInts(_GridOffset, 0, 0, 0);
			utilsCS.SetInts(_GridSize, input.count, 1, 1);
			utilsCS.DispatchGrid(Derivative, input.count);
		}
		internal void CalculatePrefixSum(ComputeBuffer input, ComputeBuffer output, int count)
		{
			utilsCS.SetBuffer(PrefixSum.ID, _Input, input);
			utilsCS.SetBuffer(PrefixSum.ID, _Output, output);
			utilsCS.SetInts(_GridOffset, 0, 0, 0);
			utilsCS.SetInts(_GridSize, count, 1, 1);
			bool swap = true;
			for (int offset = 1; offset < count; offset *= 2)
			{
				utilsCS.SetBuffer(PrefixSum.ID, _Input, input);
				utilsCS.SetBuffer(PrefixSum.ID, _Output, output);
				utilsCS.SetInt(_N, offset);
				utilsCS.DispatchGrid(PrefixSum, count);
				Swap(ref input, ref output);
				swap ^= true;
			}
			if (swap)
			{
				utilsCS.SetBuffer(CopyBuffer.ID, _Input, input);
				utilsCS.SetBuffer(CopyBuffer.ID, _Output, output);
				utilsCS.DispatchGrid(CopyBuffer, count);
			}
		}
		internal void ComputeDolphChebyshevWindow()
		{
			InitializeBuffer(ref buffer, FFTWindow, 16);
			spectrumCS.SetInts(_GridOffset, 0, 0, 0);
			spectrumCS.SetInts(_GridSize, FFTWindow, 1, 1);
			spectrumCS.SetBuffer(DolphChebyshevWindow.ID, _buffer, buffer);
			spectrumCS.DispatchGrid(DolphChebyshevWindow, FFTWindow);
			bufferSwap = 0;
			if (FFTWindow > IFFT.size.x)
			{
				spectrumCS.SetBuffer(IFFT_Part.ID, _buffer, buffer);
				for (int n = 2; n <= FFTWindow; n *= 2)
				{
					spectrumCS.SetInt(_Source, bufferSwap);
					spectrumCS.SetInt(_N, n);
					spectrumCS.DispatchGrid(FFT_Part, FFTWindow);
					bufferSwap ^= 1;
				}
			}
			else
			{
				spectrumCS.SetBuffer(IFFT.ID, _Window, window);
				spectrumCS.SetBuffer(IFFT.ID, _buffer, buffer);
				spectrumCS.SetInt(_Source, bufferSwap);
				spectrumCS.DispatchGrid(IFFT, FFTWindow);
				bufferSwap ^= 1;
			}
		}
		internal void ConvertKeyframesRotation(ComputeBuffer output, int count)
		{
			keyframingCS.SetBuffer(ConvertToQuaternions.ID, _OutputKeyframes, output);
			keyframingCS.SetInts(_GridSize, count, 1, 1);
			keyframingCS.DispatchGrid(ConvertToQuaternions, count);
		}
		internal void ConvertToSpectrum()
		{
			InitializeBuffer(ref output, buffer.count / 2, 4);
			spectrumCS.SetBuffer(AbsSpectrum.ID, _buffer, buffer);
			spectrumCS.SetBuffer(AbsSpectrum.ID, _Output, output);
			spectrumCS.SetInts(_GridOffset, 0, 0, 0);
			spectrumCS.SetInts(_GridSize, RealWindow, spectrumChunks, 1);
			spectrumCS.SetFloat(_Scale, windowScale);
			spectrumCS.SetInt(_Source, bufferSwap);
			spectrumCS.SetInt(_N, (int)plan.mode);
			spectrumCS.DispatchGrid(AbsSpectrum, RealWindow, spectrumChunks);
		}
		internal void CreateKeyframes(ComputeBuffer input, ComputeBuffer output, Vector4 min, Vector4 max, int channels, int loopCount)
		{
			keyframingCS.SetBuffer(KeyframesCreator.ID, _Input, input);
			keyframingCS.SetBuffer(KeyframesCreator.ID, _OutputKeyframes, output);
			keyframingCS.SetFloats(_MinimumValues, min.x, min.y, min.z, min.w);
			keyframingCS.SetFloats(_MaximumValues, max.x, max.y, max.z, max.w);
			keyframingCS.SetInts(_GridOffset, 0, 0, 0);
			keyframingCS.SetInts(_GridSize, input.count, 1, 1);
			keyframingCS.SetInt(_Source, loopCount);
			keyframingCS.SetInt(_N, channels);
			keyframingCS.DispatchGrid(KeyframesCreator, input.count);
		}
		internal void DecimateKeyframes(ComputeBuffer keyframes)
		{
			throw new NotImplementedException();
		}
		internal void FFT_Execute(int sampleOffset = 0, int chunkOffset = 0, int batches = 0)
		{
			if (batches < 1)
				batches = chunks;
			spectrumChunks = batches;
			InitializeBuffer(ref buffer, batches * FFTWindow, 16);
			spectrumCS.SetInts(_GridOffset, sampleOffset, chunkOffset, 0);
			spectrumCS.SetInts(_GridSize, FFTWindow, batches, 1);
			spectrumCS.SetInt(_BufferStep, 1);
			spectrumCS.SetInt(_SampleStep, 1);
			spectrumCS.SetBuffer(FFT_Init.ID, _Input, monoSound);
			spectrumCS.SetBuffer(FFT_Init.ID, _Window, window);
			spectrumCS.SetBuffer(FFT_Init.ID, _buffer, buffer);
			spectrumCS.DispatchGrid(FFT_Init, FFTWindow, batches);
			bufferSwap = 0;
			if (FFTWindow > FFT.size.x)
			{
				spectrumCS.SetBuffer(FFT_Part.ID, _buffer, buffer);
				for (int n = 2; n <= FFTWindow; n *= 2)
				{
					spectrumCS.SetInt(_Source, bufferSwap);
					spectrumCS.SetInt(_N, n);
					spectrumCS.DispatchGrid(FFT_Part, FFTWindow, batches);
					bufferSwap ^= 1;
				}
			}
			else
			{
				spectrumCS.SetBuffer(FFT.ID, _Window, window);
				spectrumCS.SetBuffer(FFT.ID, _buffer, buffer);
				spectrumCS.SetInt(_Source, bufferSwap);
				spectrumCS.Dispatch(FFT.ID, 1, batches, 1);
				bufferSwap ^= 1;
			}
		}
		internal int GetBPM()
		{
			if (audioClip == null)
				return 0;

			float[] bpms = new float[512];
			CalcBPM();
			int bpm = 0;
			float value = 0;
			StringBuilder bpms_s = new StringBuilder(12000);
			bpms_s.AppendLine("---BeatFinder---");
			for (int i = 0; i < 256; i++)
			{
				float v = bpms[i];
				if (v > value)
				{
					bpm = i;
					value = v;
				}
				bpms_s.AppendFormat("{0,3} bpm: value={1:F6} phase={2,6:F3}\n", i + 40, v, bpms[i + 256]);
			}
			bpms_s.Insert(0, "Best match = " + (bpm + 40) + "\n");
			Debug.Log(bpms_s);
			return bpm + 40;
		}
		internal float GetMax(ComputeBuffer _input, int count, int offset = 0)
		{
			int partSumCount = (count - 1) / ReductionMax.size.x + 1;
			InitializeBuffer(ref normalizeBuffer, partSumCount, 4);
			InitializeBuffer(ref finalSumBuffer, 1, 4);

			utilsCS.SetBuffer(ReductionMax.ID, _Input, _input);
			utilsCS.SetBuffer(ReductionMax.ID, _Output, normalizeBuffer);
			utilsCS.SetInts(_GridOffset, offset, 0, 0);
			utilsCS.SetInts(_GridSize, count, 1, 1);
			utilsCS.SetInt(_N, 1);
			utilsCS.Dispatch(ReductionMax.ID, partSumCount, 1, 1);

			utilsCS.SetBuffer(FinalMax.ID, _Input, normalizeBuffer);
			utilsCS.SetBuffer(FinalMax.ID, _Output, finalSumBuffer);
			utilsCS.SetInts(_GridSize, partSumCount, 1, 1);
			utilsCS.Dispatch(FinalMax.ID, 1, 1, 1);
			float[] max = new float[1];
			finalSumBuffer.GetData(max);
			return max[0];
		}
		internal float GetRMS(ComputeBuffer input, int count, int offset)
		{
			/*float[] values = new float[count];
			input.GetData(values);
			float cpuRMS = GetRMS(values);*/
			int partSumCount = (count - 1) / ReductionSum.size.x + 1;
			InitializeBuffer(ref tempBuffer, count, 4);
			InitializeBuffer(ref normalizeBuffer, partSumCount, 4);
			InitializeBuffer(ref finalSumBuffer, 1, 4);
			utilsCS.SetBuffer(PowerKernel.ID, _Input, input);
			utilsCS.SetBuffer(PowerKernel.ID, _Output, tempBuffer);
			utilsCS.SetInts(_GridOffset,offset, 0, 0);
			utilsCS.SetInts(_GridSize, count, 0, 0);
			utilsCS.SetFloat(_Scale, 2.0f);
			utilsCS.DispatchGrid(PowerKernel, count);

			if (Mathf.NextPowerOfTwo(count) < ReductionSum.size.x)
			{
				utilsCS.SetBuffer(PartialSumSmall.ID, _Input, tempBuffer);
				utilsCS.SetBuffer(PartialSumSmall.ID, _Output, normalizeBuffer);
				utilsCS.SetInts(_GridOffset, offset, 0, 0);
				utilsCS.SetInts(_GridSize, count, 1, 1);
				utilsCS.Dispatch(PartialSumSmall.ID, partSumCount, 1, 1);
			}
			else
			{
				utilsCS.SetBuffer(PartialSumBig.ID, _Input, tempBuffer);
				utilsCS.SetBuffer(PartialSumBig.ID, _Output, normalizeBuffer);
				utilsCS.SetInts(_GridOffset, offset, 0, 0);
				utilsCS.SetInts(_GridSize, count, 1, 1);
				utilsCS.Dispatch(PartialSumBig.ID, partSumCount, 1, 1);
			}
			utilsCS.SetBuffer(FinalMax.ID, _Input, normalizeBuffer);
			utilsCS.SetBuffer(FinalMax.ID, _Output, finalSumBuffer);
			utilsCS.SetInts(_GridSize, partSumCount, 1, 1);
			utilsCS.Dispatch(FinalMax.ID, 1, 1, 1);

			float[] rms = new float[1];
			finalSumBuffer.GetData(rms);
			rms[0] = Mathf.Sqrt(rms[0] / count);
			return rms[0];
		}
		internal float GetRMS(float[] values)
		{
			double dist = 0;
			for (int i = 0; i < values.Length; i++)
			{
				double v = values[i];
				dist += v * v;
			}
			return (float)Math.Sqrt(dist / values.Length);
		}
		internal void Max(ComputeBuffer input, Keyframe[] curve)
		{
			InitializeBuffer(ref keyframes, curve.Length, keyframeSize);
			keyframes.SetData(curve);
			keyframingCS.SetBuffer(CurveFilter.ID, _Output, input);
			keyframingCS.SetBuffer(CurveFilter.ID, _Keyframes, keyframes);
			keyframingCS.SetInts(_GridOffset, 0, 0, 0);
			keyframingCS.SetInts(_GridSize, input.count, 1, 1);
			keyframingCS.SetInt(_N, curve.Length);
			keyframingCS.DispatchGrid(CurveFilter, input.count);
		}
		internal void MergeBand(int startFreq, int endFreq, ComputeBuffer output, int offset)
		{
			float hzPerBin = frequency / FFTWindow;
			int start = Mathf.FloorToInt(startFreq / hzPerBin);
			int end = Mathf.CeilToInt(endFreq / hzPerBin);
			int count = Mathf.NextPowerOfTwo(end - start);
			if (count < ReductionSum.size.x)
			{
				utilsCS.SetBuffer(PartialSumSmall.ID, _Input, this.output);
				utilsCS.SetBuffer(PartialSumSmall.ID, _Output, output);
				utilsCS.SetInts(_GridOffset, start, 0, offset);
				utilsCS.SetInts(_GridSize, end, spectrumChunks, 1);
				utilsCS.DispatchGrid(PartialSumSmall, spectrumChunks);
			}
			else
			{
				utilsCS.SetBuffer(PartialSumBig.ID, _Input, this.output);
				utilsCS.SetBuffer(PartialSumBig.ID, _Output, output);
				utilsCS.SetInts(_GridOffset, start, 0, offset);
				utilsCS.SetInts(_GridSize, end, spectrumChunks, 1);
				utilsCS.Dispatch(PartialSumBig.ID, 1, spectrumChunks, 1);
			}
		}
		internal void MergeBand(ComputeTask task)
		{
			ICSModificator resolver = task.gpuMods.FirstOrDefault(mod => mod.Queue == Modificators.ExecutionQueue.SpectrumMerge);
			if(resolver == default(ICSModificator))
			{
				if(firstPass)
				{
					MergeBand(task.slot.startFreq, task.slot.endFreq, task.values, loadOffset);
				}
			}
			else
			{
				resolver.Apply(this, this.output, task.values);
			}
		}
		internal void Multiply(ComputeBuffer buffer, int offset, int count, float multiply)
		{
			utilsCS.SetBuffer(MultiplyKernel.ID, _Output, buffer);
			utilsCS.SetInts(_GridOffset, offset, 0, 0);
			utilsCS.SetInts(_GridSize, count, 1, 1);
			utilsCS.SetFloat(_Scale, multiply);
			utilsCS.DispatchGrid(MultiplyKernel, count);
		}
		internal void Multiply(ComputeBuffer values, ComputeBuffer mask)
		{
			utilsCS.SetBuffer(MultiplyBuffers.ID, _Input, mask);
			utilsCS.SetBuffer(MultiplyBuffers.ID, _Output, values);
			utilsCS.DispatchGrid(MultiplyBuffers, values.count);
		}
		internal void Multiply(ComputeBuffer input, Keyframe[] curve)
		{
			InitializeBuffer(ref keyframes, curve.Length, keyframeSize);
			keyframes.SetData(curve);
			keyframingCS.SetBuffer(MultiplyByCurve.ID, _Output, input);
			keyframingCS.SetBuffer(MultiplyByCurve.ID, _Keyframes, keyframes);
			keyframingCS.SetInts(_GridOffset, 0, 0, 0);
			keyframingCS.SetInts(_GridSize, input.count, 1, 1);
			keyframingCS.SetInt(_N, curve.Length);
			keyframingCS.DispatchGrid(MultiplyByCurve, input.count);
		}
		internal void Normalize(ComputeBuffer _input)
		{
			int partSumCount = (_input.count - 1) / ReductionMax.size.x + 1;
			InitializeBuffer(ref normalizeBuffer, partSumCount, 4);
			InitializeBuffer(ref finalSumBuffer, 1, 4);

			utilsCS.SetBuffer(ReductionMax.ID, _Input, _input);
			utilsCS.SetBuffer(ReductionMax.ID, _Output, normalizeBuffer);
			utilsCS.SetInts(_GridOffset, 0, 0, 0);
			utilsCS.SetInts(_GridSize, _input.count, 1, 1);
			utilsCS.SetInt(_N, 1);
			utilsCS.Dispatch(ReductionMax.ID, partSumCount, 1, 1);

			utilsCS.SetBuffer(FinalMax.ID, _Input, normalizeBuffer);
			utilsCS.SetBuffer(FinalMax.ID, _Output, finalSumBuffer);
			utilsCS.SetInts(_GridSize, partSumCount, 1, 1);
			utilsCS.Dispatch(FinalMax.ID, 1, 1, 1);

			utilsCS.SetBuffer(DivideKernel.ID, _Input, finalSumBuffer);
			utilsCS.SetBuffer(DivideKernel.ID, _Output, _input);
			utilsCS.DispatchGrid(DivideKernel, _input.count);
		}
		internal void Normalize(ComputeBuffer _input, RectInt region)
		{
			int partSumCount = (region.width * region.height - 1) / ReductionMax.size.x + 1;
			InitializeBuffer(ref normalizeBuffer, partSumCount, 4);
			InitializeBuffer(ref finalSumBuffer, region.height, 4);

			utilsCS.SetBuffer(ReductionMax.ID, _Input, _input);
			utilsCS.SetBuffer(ReductionMax.ID, _Output, normalizeBuffer);
			utilsCS.SetInts(_GridOffset, region.x, region.y, 0);
			utilsCS.SetInts(_GridSize, region.width, region.height, 1);
			utilsCS.SetInt(_N, region.width);
			utilsCS.DispatchGrid(ReductionMax, region.width * region.height);

			utilsCS.SetBuffer(FinalMax.ID, _Input, normalizeBuffer);
			utilsCS.SetBuffer(FinalMax.ID, _Output, finalSumBuffer);
			utilsCS.SetInts(_GridSize, partSumCount, 1, 1);
			utilsCS.Dispatch(FinalMax.ID, 1, region.height, 1);

			utilsCS.SetBuffer(DivideKernel.ID, _Input, finalSumBuffer);
			utilsCS.SetBuffer(DivideKernel.ID, _Output, _input);
			utilsCS.DispatchGrid(DivideKernel, region.width * region.height);
		}
		internal void RemapGradient(ComputeBuffer input, ComputeBuffer output, ComputeBuffer remap, int channels, int gradientKeys, int loopCount)
		{
			keyframingCS.SetBuffer(RemapGradientKernel.ID, _Input, input);
			keyframingCS.SetBuffer(RemapGradientKernel.ID, _OutputKeyframes, output);
			keyframingCS.SetBuffer(RemapGradientKernel.ID, _Keyframes, remap);
			keyframingCS.SetInts(_GridOffset, 0, 0, 0);
			keyframingCS.SetInts(_GridSize, input.count, channels, gradientKeys);
			keyframingCS.SetInt(_Source, loopCount);
			keyframingCS.SetInt(_N, remap.count - gradientKeys * 3); //N - alpha keyframes gradient count
			keyframingCS.DispatchGrid(RemapGradientKernel, input.count);
		}
		internal void ResolveMultisamples()
		{
			if ((plan.mode & Mode.ResolveMultisamles) != 0 && !Mathf.Approximately(resolveFactor, 0))
				ResolveMultisamples(output, spectrumChunks, RealWindow);
		}
		internal void ResolveMultisamples(ComputeBuffer input, int inputCount, int bandSize)
		{
			if (plan.multisamples <= 1)
				return;

			InitializeBuffer(ref buffer2, bandSize * inputCount, 4);
			spectrumCS.SetBuffer(ResolveMultisamplesKernel.ID, _Input, input);
			spectrumCS.SetBuffer(ResolveMultisamplesKernel.ID, _Window, deconvolutionBuffer);
			spectrumCS.SetBuffer(ResolveMultisamplesKernel.ID, _Output, buffer2);
			spectrumCS.SetInts(_GridSize, bandSize, inputCount, 1);
			spectrumCS.SetFloat(_MaximumValues, resolveCoeff);
			spectrumCS.SetFloat(_Scale, resolveFactor);
			spectrumCS.DispatchGrid(ResolveMultisamplesKernel, bandSize, inputCount);

			utilsCS.SetBuffer(CopyBuffer.ID, _Input, buffer2);
			utilsCS.SetBuffer(CopyBuffer.ID, _Output, input);
			utilsCS.SetInts(_GridOffset, 0, 0, 0);
			utilsCS.DispatchGrid(CopyBuffer, bandSize * inputCount);
		}
		internal void SetRemap(AnimationCurve curve)
		{
			if (keyframes != null)
				keyframes.Dispose();
			if (curve == null)
			{
				keyframes = null;
				return;
			}
			keyframes = new ComputeBuffer(curve.length, keyframeSize);
			keyframes.SetData(curve.keys);
		}
		internal void SmoothSpectrum(ComputeBuffer input, ComputeBuffer output, float factor)
		{
			float chunksPerSecond = frequency * plan.multisamples / FFTWindow;
			utilsCS.SetBuffer(FrequencySmooth.ID, _Input, input);
			utilsCS.SetBuffer(FrequencySmooth.ID, _Output, output);
			utilsCS.SetInts(_GridSize, input.count, 1, 1);
			utilsCS.SetInt(_N, Mathf.CeilToInt(factor * chunksPerSecond));
			utilsCS.DispatchGrid(FrequencySmooth, input.count);
		}
		protected void ApplyModificators(ComputeTask task)
		{
			foreach (ICSModificator mod in task.gpuMods)
			{
				mod.Apply(this, task.values, task.buffer);
				if (mod.UseTempBuffer)
					task.Swap();
			}
		}
		public void CalcBPM()
		{
			if(monoSound == null)
				LoadAudio(0, audioClip.samples);

			FFT_Execute(0, 0, chunks);
			//Преобразуем спектр из комплексных чисел в частотно-амплитудный спектр
			ConvertToSpectrum();
			InitializeBuffer(ref tempBuffer, chunks, 4);
			InitializeBuffer(ref tempBufferSmall, 512, 4);
			MergeBand(0, audioClip.frequency / 2, tempBuffer, 0);
			spectrumCS.SetBuffer(DFT_BPM.ID, _Input, tempBuffer);
			spectrumCS.SetBuffer(DFT_BPM.ID, _Output, tempBufferSmall);
			spectrumCS.SetInts(_GridOffset, 0, 0, 0);
			spectrumCS.SetInts(_GridSize, chunks, 1, 1);
			spectrumCS.Dispatch(DFT_BPM.ID, 256, 1, 1);
		}
		protected bool IsWorking()
		{
			return threads > 0;
		}
		protected void LoadAudio(int offset, int samples)
		{
			if (offset >= audioClip.samples)
				return;
			if (offset + samples > audioClip.samples)
				samples = audioClip.samples - offset;
			samples *= audioClip.channels;

			float[] multiChannelSamples = new float[samples];
			if (!audioClip.preloadAudioData)
				audioClip.LoadAudioData();
			audioClip.GetData(multiChannelSamples, offset);

			ComputeBuffer soundInput = new ComputeBuffer(samples, 4);
			soundInput.SetData(multiChannelSamples);

			spectrumCS.SetInt(_Frequency, audioClip.frequency);

			chunks = audioClip.samples / FFTWindow * plan.multisamples - (plan.multisamples - 1);
			int allignedSamples = chunks * FFTWindow;

			int partSumCount = (int)(((long)allignedSamples * plan.multisamples - 1) / ReductionMax.size.x + 1);
			InitializeBuffer(ref tempBuffer, partSumCount, 4);
			InitializeBuffer(ref tempBufferSmall, partSumCount, 4);

			Kernel kernelPrepass = MergeChannelsN;
			if (audioClip.channels == 1)
				kernelPrepass = CopyBuffer;
			else if (audioClip.channels == 2)
				kernelPrepass = MergeChannels2;

			InitializeBuffer(ref monoSound, allignedSamples, 4);
			utilsCS.SetBuffer(kernelPrepass.ID, _Input, soundInput);
			utilsCS.SetBuffer(kernelPrepass.ID, _Output, monoSound);
			utilsCS.SetInts(_GridOffset, 0, 0, 0);
			utilsCS.SetInts(_GridSize, chunks, FFTWindow, 1);
			utilsCS.SetInt(_Channels, audioClip.channels);
			utilsCS.DispatchGrid(kernelPrepass, chunks, FFTWindow);
			soundInput.Release();
		}
		#endregion
	}
}
#pragma warning restore 420