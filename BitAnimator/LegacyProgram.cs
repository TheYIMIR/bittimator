
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using UnityEngine;
using Numerics;
using DSPLib;

using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;

#pragma warning disable 420 // warning CS0420: 'AudioVisualization.LegacyProgram.threads': A volatile field references will not be treated as volatile
namespace AudioVisualization
{
	[EngineInfo("Legacy")]
	public partial class LegacyProgram : Engine
	{
		public override CoreType Type { get { return CoreType.Legacy; } }
		protected FFT[] fft2;
		protected LegacyProgramSettings settings;
		protected int processorCount;
		protected IntPtr fft;
		protected List<Thread> threads = new List<Thread>();

		protected float[] window;
		protected float[] monoSound;
		protected float[] spectrumMap;
		protected float[] buffer;
		protected LegacyTask[] tasks;
		protected string spectrumCache;
		protected int bytesPerSample;
		protected int loadOffset;
		protected int loadLength;
		protected int block;
		protected int blocks;
		protected int pass;
		protected int passCount;
		protected int audioOffset;
		protected int audioSamples;
		protected int cachedChunks;
		protected bool firstPass;
		protected FileInfo cache;
		protected FileStream cacheFile;
		protected volatile int activeThreads;
		protected object jobEvent = new object();
		protected object jobGuard = new object();
		protected GCHandle monoSoundPin;
		protected GCHandle spectrumMapPin;
		bool audioRequest;

		protected struct SpectrumPartRequest
		{
			public int thread;
			public int offset;
			public int count;
			public SpectrumPartRequest(int _thread, int _offset, int _count)
			{
				thread = _thread;
				offset = _offset;
				count = _count;
			}
		}
		public struct LogEvent
		{
			public enum TypeEvent { Log, Warning, Error, Exception }
			public TypeEvent typeEvent;
			public object content;
			public LogEvent(Exception ex) { typeEvent = TypeEvent.Exception; content = ex; }
			public LogEvent(string message) { typeEvent = TypeEvent.Log; content = message; }
		}
		public static List<LogEvent> log = new List<LogEvent>();

		// Функция проверяет работают ли потоки в фоновом режиме или нет
		// также синхронно загружает аудио если требуется
		// Функция должна вызываться в основном потоке, иначе при загрузке аудио юнити выдаст ошибку
		protected bool IsWorking()
		{
			if (audioRequest)
			{
				lock (audioClip)
				{
					LoadAudioChunks(loadOffset, loadLength);
					audioRequest = false;
					Monitor.Pulse(audioClip);
				}
			}
			if (fft != IntPtr.Zero)
				spectrumChunks = (int)NativeCore.GetProgress(fft);

			Progress = (float)(loadOffset + spectrumChunks) / chunks;
			return activeThreads > 0;
		}
		// Инициализирует движок для обработки спектрограммы, создания пиков и ключей анимации
		public override void Initialize(EngineSettings _settings, Plan _plan, AudioClip audio)
		{
			if (activeThreads > 0)
			{
				InteruptWork();
			}
			if (AssignSettings(_settings, _plan))
			{
				Status = "Initialization...";
				plan = _plan;
				FFTWindow = plan.WindowSize;
				RealWindow = FFTWindow / 2;

				processorCount = SystemInfo.processorCount;
				window = DSP.Window.Coefficients(plan.filter, (uint)FFTWindow, plan.windowParam);
				windowScale = DSP.Window.ScaleFactor.Signal(window);
				Multiply(window, windowScale);
				settings = _settings as LegacyProgramSettings;
				if (!InitializeNativeCore())
				{
					InitializeLegacyCore();
				}
				if (audioClip != null)
				{
					loadOffset = loadLength = audioOffset = audioSamples = 0;
					chunks = audioClip.samples / FFTWindow * plan.multisamples - (plan.multisamples - 1);
					spectrumCache = Path.Combine(Application.temporaryCachePath, audioClip.name + "_" + plan.GetHashCode() + ".cache");
					if (fft != IntPtr.Zero)
						NativeCore.SetFrequency(fft, (uint)audioClip.frequency);
				}
				Status = "Initialized";
			}
			if (audioClip.Replace(audio))
			{
				frequency = audioClip.frequency;
				loadOffset = loadLength = audioOffset = audioSamples = 0;
				chunks = audioClip.samples / FFTWindow * plan.multisamples - (plan.multisamples - 1);
				spectrumCache = Path.Combine(Application.temporaryCachePath, audioClip.name + "_" + plan.GetHashCode() + ".cache");
				if (fft != IntPtr.Zero)
					NativeCore.SetFrequency(fft, (uint)frequency);
			}
		}
		bool InitializeNativeCore()
		{
			if (settings != null)
			{
				if (!settings.useNativeCore)
				{
					if (fft != IntPtr.Zero)
					{
						NativeCore.Dispose(fft);
						fft = IntPtr.Zero;
					}
					return false;
				}

				if (fft == IntPtr.Zero)
				{
					if (!NativeCore.Load())
					{
						settings.useNativeCore = false;
						return false;
					}
				}
				else
				{
					NativeCore.Dispose(fft);
				}
				NativeCore.Instructions instructions = NativeCore.GetCPUInstructionSet();
				if (BitAnimator.debugMode)
				{
					Debug.Log("Available CPU Instruction set: " + instructions.ToString());
				}
				if ((instructions & NativeCore.Instructions.AVX2) == 0)
				{
					settings.useAVX = false;
				}
				if (!settings.useAVX)
				{
					instructions &= ~NativeCore.Instructions.AVX2;
				}
				GCHandle pin = GCHandle.Alloc(window, GCHandleType.Pinned);
				fft = NativeCore.CreateFFT(plan, window, (uint)processorCount, instructions);
				pin.Free();
				return true;
			}
			return false;
		}
		void InitializeLegacyCore()
		{
			fft2 = new FFT[SystemInfo.processorCount];
			for (int i = 0; i < fft2.Length; i++)
			{
				fft2[i] = new FFT();
				fft2[i].Initialize((uint)FFTWindow);
			}
		}
		protected void LoadAudioChunks(int offset, int count)
		{
			int offsetSamples = offset * FFTWindow / plan.multisamples;
			int countSamples = (count + plan.multisamples - 1) * FFTWindow / plan.multisamples;
			LoadAudio(offsetSamples, countSamples);
		}
		// Синхронно загружает порцию аудиоданных
		protected void LoadAudio(int offset, int samples)
		{
			if (offset >= audioClip.samples)
				return;

			if (offset + samples > audioClip.samples)
				samples = audioClip.samples - offset;

			audioOffset = offset;
			audioSamples = samples;
			samples *= audioClip.channels;
			float[] multiChannelSamples = new float[samples];
			if (!audioClip.preloadAudioData)
				audioClip.LoadAudioData();

			audioClip.GetData(multiChannelSamples, offset);

			if (monoSound != null)
				monoSoundPin.Free();

			if (audioClip.channels == 1)
			{
				monoSound = multiChannelSamples;
			}
			else if (audioClip.channels == 2)
			{
				monoSound = new float[audioSamples];
				for (int i = 0, j = 0; j < samples; i++, j += 2) monoSound[i] = (multiChannelSamples[j] + multiChannelSamples[j + 1]) * 0.5f;
			}
			else
			{
				monoSound = new float[audioSamples];
				int channels = audioClip.channels;
				for (int c = 0; c < channels; c++) for (int i = 0, j = c; j < samples; i++, j += channels) monoSound[i] += multiChannelSamples[j];
				float mul = 1.0f / channels;
				for (int i = 0; i < monoSound.Length; i++) monoSound[i] *= mul;
			}
			monoSoundPin = GCHandle.Alloc(monoSound, GCHandleType.Pinned);
		}
		// Корутина для создания ключей анимации
		public override IEnumerator ComputeAnimation(IEnumerable<BitAnimator.RecordSlot> slots)
		{
			Status = "Allocating memory...";
			tasks = slots.Select(slot => new LegacyTask(this, slot, chunks)).ToArray();

			Progress = 0;
			int calculatedMemory = (chunks * RealWindow * 4 + audioClip.samples * 4) / 1024 / 1024;
			blocks = (calculatedMemory - 1) / maxUseRAM + 1;
			int batches = (chunks - 1) / blocks + 1;
			if (spectrumMap != null)
				spectrumMapPin.Free();
			spectrumMap = new float[RealWindow * batches];
			spectrumMapPin = GCHandle.Alloc(spectrumMap, GCHandleType.Pinned);
			cache = new FileInfo(spectrumCache);
			passCount = blocks == 1 ? 1 : tasks.Max(task => task.legacyMods.Length > 0 ? task.legacyMods.Max(mod => mod.MultipassRequired ? 2 : 1) : 1);
			if (BitAnimator.debugMode)
			{
				for (pass = 0; pass < passCount; pass++)
				{
					firstPass = pass == 0;
					for (block = 0; block < blocks; block++)
					{
						int offset = batches * block;
						loadOffset = offset;
						loadLength = Math.Min(batches, chunks - offset);
						LoadSpectrumSync(offset, Math.Min(batches, chunks - offset));
						if (passCount > 1)
							Status = String.Format("Pass {0}/{1}. Computing peaks... {2}/{3}", pass + 1, passCount, block + 1, blocks);
						else
							Status = String.Format("Computing peaks... {0}/{1}", block + 1, blocks);

						foreach (LegacyTask task in tasks)
							MergeBand(task);
					}
				}
				Status = "Computing keyframes...";
				foreach (LegacyTask task in tasks)
					RunTask(task);
			}
			else
			{
				for (pass = 0; pass < passCount; pass++)
				{
					firstPass = pass == 0;
					for (block = 0; block < blocks; block++)
					{
						int offset = batches * block;
						loadOffset = offset;
						loadLength = Math.Min(batches, chunks - offset);
						EnqueueTask(LoadSpectrum, new SpectrumPartRequest(0, offset, Math.Min(batches, chunks - offset)));

						yield return new WaitWhile(IsWorking);
						if (passCount > 1)
							Status = String.Format("Pass {0}/{1}. Computing peaks... {2}/{3}", pass + 1, passCount, block + 1, blocks);
						else
							Status = String.Format("Computing peaks... {0}/{1}", block + 1, blocks);

						foreach (LegacyTask task in tasks)
							EnqueueTask(MergeBand, task);

						yield return new WaitWhile(IsWorking);
					}
				}
				Status = "Computing keyframes...";
				foreach (LegacyTask task in tasks)
					EnqueueTask(RunTask, task);

				yield return new WaitWhile(IsWorking);
			}
			Status = "Keyframes calculated";
			if (cacheFile != null)
				cacheFile.Dispose();
			cacheFile = null;
			cache = null;
			PrintLogs();
		}
		protected static void PrintLogs()
		{
			lock (log)
			{
				for (int s = 0; s < log.Count; s++)
				{
					switch (log[s].typeEvent)
					{
						case LogEvent.TypeEvent.Log: Debug.Log(log[s].content); break;
						case LogEvent.TypeEvent.Warning: Debug.LogWarning(log[s].content); break;
						case LogEvent.TypeEvent.Error: Debug.LogError(log[s].content); break;
						case LogEvent.TypeEvent.Exception: Debug.LogException(log[s].content as Exception); break;
					}
				}
				log.Clear();
			}
		}
		public override IEnumerable<Task> GetTasks()
		{
			return tasks.Cast<Task>();
		}
		public override IEnumerator ComputeBPM()
		{
			Status = "Calculation BPM...";
			Progress = 0;
			yield return null;

			Vector2[] sincos = new Vector2[256];

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
				yield return new WaitWhile(IsWorking);

				float[] peaks = new float[loadLength];
				for (int i = 0; i < loadLength; i++)
				{
					float sum = 0;
					for (int j = 0; j < RealWindow; j++)
						sum += spectrumMap[i * RealWindow + j];
					peaks[i] = sum;
				}
				float chunksPerMinute = frequency / FFTWindow * plan.multisamples * 60.0f;
				for (int i = 0; i < 256; i++)
				{
					float a1 = 2.0f * Mathf.PI * (i + 40) / chunksPerMinute;
					Vector2 integral = Vector2.zero;
					for (int t = 0; t < loadLength; t++)
					{
						float value = peaks[t]; float a = a1 * (t + loadOffset);
						integral.x += Mathf.Cos(a) * value;
						integral.y += Mathf.Sin(a) * value;
					}
					sincos[i] = integral;
				}
			}
			if (cacheFile != null)
				cacheFile.Dispose();
			cacheFile = null;
			cache = null;
			float maxAmp = 0;
			int idxMax = 0;
			float[] bpms = new float[256];
			float[] phases = new float[256];
			for (int i = 0; i < 256; i++)
			{
				float amp = sincos[i].magnitude / chunks;
				float phase = Mathf.Atan2(sincos[idxMax].y, sincos[idxMax].x);
				bpms[i] = amp;
				phases[i] = phase;
				if (amp > maxAmp)
				{
					maxAmp = amp;
					idxMax = i;
				}
			}
			if (BitAnimator.debugMode)
			{
				for (int i = 0; i < 256; i++)
					bpms[i] /= maxAmp;
				int[] keys = Enumerable.Range(0, 256).ToArray();
				Array.Sort(bpms, keys);
				StringBuilder str = new StringBuilder();
				for (int i = 255; i >= 0; i--)
				{
					str.AppendFormat("BPM = {0} : match = {1:F6} offset = {2:F2}\n", keys[i] + 40, bpms[i], 60.0f / (keys[i] + 40) * phases[idxMax] / (2.0f * Mathf.PI) * 1000.0f);
				}
				str.AppendLine("Phase = " + phases[idxMax] / (2.0f * Mathf.PI));
				Debug.Log(str);
			}
			if (phases[idxMax] < 0)
				phases[idxMax] += 2.0f * Mathf.PI;

			bpm = idxMax + 40;
			beatOffset = 60.0f / bpm * phases[idxMax] / (2.0f * Mathf.PI) * 1000.0f;
		}
		protected void EnqueueTask(Action action)
		{
			Interlocked.Increment(ref activeThreads);
			lock (threads)
			{
				threads.RemoveAll(t => (t.ThreadState & ThreadState.Stopped) != 0);
				Thread job = new Thread(() =>
				{
					try
					{
						action();
					}
					catch (ThreadInterruptedException)
					{
					}
					catch (Exception ex)
					{
						log.Add(new LogEvent(ex));
					}
					finally
					{
						lock (jobEvent)
						{
							Interlocked.Decrement(ref activeThreads);
							Monitor.PulseAll(jobEvent);
						}
					}
				});
				job.Name = "LegacyProgram";
				job.Priority = System.Threading.ThreadPriority.BelowNormal;
				job.Start();
				threads.Add(job);
			}
		}
		protected void EnqueueTask(Action<object> action, object arg)
		{
			Interlocked.Increment(ref activeThreads);
			lock (threads)
			{
				threads.RemoveAll(t => (t.ThreadState & ThreadState.Stopped) != 0);
				Thread job = new Thread((object o) =>
				{
				   try
				   {
					   action(o);
				   }
				   catch (ThreadInterruptedException)
				   {
				   }
				   catch (Exception ex)
				   {
					   log.Add(new LogEvent(ex));
				   }
				   finally
				   {
					   lock (jobEvent)
					   {
						   Interlocked.Decrement(ref activeThreads);
						   Monitor.PulseAll(jobEvent);
					   }
				   }
				});
				job.Name = "LegacyProgram thread";
				job.Priority = System.Threading.ThreadPriority.BelowNormal;
				job.Start(arg);
				threads.Add(job);
			}
		}
		protected void LoadSpectrum(object requestedPart)
		{
			SpectrumPartRequest part = (SpectrumPartRequest)requestedPart;
			int offsetChunks = part.offset;
			int chunksCount = part.count;
			spectrumChunks = 0;
			if (fft != IntPtr.Zero)
				NativeCore.Reset(fft);

			//if(LoadCache())
			//	return;

			if (passCount > 1)
				Status = String.Format("Pass {0}/{1}. Computing spectrum... {2}/{3}", pass + 1, passCount, block + 1, blocks);
			else
				Status = String.Format("Computing spectrum... {0}/{1}", block + 1, blocks);

			int currentThreads = activeThreads;

			if (BitAnimator.debugMode)
			{
				LoadAudioChunks(offsetChunks, chunksCount);
			}
			else
			{
				lock (audioClip)
				{
					audioRequest = true;
					Monitor.Wait(audioClip);
				}
			}
			if (chunksCount < processorCount)
			{
				CalculateSpectrumAsync(new SpectrumPartRequest(0, 0, chunksCount));
			}
			else
			{
				int partChunks = chunksCount / processorCount;
				int remainder = chunksCount % processorCount;
				for (int thread = 0, offset = 0; thread < processorCount; thread++)
				{
					int count = partChunks + (thread < remainder ? 1 : 0);
					if (BitAnimator.debugMode)
					{
						//Debug.Log(String.Format("[LegacyProgram] Calculate spectrum range [{0}..{1})", offsetChunks + offset, offsetChunks + offset + count));
						CalculateSpectrumAsync(new SpectrumPartRequest(thread, offset, count));
					}
					else
					{
						EnqueueTask(CalculateSpectrumAsync, new SpectrumPartRequest(thread, offset, count));
					}
					offset += count;
				}
				lock (jobEvent)
					while (activeThreads > currentThreads)
						Monitor.Wait(jobEvent);
			}
			spectrumChunks = chunksCount;

			if (passCount > 1)
				Status = String.Format("Pass {0}/{1}. Saving spectrum cache... {2}/{3}", pass + 1, passCount, block + 1, blocks);
			else
				Status = String.Format("Saving spectrum cache... {0}/{1}", block + 1, blocks);
			//SaveCache();
		}
		protected void LoadSpectrumSync(int offsetChunks, int chunksCount)
		{
			spectrumChunks = 0;
			if (fft != IntPtr.Zero)
				NativeCore.Reset(fft);
			//if(LoadCache())
			//	return;

			if (passCount > 1)
				Status = String.Format("Pass {0}/{1}. Computing spectrum... {2}/{3}", pass + 1, passCount, block + 1, blocks);
			else
				Status = String.Format("Computing spectrum... {0}/{1}", block + 1, blocks);

			int offset = offsetChunks * FFTWindow / plan.multisamples;
			int samples = (chunksCount + plan.multisamples - 1) * FFTWindow / plan.multisamples;
			LoadAudio(offset, samples);

			if (fft != IntPtr.Zero)
			{
				NativeCore.CalculateSpectrum(fft, monoSound, (uint)monoSound.Length, spectrumMap, (uint)spectrumMap.Length, 0, (uint)chunksCount, 0);
			}
			else
			{
				CalculateSpectrum(0, chunksCount, 0);
			}
			spectrumChunks = chunksCount;

			if (passCount > 1)
				Status = String.Format("Pass {0}/{1}. Spectrum calculated {2}/{3}", pass + 1, passCount, block + 1, blocks);
			else
				Status = String.Format("Spectrum calculated {0}/{1}", block + 1, blocks);
			/*
			if (passCount > 1)
				Status = String.Format("Pass {0}/{1}. Saving spectrum cache... {2}/{3}", pass + 1, passCount, block + 1, blocks);
			else
				Status = String.Format("Saving spectrum cache... {0}/{1}", block + 1, blocks);
			
			SaveCache();
			*/
		}
		protected void ConvertKeyframesRotation(Keyframe[][] keyframes)
		{
			for (int i = 0; i < keyframes[0].Length; i++)
			{
				// yaw (Z), pitch (Y), roll (X)
				float x = keyframes[0][i].value;
				float y = keyframes[1][i].value;
				float z = keyframes[2][i].value;
				Quaternion q = Quaternion.Euler(x, y, z);
				keyframes[0][i].value = q.x;
				keyframes[1][i].value = q.y;
				keyframes[2][i].value = q.z;
				keyframes[3][i].value = q.w;
			}
		}
#pragma warning disable CS0618 // Тип или член устарел
		protected void CreateKeyframes(float[] values, LegacyTask task)
		{
			float timePerChunk = FFTWindow / frequency / plan.multisamples;
			Keyframe[][] keyframes = task.keyframes;
			Vector4 minValue = task.slot.minValue;
			Vector4 maxValue = task.slot.maxValue;
			int channels = task.Channels;
			int loops = task.slot.loops;
			Keyframe[] kX = keyframes[0];
			Keyframe[] kY = keyframes[1];
			Keyframe[] kZ = keyframes[2];
			Keyframe[] kW = keyframes[3];
			float halfWindowTime = 0.5f * FFTWindow / frequency;
			if (loops > 1)
			{
				if (task.slot.type == BitAnimator.PropertyType.Int)
				{
					for (int i = 0; i < values.Length; i++)
					{
						float v = values[i];
						kX[i].time = kY[i].time = kZ[i].time = kW[i].time = i * timePerChunk + halfWindowTime;
						kX[i].value = Mathf.RoundToInt(Mathf.LerpUnclamped(minValue.x, maxValue.x, v));
						kY[i].value = Mathf.RoundToInt(Mathf.LerpUnclamped(minValue.y, maxValue.y, v));
						kZ[i].value = Mathf.RoundToInt(Mathf.LerpUnclamped(minValue.z, maxValue.z, v));
						kW[i].value = Mathf.RoundToInt(Mathf.LerpUnclamped(minValue.w, maxValue.w, v));
						kX[i].tangentMode = kY[i].tangentMode = kZ[i].tangentMode = kW[i].tangentMode = 1;
					}
				}
				else
				{
					for (int i = 0; i < values.Length; i++)
					{
						float v = Mathf.Repeat(values[i] * loops, 1.0f);
						kX[i].time = kY[i].time = kZ[i].time = kW[i].time = i * timePerChunk + halfWindowTime;
						kX[i].value = Mathf.LerpUnclamped(minValue.x, maxValue.x, v);
						kY[i].value = Mathf.LerpUnclamped(minValue.y, maxValue.y, v);
						kZ[i].value = Mathf.LerpUnclamped(minValue.z, maxValue.z, v);
						kW[i].value = Mathf.LerpUnclamped(minValue.w, maxValue.w, v);
						kX[i].tangentMode = kY[i].tangentMode = kZ[i].tangentMode = kW[i].tangentMode = 1;
					}
				}
			}
			else if (task.slot.type == BitAnimator.PropertyType.Int)
			{
				for (int i = 0; i < values.Length; i++)
				{
					float v = values[i];
					kX[i].time = kY[i].time = kZ[i].time = kW[i].time = i * timePerChunk + halfWindowTime;
					kX[i].value = Mathf.RoundToInt(Mathf.LerpUnclamped(minValue.x, maxValue.x, v));
					kY[i].value = Mathf.RoundToInt(Mathf.LerpUnclamped(minValue.y, maxValue.y, v));
					kZ[i].value = Mathf.RoundToInt(Mathf.LerpUnclamped(minValue.z, maxValue.z, v));
					kW[i].value = Mathf.RoundToInt(Mathf.LerpUnclamped(minValue.w, maxValue.w, v));
					kX[i].tangentMode = kY[i].tangentMode = kZ[i].tangentMode = kW[i].tangentMode = 1;
				}
			}
			else
			{
				for (int i = 0; i < values.Length; i++)
				{
					float v = values[i];
					kX[i].time = kY[i].time = kZ[i].time = kW[i].time = i * timePerChunk + halfWindowTime;
					kX[i].value = Mathf.LerpUnclamped(minValue.x, maxValue.x, v);
					kY[i].value = Mathf.LerpUnclamped(minValue.y, maxValue.y, v);
					kZ[i].value = Mathf.LerpUnclamped(minValue.z, maxValue.z, v);
					kW[i].value = Mathf.LerpUnclamped(minValue.w, maxValue.w, v);
					kX[i].tangentMode = kY[i].tangentMode = kZ[i].tangentMode = kW[i].tangentMode = 1;
				}
			}
		}
		protected void RemapGradient(float[] values, Keyframe[][] keyframes, Gradient gradient, int channels, int loops)
		{
			float timePerChunk = FFTWindow / frequency / plan.multisamples;
			float halfWindowTime = 0.5f * FFTWindow / frequency;
			Keyframe[] kR = keyframes[0];
			Keyframe[] kG = keyframes[1];
			Keyframe[] kB = keyframes[2];
			Keyframe[] kA = keyframes[3];

			if (loops > 1)
			{
				for (int i = 0; i < values.Length; i++)
				{
					Color color = gradient.Evaluate(Mathf.Repeat(values[i] * loops, 1.0f));
					kR[i].time = kG[i].time = kB[i].time = kA[i].time = i * timePerChunk + halfWindowTime;
					kR[i].value = color.r; kG[i].value = color.g; kB[i].value = color.b; kA[i].value = color.a;
					kR[i].tangentMode = kG[i].tangentMode = kB[i].tangentMode = kA[i].tangentMode = 1;
				}
			}
			else
			{
				for (int i = 0; i < values.Length; i++)
				{
					Color color = gradient.Evaluate(values[i]);
					kR[i].time = kG[i].time = kB[i].time = kA[i].time = i * timePerChunk + halfWindowTime;
					kR[i].value = color.r; kG[i].value = color.g; kB[i].value = color.b; kA[i].value = color.a;
					kR[i].tangentMode = kG[i].tangentMode = kB[i].tangentMode = kA[i].tangentMode = 1;
				}
			}
		}
#pragma warning restore CS0618 // Тип или член устарел
		protected void MergeBand(object LegacyTask)
		{
			LegacyTask task = (LegacyTask)LegacyTask;
			task.legacyResolver.Resolve(spectrumMap, task.rawValues);
		}
		protected static void Multiply(float[] values, float factor)
		{
			for (int i = 0; i < values.Length; i++)
				values[i] *= factor;
		}
		protected static void Multiply(float[] values, float[] mask)
		{
			for (int i = 0; i < values.Length; i++)
				values[i] *= mask[i];
		}
		protected void EnergyCorrection(float[] values)
		{
			float a = 0.001f * frequency / RealWindow;
			float b = a * 0.5f;
			float x = 0.0f;
			int count = values.Length;
			for (int i = 0; i < count; i++, x+=1.0f)
				values[i] *= x * a + b;
		}
		internal static void AproximateKeyframes(int left, int right, Keyframe[] k, out float outTangent, out float inTangent)
		{
			//y ~ a*h10(x) + b*h11(x)
			// inTangent = a
			//outTangent = b
			float start = k[left].time;
			float end = k[right].time;
			float lValue = k[left].value;
			float rValue = k[right].value;
			float scale = 1.0f / (end - start);
			float t01 = 0;
			float t11 = 0;
			float t00 = 0;
			float yt0 = 0;
			float yt1 = 0;
			//если апроксимируем 1 ключ то решение будет неоднозначно (нулевой определитель матрицы)
			if (right - left > 2)
			{
				for (int i = left + 1; i < right; i++)
				{
					float x = k[i].time;
					float y = k[i].value;

					float t = (x - start) * scale;
					float t2 = t * t;
					float t3 = t2 * t;

					float h10 = t3 - 2.0f * t2 + t;
					float h01 = 3.0f * t2 - 2.0f * t3;
					float h00 = 1.0f - h01;
					float h11 = t3 - t2;

					y -= h00 * lValue + h01 * rValue;
					y *= scale;

					yt0 += y * h10;
					yt1 += y * h11;
					t00 += h10 * h10;
					t01 += h10 * h11;
					t11 += h11 * h11;
				}
			}
			else
			{
				//поэтому добавляем еще 2 ключа
				//и линейно интерполируем эти ключи left - k[0]' - k[0] - k[0]' - right
				float x = k[left + 1].time;
				float v = k[left + 1].value;

				float t = 0.5f * (x - start) * scale;
				float t2 = t * t;
				float t3 = t2 * t;

				float h10 = t3 - 2.0f * t2 + t;
				float h01 = 3.0f * t2 - 2.0f * t3;
				float h00 = 1.0f - h01;
				float h11 = t3 - t2;

				float y = 0.5f * (v + lValue) - (h00 * lValue + h01 * rValue);
				y *= scale;

				yt0 += y * h10;
				yt1 += y * h11;
				t00 += h10 * h10;
				t01 += h10 * h11;
				t11 += h11 * h11;

				t = (0.5f * (end + x) - start) * scale;
				t2 = t * t;
				t3 = t2 * t;

				h10 = t3 - 2.0f * t2 + t;
				h01 = 3.0f * t2 - 2.0f * t3;
				h00 = 1.0f - h01;
				h11 = t3 - t2;

				y = 0.5f * (v + rValue) - (h00 * lValue + h01 * rValue);
				y *= scale;

				yt0 += y * h10;
				yt1 += y * h11;
				t00 += h10 * h10;
				t01 += h10 * h11;
				t11 += h11 * h11;
			}
			//  |t01  t11|
			//  |t00  t01| 
			float det = t01 * t01 - t11 * t00;
			//  |yt1  t11|
			//  |yt0  t01|
			float det_a = yt1 * t01 - t11 * yt0;
			//  |t01  yt1|
			//  |t00  yt0|
			float det_b = t01 * yt0 - yt1 * t00;
			outTangent = det_a / det;
			inTangent = det_b / det;
			//Debug.LogFormat("inTangent = {0:F3}, outTangent = {1:F3}", inTangent, outTangent);
		}
		//  Unity uses Hermite spline for curves
		//  https://en.wikipedia.org/wiki/Cubic_Hermite_spline
		static float Evaluate(Keyframe left, Keyframe right, float time)
		{
			float scale = right.time - left.time;
			float t = (time - left.time) / scale;
			float t2 = t * t;
			float t3 = t2 * t;

			float h10 = t3 - 2.0f * t2 + t;
			float h01 = 3.0f * t2 - 2.0f * t3;
			float h00 = 1.0f - h01;
			float h11 = t3 - t2;
			return h00 * left.value
					+ h10 * scale * left.outTangent
					+ h01 * right.value
					+ h11 * scale * right.inTangent;
		}
#if UNITY_2018_1_OR_NEWER
		static float EvaluateNew(Keyframe left, Keyframe right, float time)
		{
			bool W0 = (left.weightedMode & WeightedMode.Out) != 0;
			bool W1 = (right.weightedMode & WeightedMode.In) != 0;
			float scale = right.time - left.time;
			if (W0 || W1)
			{
				float x = (time - left.time) / scale;
				float t = x;
				{
					float B = left.outWeight;
					float C = 1 - right.inWeight;
					float a = 3 * B - 3 * C + 1;
					float b = -6 * B + 3 * C;
					float c = 3 * B;
					float d = -x;

					for (int i = 0; i < 12; i++)
					{
						float t2 = t * t;
						float t3 = t2 * t;
						float fx = a * t3 + b * t2 + c * t + d;
						float dt = 3 * a * t2 + 2 * b * t + c;
						if (Mathf.Abs(dt) > 0.001f)
							t -= fx / dt;
					}
				}
				{
					float A = left.value;
					float D = right.value;
					float B = A + left.outTangent * left.outWeight * (right.time - left.time);
					float C = D - right.inTangent * right.inWeight * (right.time - left.time);
					float a = -A + 3 * B - 3 * C + D;
					float b = 3 * A - 6 * B + 3 * C;
					float c = -3 * A + 3 * B;
					float d = A;
					float t2 = t * t;
					float t3 = t2 * t;
					float y = a * t3 + b * t2 + c * t + d;
					return y;
				}
			}
			else
			{
				float t = (time - left.time) / scale;
				float t2 = t * t;
				float t3 = t2 * t;

				float h10 = t3 - 2.0f * t2 + t;
				float h01 = 3.0f * t2 - 2.0f * t3;
				float h00 = 1.0f - h01;
				float h11 = t3 - t2;
				return h00 * left.value
						+ h10 * scale * left.outTangent
						+ h01 * right.value
						+ h11 * scale * right.inTangent;
			}
		}
#else
		static float EvaluateNew(Keyframe left, Keyframe right, float time)
		{
			return Evaluate(left, right, time);
		}
#endif
		internal static Keyframe[] DecimateAnimationCurve(Keyframe[] keyframes, float maxError)
		{
			maxError *= maxError; //Converting to RMS
			int count = keyframes.Length;
			int[] table = new int[count];
			int saved = 1;
			int indexLeft = 0;
			int indexRight = 2;
			int below = 1;
			float savedOutTangent = 0;
			float savedInTangent = 0;
			// пропускаем ключи до тех пор пока ошибка меньше порога
			// сначала пропускаем 3 ключа (2 ключа можно точно аппроксимировать)
			while (indexRight < count)
			{
				float otg;
				float itg;
				AproximateKeyframes(indexLeft, indexRight, keyframes, out otg, out itg);
				Keyframe left = keyframes[indexLeft];
				Keyframe right = keyframes[indexRight];
				left.outTangent = otg;
				right.inTangent = itg;
				// расчитываем среднюю ошибку аппроксимации
				bool skipNext = true;
				for (int x = indexLeft + 1; x < indexRight; x++)
				{
					float aproximated = Evaluate(left, right, keyframes[x].time);
					float original = keyframes[x].value;
					float R = aproximated - original;
					if (R * R > maxError)
					{
						// ошибка больше порога: останавливаемся тут
						keyframes[indexLeft].outTangent = savedOutTangent;
						keyframes[below].inTangent = savedInTangent;
						table[saved++] = below;
						indexLeft = below;
						indexRight = indexLeft + 3;
						below = indexRight;
						skipNext = false;
						break;
					}
				}
				if (skipNext)
				{
					// ошибка меньше порога: пропускаем еще
					// величина шага удваевается каждый раз
					below = indexRight;
					indexRight += indexRight - indexLeft - 1;
					savedOutTangent = otg;
					savedInTangent = itg;
				}
			}
			table[saved++] = count - 1;
			return table.Take(saved).Select((idx) => keyframes[idx]).ToArray();
		}
		internal static Keyframe[] DecimateAnimationCurveV2(Keyframe[] keyframes, float maxError)
		{
			maxError *= maxError; //Converting to RMS
			int count = keyframes.Length;
			int[] table = new int[count];
			int saved = 1;
			int indexLeft = 0;
			int indexRight = 2;
			int below = -1;
			int above = -1;
			//пропускаем ключи до тех пор пока ошибка меньше порога
			//сначала пропускаем 3 ключа (2 ключа можно точно аппроксимировать)
			while (indexRight < count)
			{
				float otg;
				float itg;
				AproximateKeyframes(indexLeft, indexRight, keyframes, out otg, out itg);
				Keyframe left = keyframes[indexLeft];
				Keyframe right = keyframes[indexRight];
				left.outTangent = otg;
				right.inTangent = itg;
				//расчитываем среднюю ошибку аппроксимации
				bool thresholdReached = false;
				for (int x = indexLeft + 1; x < indexRight; x++)
				{
					float aproximated = Evaluate(left, right, keyframes[x].time);
					float original = keyframes[x].value;
					float R = aproximated - original;
					if (R * R > maxError)
					{
						// ошибка больше порога: останавливаемся тут
						above = indexRight;
						thresholdReached = true;
						break;
					}
				}
				if (!thresholdReached)
				{
					//ошибка меньше порога: пропускаем еще
					below = indexRight;
					if (above < 0)
					{
						indexRight += indexRight - indexLeft - 1;
						continue;
					}
				}

				//если была ошибка больше порога: пропускаем столько ключей пока ошибка меньше порога
				if (above - below > 1)
					indexRight = (below + above) / 2; // подразделяем область поиска
				else // останавливаем подразделение и записываем результат
				{
					AproximateKeyframes(indexLeft, below, keyframes, out otg, out itg);
					keyframes[indexLeft].outTangent = otg;
					keyframes[below].inTangent = itg;
					table[saved++] = below;
					indexLeft = below;
					indexRight = indexLeft + 3;
					below = indexLeft + 2;
					above = -1;
				}
			}
			table[saved++] = count - 1;
			return table.Take(saved).Select((idx) => keyframes[idx]).ToArray();
		}
		internal static Keyframe[] DecimateAnimationCurveV1(Keyframe[] keyframes, float maxError)
		{
			maxError *= maxError; //Converting to RMS
			int size = keyframes.Length;
			int[] k = Enumerable.Range(0, size).ToArray();
			//StringBuilder str = new StringBuilder();
			//str.AppendLine("[BitAnimator] Keyframes count before decimation = " + size);
			float[] rms = new float[size];
			bool[] decimate = new bool[size];
			rms[0] = rms[size - 1] = maxError;
			int iterations = 0;
			int j, r;
			//этап 1 - поиск опорных ключевых кадров
			for (; iterations < 64; iterations++)
			{
				// j - left key index
				// r - right key index
				//пропускаем 1 ключ и вычисляем ошибку (в точке где был ключ) между оригиналом и апроксимацией
				for (int i = 1; i < size - 1; i++)
				{
					j = k[i - 1];
					r = k[i + 1];
					float otg;
					float itg;
					AproximateKeyframes(j, r, keyframes, out otg, out itg);
					Keyframe left = keyframes[j];
					Keyframe right = keyframes[r];
					left.outTangent = otg;
					right.inTangent = itg;
					float error = 0;
					for (int x = j + 1; x < r; x++)
					{
						float aproximated = Evaluate(left, right, keyframes[x].time);
						float original = keyframes[x].value;
						float R = aproximated - original;
						error += R * R;
					}
					rms[i] = error / (r - j - 1);
				}
				//(Аггрегатная функция) маркируем на удаление ключ если его удаление имеет меньше влияния и ниже порога
				for (int i = 1; i < size - 1; i++)
				{
					if (rms[i] <= maxError && rms[i] <= rms[i - 1] && rms[i] <= rms[i + 1])
						decimate[i] = true;
					else
						decimate[i] = false;
				}
				//перемещаем оставшиеся ключи к началу массива
				j = 1;
				double currentRMS = 0;
				for (int i = 1; i < size; i++)
				{
					if (!decimate[i])
					{
						k[j] = k[i];
						j++;
					}
					else
						currentRMS += rms[i];
					rms[i] = 0;
				}
				//str.AppendLine(string.Format("Iteration: {0}, Keyframes = {1}, RMSD = {2:F6}", iterations, j, size != j ? Math.Sqrt(currentRMS / (size - j)) : 0));
				//при малом сжатии прерываем оптимизацию
				if (j < 0.99f * size)
					size = j;
				else
				{
					size = j;
					break;
				}
			}
			//апроксимируем и сохраняем результат
			//и вычисляем среднеквадратическую ошибку кривой между оригиналом и апроксимацией
			double RMS = 0;
			for (int i = 1; i < size; i++)
			{
				j = k[i - 1];
				r = k[i];
				if (r - j <= 1)
					continue;

				float otg;
				float itg;
				AproximateKeyframes(j, r, keyframes, out otg, out itg);
				keyframes[j].outTangent = otg;
				keyframes[r].inTangent = itg;
				Keyframe left = keyframes[j];
				Keyframe right = keyframes[r];
				for (int x = j + 1; x < r; x++)
				{
					double aproximated = Evaluate(left, right, keyframes[x].time);
					double original = keyframes[x].value;
					double R = aproximated - original;
					RMS += R * R;
				}
			}
			Keyframe[] result = k.Take(size).Select((idx) => keyframes[idx]).ToArray();
			log.Add(new LogEvent(String.Format("[v1] Keyframes after {0} iterations = {1}, RMSE = {2:F6}", iterations, size, Math.Sqrt(RMS / size))));
			return result;
		}
		protected void InteruptWork()
		{
			if (fft != IntPtr.Zero)
			{
				NativeCore.Stop(fft);
			}
			lock (threads)
			{
				foreach (Thread thread in threads)
				{
					if (thread.ThreadState != ThreadState.Stopped)
					{
						thread.Interrupt();
					}
				}
				foreach (Thread thread in threads)
				{
					thread.Join();
				}
				threads.Clear();
			}
		}
		public override void Dispose()
		{
			InteruptWork();

			if (cacheFile != null)
			{
				cacheFile.Dispose();
				cacheFile = null;
			}
			lock (jobGuard)
			{
				if (fft != IntPtr.Zero)
				{
					NativeCore.Dispose(fft);
					NativeCore.UnLoad();
					fft = IntPtr.Zero;
				}
			}
			if (monoSound != null) monoSoundPin.Free();
			if (spectrumMap != null) spectrumMapPin.Free();
		}
		static byte[] GetBytes(float[] values, int count)
		{
			byte[] result = new byte[count * sizeof(float)];
			Buffer.BlockCopy(values, 0, result, 0, result.Length);
			return result;
		}
		protected void SaveCache()
		{
			if (cacheFile == null)
				cacheFile = cache.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite);
			cacheFile.Seek(loadOffset * RealWindow * sizeof(float), SeekOrigin.Begin);
			cacheFile.Write(GetBytes(spectrumMap, loadLength * RealWindow), 0, loadLength * RealWindow * sizeof(float));
		}
		protected bool LoadCache()
		{
			int end = loadOffset + loadLength;

			if (cache.Exists)
			{
				cachedChunks = (int)cache.Length / RealWindow / sizeof(float);
				if (cachedChunks < end)
					return false;
			}
			else
				return false;

			if (cacheFile == null)
				cacheFile = cache.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite);

			Status = "Loading spectrum from cache...";
			cacheFile.Seek(loadOffset * RealWindow * sizeof(float), SeekOrigin.Begin);
			int bytes = loadLength * RealWindow * sizeof(float);
			byte[] data = new byte[bytes];
			cacheFile.Read(data, 0, bytes);
			Buffer.BlockCopy(data, 0, spectrumMap, 0, bytes);
			spectrumChunks = loadLength;
			return true;
		}

		//Algorithmic Beat Mapping in Unity: Preprocessed Audio Analysis
		//https://medium.com/giant-scam/algorithmic-beat-mapping-in-unity-preprocessed-audio-analysis-d41c339c135a
		protected void CalculateSpectrum(int offset, int count, int thread)
		{
			float[] chunk = new float[FFTWindow];
			for (int i = 0; i < count; i++)
			{
				// Grab the current chunk of audio sample data
				Array.Copy(monoSound, (i + offset) * FFTWindow / plan.multisamples, chunk, 0, FFTWindow);

				// Apply our chosen FFT Window
				Multiply(chunk, window);

				// Perform the FFT and convert output (complex numbers) to Magnitude
				Complex[] fftSpectrum = fft2[thread].Execute(chunk);
				float[] scaledFFTSpectrum = DSP.ConvertComplex.ToMagnitude(fftSpectrum);
				//Multiply(scaledFFTSpectrum, windowScale);

				//Spectrum energy correction
				if ((plan.mode & Mode.EnergyCorrection) != 0)
					EnergyCorrection(scaledFFTSpectrum);

				if ((plan.mode & Mode.CalculateFons) != 0)
					CalculateFons(scaledFFTSpectrum);

				Array.Copy(scaledFFTSpectrum, 0, spectrumMap, (i + offset) * RealWindow, RealWindow);
				Interlocked.Increment(ref spectrumChunks);
			}
		}
		void CalculateSpectrumAsync(object part)
		{
			SpectrumPartRequest i = (SpectrumPartRequest)part;
			if (i.offset < 0)
				throw new ArgumentOutOfRangeException("[LegacyProgram] offset must be non negative");
			if (i.count < 0)
				throw new ArgumentOutOfRangeException("[LegacyProgram] count must be non negative");
			if (i.thread < 0 || i.thread >= processorCount)
				throw new ArgumentOutOfRangeException("[LegacyProgram] thread index must be in range [0.." + (processorCount - 1) + "]");
			if ((i.offset + i.count) * FFTWindow / plan.multisamples > monoSound.Length)
				throw new ArgumentOutOfRangeException("[LegacyProgram] audiosamples < required!");
			if ((i.offset + i.count) * RealWindow > spectrumMap.Length)
				throw new ArgumentOutOfRangeException("[LegacyProgram] spectrum values < required!");

			if (fft != IntPtr.Zero)
				NativeCore.CalculateSpectrum(fft, monoSound, (uint)monoSound.Length, spectrumMap, (uint)spectrumMap.Length, (uint)i.offset, (uint)i.count, (uint)i.thread);
			else
				CalculateSpectrum(i.offset, i.count, i.thread);
		}
		protected void RunTask(object LegacyTask)
		{
			LegacyTask task = (LegacyTask)LegacyTask;
			ApplyModificators(task);
			CreateKeyframes(task);
		}
		protected void ApplyModificators(LegacyTask task)
		{
			float[] values = task.values;
			float[] buffer = new float[values.Length];
			Buffer.BlockCopy(task.rawValues, 0, values, 0, values.Length * sizeof(float));
			foreach (ILegacyModificator mod in task.legacyMods.Where(mod => mod.Queue == Modificators.ExecutionQueue.Peaks))
			{
				if (mod.Enabled && mod.Queue == Modificators.ExecutionQueue.Peaks)
				{
					mod.Apply(values, buffer);
					if (mod.UseTempBuffer)
						Swap(ref values, ref buffer);
				}
			}
			task.values = values;
		}
		protected void CreateKeyframes(LegacyTask task)
		{
			if (task.slot.type == BitAnimator.PropertyType.Color)
				RemapGradient(task.values, task.keyframes, task.slot.colors, task.Channels, task.slot.loops);
			else
				CreateKeyframes(task.values, task);

			//перевод углов Эйлера в кватернионы
			if (task.slot.type == BitAnimator.PropertyType.Quaternion)
				ConvertKeyframesRotation(task.keyframes);

			if (plan.quality < 1.0f)
			{
				float rmsQuality = Mathf.Pow(10.0f, -6.0f * plan.quality * plan.quality - 1.0f); //quality to RMS   [0..1] => [1e-1 .. 1e-7]
				for (int c = 0; c < task.Channels; c++)
					task.keyframes[c] = DecimateAnimationCurve(task.GetKeyframes(c), rmsQuality * task.AmplitudeRange(c));
			}
		}
		//Acoustics — Normal equal-loudness-level contours
		//http://libnorm.ru/Files2/1/4293820/4293820821.pdf

		//  Hz, Alpha_f, Lu, Tf
		private static float[] isofons = {
			20.0f, 0.532f, -31.6f, 78.5f,
			25.0f, 0.506f, -27.2f, 68.7f,
			31.5f, 0.480f, -23.0f, 59.5f,
			40.0f, 0.455f, -19.1f, 51.1f,
			50.0f, 0.432f, -15.9f, 44.0f,
			63.0f, 0.409f, -13.0f, 37.5f,
			80.0f, 0.387f, -10.3f, 31.5f,
			100.0f, 0.367f, -8.1f, 26.5f,
			125.0f, 0.349f, -6.2f, 22.1f,
			160.0f, 0.330f, -4.5f, 17.9f,
			200.0f, 0.315f, -3.1f, 14.4f,
			250.0f, 0.301f, -2.0f, 11.4f,
			315.0f, 0.288f, -1.1f, 8.6f,
			400.0f, 0.276f, -0.4f, 6.2f,
			500.0f, 0.267f, 0.0f, 4.4f,
			630.0f, 0.259f, 0.3f, 3.0f,
			800.0f, 0.253f, 0.5f, 2.2f,
			1000.0f, 0.250f, 0.0f, 2.4f,
			1250.0f, 0.246f, -2.7f, 3.5f,
			1600.0f, 0.244f, -4.1f, 1.7f,
			2000.0f, 0.243f, -1.0f, -1.3f,
			2500.0f, 0.243f, 1.7f, -4.2f,
			3150.0f, 0.243f, 2.5f, -6.0f,
			4000.0f, 0.242f, 1.2f, -5.4f,
			5000.0f, 0.242f, -2.1f, -1.5f,
			6300.0f, 0.245f, -7.1f, 6.0f,
			8000.0f, 0.254f, -11.2f, 12.6f,
			10000.0f, 0.271f, -10.7f, 13.9f,
			12500.0f, 0.301f, -3.1f, 12.3f
		};

		private static float[] Hz_data = {
			20.0f,
			25.0f,
			31.5f,
			40.0f,
			50.0f,
			63.0f,
			80.0f,
			100.0f,
			125.0f,
			160.0f,
			200.0f,
			250.0f,
			315.0f,
			400.0f,
			500.0f,
			630.0f,
			800.0f,
			1000.0f,
			1250.0f,
			1600.0f,
			2000.0f,
			2500.0f,
			3150.0f,
			4000.0f,
			5000.0f,
			6300.0f,
			8000.0f,
			10000.0f,
			12500.0f
		};
		float getFons(float hz, float Lp)
		{
			Lp = Mathf.Max(0.0f, Mathf.Log10(Lp) * 10.0f + 94.0f);
			int idx2 = 0;
			while (idx2 < Hz_data.Length && Hz_data[idx2] < hz)
				idx2++;

			int idx = Mathf.Max(idx2 - 1, 0);
			idx2 = Mathf.Min(idx2, Hz_data.Length - 1);
			float w = idx != idx2 ? (hz - Hz_data[idx]) / (Hz_data[idx2] - Hz_data[idx]) : 0;
			idx *= 4;
			idx2 *= 4;
			float Alpha_f = Mathf.Lerp(isofons[idx + 1], isofons[idx2 + 1], w);
			float Lu = Mathf.Lerp(isofons[idx + 2], isofons[idx2 + 2], w);
			float Tf = Mathf.Lerp(isofons[idx + 3], isofons[idx2 + 3], w);
			//Convert dB to Fons
			float Bf = Mathf.Pow(0.4f * Mathf.Pow(10.0f, (Lp + Lu) * 0.1f - 9), Alpha_f) - Mathf.Pow(0.4f * Mathf.Pow(10.0f, (Tf + Lu) * 0.1f - 9), Alpha_f) + 0.005135f;

			return Bf * Bf * Bf * Bf; //optimized calculatation (convert dB to raw values)
		}
		public void CalculateFons(float[] spectrum)
		{
			float hzPerBin = frequency / FFTWindow;
			for (uint i = 0; i < spectrum.Length; i++)
				spectrum[i] = getFons(i * hzPerBin, spectrum[i]);
		}
		public void CalculateFons_alpha(float[] spectrum)
		{
			int idx = 0;
			int idx2 = 0;
			int maxIndex = 28;
			float w = 0;
			float hz = 0;
			float hz1 = 0;
			float hz2 = Hz_data[0];
			float prevValue = 0;
			float hzPerBin = frequency / FFTWindow;
			for (uint i = 0; i < spectrum.Length; i++, hz += hzPerBin)
			{
				float Lp = spectrum[i];
				Lp = Math.Max(0.0f, Mathf.Log10(Lp) * 10.0f + 94.0f);

				if (idx < maxIndex)
				{
					if (hz2 < hz)
					{
						do
						{
							hz1 = hz2;
							idx = idx2;
							idx2 = Math.Min(idx2 + 1, maxIndex);
							hz2 = Hz_data[idx2];
						}
						while (hz2 < hz && idx < maxIndex);
						w = idx != idx2 ? (hz - hz1) / (hz2 - hz1) : 0;
					}
					else
					{
						w = (hz - hz1) / (hz2 - hz1);
					}
				}
				int i_1 = idx * 4;
				int i_2 = idx2 * 4;
				float Alpha_f = isofons[i_1 + 1] * w + isofons[i_2 + 1] * (1.0f - w);
				float Lu = isofons[i_1 + 2] * w + isofons[i_2 + 2] * (1.0f - w);
				float Tf = isofons[i_1 + 3] * w + isofons[i_2 + 3] * (1.0f - w);
				//Convert dB to Fons
				float L = Mathf.Pow(10.0f, (Lp + Lu) * 0.1f - 9.0f);
				float T = Mathf.Pow(10.0f, (Tf + Lu) * 0.1f - 9.0f);
				float Bf = Mathf.Pow(0.4f * L, Alpha_f) - Mathf.Pow(0.4f * T, Alpha_f) + 0.005135f;
				//float Ln = 40.0f*Mathf.Log10 (Bf) + 94.0f;
				//convert Fons to dB
				//float Af = 0.00447f * (Mathf.Pow (10.0f, 0.025f * Ln) - 1.15f) + Mathf.Pow (0.4f * Mathf.Pow (10f, 0.1f * (Tf + Lu) - 9.0f), Alpha_f);
				//Af = Mathf.Max (Af, 0);
				//Lp = 10.0f / Alpha_f * Mathf.Log10 (Af) - Lu + 94.0f;

				//return Mathf.Pow (10.0f, (Ln - 94.0f)*0.1f);
				if (0.0f < Bf && Bf < 1.0f)
					spectrum[i] = prevValue = Bf * Bf * Bf * Bf; //optimized calculatation
				else
					spectrum[i] = prevValue;
			}
		}
		public float[] LinearToLogFreq(float[] x)
		{
			float[] result = new float[x.Length];
			Array.Clear(result, 0, result.Length);

			//map func = 1 - log2(a*(1-x) + 1)/log2(a + 1)
			float a = result.Length - 1;
			float k = Mathf.Log(a + 1, 2.0f);
			for (int i = 0; i < result.Length - 1; i++) //foreach result
			{
				float fIndex = 1 - Mathf.Log(a - i + 1, 2.0f) / k;
				float fIndex2 = 1 - Mathf.Log(a - i, 2.0f) / k;
				fIndex *= a;
				fIndex2 *= a;
				int i1 = (int)Mathf.Floor(fIndex);
				int i2 = (int)Mathf.Floor(fIndex2);
				float w = fIndex - i1;
				int j = i1;
				int n = 0;
				do
				{
					result[i] += Mathf.Lerp(x[j], x[j + 1 >= result.Length ? j : j + 1], w);
					n++;
					j++;
				} while (j < i2);
				result[i] /= n;
			}
			return result;
		}
		internal static void Swap<T>(ref T lhs, ref T rhs)
		{
			T temp = lhs;
			lhs = rhs;
			rhs = temp;
		}
		internal void Max(float[] input, AnimationCurve animationCurve)
		{
			float timePerChunk = FFTWindow / frequency / plan.multisamples;
			for (int i = 0; i < input.Length; i++) { float value = animationCurve.Evaluate(i * timePerChunk); if (value > input[i]) input[i] = value; }
		}
		internal void SmoothSpectrum(float[] input, float[] output, float smoothness)
		{
			if (Mathf.Approximately(smoothness, 0.0f))
				return;
			float chunksPerSecond = frequency * plan.multisamples / FFTWindow;
			int N = Mathf.CeilToInt(smoothness * chunksPerSecond);
			int halfN = N / 2;
			float radius = N / 2.0f;
			float sharpness = radius / 2.0f;
			float len_correction = 2.0f * sharpness * sharpness;
			float correction = Mathf.Sqrt(Mathf.PI * len_correction);
			float[] kernel = new float[N];
			for (int i = 0; i < kernel.Length; i++)
			{
				float x = i + 0.5f - radius;
				float len = x * x;
				kernel[i] = Mathf.Exp(-len / len_correction);
			}
			// Семплируем начало с проверкой на выход за границы массива
			for (int idx = 0; idx < Math.Min(halfN, input.Length); idx++)
			{
				int i = idx - halfN;
				float result = 0;
				for (int j = 0; j < N; j++) result += input[Math.Max(0, i + j)] * kernel[j];
				output[idx] = result / correction;
			}
			// Середину можно семплировать без лишних проверок
			for (int idx = halfN; idx < input.Length - halfN; idx++)
			{
				int i = idx - halfN;
				float result = 0;
				for (int j = 0; j < N; j++) result += input[i + j] * kernel[j];
				output[idx] = result / correction;
			}
			// Семплируем конец с проверкой на выход за границы массива
			for (int idx = Math.Max(0, input.Length - halfN); idx < input.Length; idx++)
			{
				int i = idx - halfN;
				float result = 0;
				for (int j = 0; j < N; j++) result += input[Math.Min(i + j, input.Length - 1)] * kernel[j];
				output[idx] = result / correction;
			}
		}
		double RMSD(float[] src, float[] dst)
		{
			double dist = 0;
			for (int i = 0; i < src.Length; i++)
			{
				double v = src[i] - dst[i];
				dist += v * v;
			}
			return Math.Sqrt(dist / src.Length);
		}
		internal static void Normalize(float[] values)
		{
			float max = 0;
			for (int i = 0; i < values.Length; i++) if (values[i] > max) max = values[i];
			if (max > 0)
				for (int i = 0; i < values.Length; i++) values[i] /= max;
		}
		public static class NativeCore
		{
			[Flags]
			public enum Instructions : UInt32
			{
				None = 0,
				SSE = 1 << 0,
				SSE2 = 1 << 1,
				SSE3 = 1 << 2,
				SSSE3 = 1 << 3,
				SSE42 = 1 << 4,
				AVX = 1 << 5,
				AVX2 = 1 << 6,
				AVX512F = 1 << 7,
				AVX512ER = 1 << 8
			};
			[DllImport("kernel32.dll", SetLastError = true)]
			static extern IntPtr LoadLibrary(string lib);

			[DllImport("kernel32.dll", SetLastError = true)]
			static extern void FreeLibrary(IntPtr module);

			[DllImport("kernel32.dll", SetLastError = true)]
			static extern IntPtr GetProcAddress(IntPtr module, string proc);

			static IntPtr module = IntPtr.Zero;
			static int refCount = 0;
			public static bool IsLoaded { get { return module != IntPtr.Zero; } }
			public static bool Load()
			{
				++refCount;
				if (IsLoaded)
					return true;

				module = LoadLibrary("Assets\\BitAnimator\\BitAnimatorCore.dll");
				if (module == IntPtr.Zero)
				{
					Debug.LogError("Could not load BitAnimator.NativeCore: " + Marshal.GetLastWin32Error());
					return false;
				}
				bool success = true;
				success &= BindMethod("GetCPUInstructionSet", out GetCPUInstructionSet);
				success &= BindMethod("CreateFFT", out CreateFFT);
				success &= BindMethod("Dispose", out Dispose);
				success &= BindMethod("SetFrequency", out SetFrequency);
				success &= BindMethod("CalculateSpectrum", out CalculateSpectrum);
				success &= BindMethod("ResizeSpectrum", out ResizeSpectrum);
				success &= BindMethod("Stop", out Stop);
				success &= BindMethod("Reset", out Reset);
				success &= BindMethod("GetProgress", out GetProgress);
				return success;
			}
			public static void UnLoad()
			{
				--refCount;
				if (IsLoaded && refCount == 0)
				{
					FreeLibrary(module);
					module = IntPtr.Zero;
				}
			}
			static bool BindMethod<T>(string name, out T func) where T : Delegate
			{
				IntPtr method = GetProcAddress(module, name);
				if (method == IntPtr.Zero)
				{
					Debug.LogError("Could not load BitAnimator.NativeCore method \"" + name + "\": " + Marshal.GetLastWin32Error());
					func = null;
					return false;
				}
				func = (T)Marshal.GetDelegateForFunctionPointer(method, typeof(T));
				return true;
			}

			[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
			public delegate Instructions DGetCPUInstructionSet();
			public static DGetCPUInstructionSet GetCPUInstructionSet;

			[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
			public delegate IntPtr DCreateFFT([In] Plan plan, [MarshalAs(UnmanagedType.LPArray)] float[] window, UInt32 processorCount, Instructions instructions);
			public static DCreateFFT CreateFFT;

			[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
			public delegate void DDispose([In] IntPtr fft);
			public static DDispose Dispose;

			[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
			public delegate void DSetFrequency([In] IntPtr fft, UInt32 frequency);
			public static DSetFrequency SetFrequency;

			[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
			public delegate void DCalculateSpectrum([In] IntPtr fft,
				[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] float[] sound, UInt32 samples,
				[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] float[] output, UInt32 outputSize, UInt32 offset, UInt32 count, UInt32 thread);
			public static DCalculateSpectrum CalculateSpectrum;

			[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
			public delegate void DResizeSpectrum([In] IntPtr fft,
				[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] float[] spectrum, UInt32 samples, UInt32 newWidth);
			public static DResizeSpectrum ResizeSpectrum;

			[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
			public delegate void DStop([In] IntPtr fft);
			public static DStop Stop;

			[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
			public delegate void DReset([In] IntPtr fft);
			public static DReset Reset;

			[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
			public delegate UInt32 DGetProgress([In] IntPtr fft);
			public static DGetProgress GetProgress;
		}
	}
}
#pragma warning restore 420