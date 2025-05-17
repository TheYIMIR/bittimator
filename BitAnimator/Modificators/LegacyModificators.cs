
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
using AudioVisualization;
using AudioVisualization.Modificators;
using UnityEngine;

namespace AudioVisualization
{
	public partial class LegacyProgram : Engine
	{
		public interface ILegacySpectrumResolver : ISpectrumResolver, IDisposable
		{
			void SetEngine(LegacyProgram engine);
			void Resolve(float[] input, float[] output);
		}
		public interface ILegacyModificator : IModificator, IDisposable
		{
			bool MultipassRequired { get; }
			void SetEngine(LegacyProgram engine);
			void Apply(float[] input, float[] output);
		}
		public class LegacyTask : Task
		{
			public static Factory<SpectrumResolver, ILegacySpectrumResolver> resolverFactory = new Factory<SpectrumResolver, ILegacySpectrumResolver>();
			public static Factory<Modificator, ILegacyModificator> modFactory = new Factory<Modificator, ILegacyModificator>();
			internal ILegacySpectrumResolver legacyResolver;
			internal ILegacyModificator[] legacyMods;
			internal LegacyProgram engine;
			public float[] values;
			public float[] rawValues;
			public Keyframe[][] keyframes;
			public bool isPeaksLoaded;
			static LegacyTask()
			{
				resolverFactory.Register(resolver => new LegacyDefaultSpectrumResolver(resolver), typeof(DefaultSpectrumResolver));
				resolverFactory.Register(resolver => new LegacyMultiBandResolver(resolver), typeof(MultiBandResolver));

				modFactory.Register(mod => new LegacyMainTone(mod), typeof(MainTone));
				modFactory.Register(mod => new LegacyShift(mod), typeof(Shift));
				modFactory.Register(mod => new LegacyTimeLimit(mod), typeof(TimeLimit));
				modFactory.Register(mod => new LegacyVolumeOverTime(mod), typeof(VolumeOverTime));
				modFactory.Register(mod => new LegacyDamping(mod), typeof(Damping));
				modFactory.Register(mod => new LegacyAmplitudeSmooth(mod), typeof(AmplitudeSmooth));
				modFactory.Register(mod => new LegacyAccumulate(mod), typeof(Accumulate));
				modFactory.Register(mod => new LegacyNormalize(mod), typeof(Normalize));
				modFactory.Register(mod => new LegacyRemap(mod), typeof(Remap));
				modFactory.Register(mod => new LegacyCustomPeaks(mod), typeof(CustomPeaks));
				modFactory.Register(mod => new LegacyBeatTrigger(mod), typeof(BeatTrigger));
				modFactory.Register(mod => new LegacyRandomizer(mod), typeof(Randomizer));
				modFactory.Register(mod => new LegacyBeatFinder(mod), typeof(BeatFinder));
			}
			public LegacyTask(LegacyProgram engine, BitAnimator.RecordSlot slot, int valuesCount)
			{
				this.engine = engine;
				this.slot = slot;

				legacyResolver = resolverFactory.Convert(slot.resolver);
				legacyResolver.SetEngine(engine);

				legacyMods = slot.modificators.Select(m => modFactory.Convert(m)).Where(m => m != null).ToArray();
				foreach (ILegacyModificator mod in legacyMods)
					mod.SetEngine(engine);

				values = new float[valuesCount];
				rawValues = new float[valuesCount];

				//Резервируем буффер для ключей XYZW|RGBA
				keyframes = new Keyframe[Channels][];
				for(int i = 0; i < Channels; i++)
					keyframes[i] = new Keyframe[valuesCount];
			}
			override public Keyframe[] GetKeyframes(int channel)
			{
				return keyframes[channel];
			}
			override public void Dispose()
			{
				if (legacyResolver != null)
					legacyResolver.Dispose();

				if (legacyMods != null)
					foreach (ILegacyModificator mod in legacyMods)
						mod.Dispose();

				legacyResolver = null;
				legacyMods = null;
				return;
			}
		}
		internal abstract class LegacySpectrumResolver<T> : ILegacySpectrumResolver where T : SpectrumResolver
		{
			public T resolver;
			public LegacyProgram engine;
			public LegacySpectrumResolver(SpectrumResolver resolver)
			{
				this.resolver = (T)resolver;
			}
			public virtual bool UseTempBuffer { get { return false; } }
			public virtual bool MultipassRequired { get { return false; } }
			public virtual void SetEngine(LegacyProgram engine) 
			{ 
				this.engine = engine; 
			}
			public virtual void Resolve(float[] input, float[] output)
			{
				throw new NotImplementedException();
			}
			public virtual void Dispose() { }
		}
		internal abstract class LegacyModificator<T> : ILegacyModificator where T : Modificator
		{
			public T mod;
			public LegacyProgram engine;
			public LegacyModificator(Modificator mod)
			{
				this.mod = (T)mod;
			}
			public bool Enabled { get { return mod.enabled; } set { mod.enabled = value; } }
			public virtual bool UseTempBuffer { get { return false; } }
			public virtual bool UseMask { get { return false; } }
			public virtual bool MultipassRequired { get { return false; } }
			public virtual ExecutionQueue Queue { get { return ExecutionQueue.Peaks; } }
			public virtual void SetEngine(LegacyProgram engine)
			{
				this.engine = engine;
			}
			public virtual void Apply(float[] input, float[] output)
			{
				throw new NotImplementedException();
			}
			public virtual void Dispose() { }
		}
		internal class LegacyDefaultSpectrumResolver : LegacySpectrumResolver<DefaultSpectrumResolver>
		{
			internal LegacyDefaultSpectrumResolver(SpectrumResolver resolver) : base(resolver) {}
			public override void Resolve(float[] input, float[] output)
			{
				if (engine.firstPass)
				{
					float hzPerBin = engine.frequency / engine.FFTWindow;
					int realWindow = engine.RealWindow;
					int loadOffset = engine.loadOffset;
					int chunks = engine.spectrumChunks;
					int start = Math.Max(0, Mathf.FloorToInt(resolver.startFreq / hzPerBin));
					int end = Math.Min(realWindow, Mathf.CeilToInt(resolver.endFreq / hzPerBin));
					float scale = 1.0f / Math.Max(1, end - start);
					for (int i = 0, s0 = start, s1 = end; i < chunks; i++, s0 += realWindow, s1 += realWindow)
					{
						float acc = 0f;
						for (int p = s0; p < s1; p++)
							acc += input[p];
						output[i + loadOffset] = acc * scale;
					}
				}
			}
		}
		internal class LegacyMultiBandResolver : LegacySpectrumResolver<MultiBandResolver>
		{
			internal float[] window;
			public LegacyMultiBandResolver(SpectrumResolver resolver) : base(resolver) { }
			static float Erf(float x)
			{
				// constants
				const float a1 = 0.254829592f;
				const float a2 = -0.284496736f;
				const float a3 = 1.421413741f;
				const float a4 = -1.453152027f;
				const float a5 = 1.061405429f;
				const float p = 0.3275911f;

				// Save the sign of x
				float sign = 1.0f;
				if (x < 0.0f)
				{
					sign = -1.0f;
					x = -x;
				}

				// A&S formula 7.1.26
				float t = 1.0f / (1.0f + p * x);
				float y = 1.0f - ((((a5 * t + a4) * t + a3) * t + a2) * t + a1) * t * Mathf.Exp(-x * x);

				return sign * y;
			}
			static float Erf2(float x)
			{
				const float a = 0.140012288687f;
				const float b = 1.273239544740f;
				float sign = x < 0.0f ? -1.0f : 1.0f;
				float x2 = x * x;
				float ax2 = a * x2;
				float y = Mathf.Exp(-x2 * (b + ax2) / (1.0f + ax2));
				return sign * Mathf.Sqrt(1.0f - y);
			}
			static float Lf(float x)
			{
				return x < -1f ? -1f : (x > 1f ? 1f : x);
			}
			public override void SetEngine(LegacyProgram engine)
			{
				base.SetEngine(engine);
				MultiBandResolver.Band[] bands = resolver.bands;
				window = new float[engine.RealWindow];
				float hzPerBin = engine.frequency / engine.FFTWindow;
				for (int i = 0; i < bands.Length; i++)
				{
					float freq = bands[i].frequency;
					float width = bands[i].width;
					float value = bands[i].value;
					float s = hzPerBin;
					float o = width;
					if (resolver.smoothBand)
					{
						float m = value * o * Mathf.Sqrt(Mathf.PI) / 2.0f / hzPerBin;
						float invO = 1.0f / o;
						float prevErf = m * Erf(-freq * invO);
						for (int j = 0; j < engine.RealWindow; j++)
						{
							float erf = m * Erf((s - freq) * invO);
							window[j] += erf - prevErf;
							prevErf = erf;
							s += hzPerBin;
						}
					}
					else
					{
						float m = value * o / hzPerBin;
						float invO = 1.0f / o;
						float prevErf = m * Lf(-freq * invO);
						for (int j = 0; j < engine.RealWindow; j++)
						{
							float erf = m * Lf((s - freq) * invO);
							window[j] += erf - prevErf;
							prevErf = erf;
							s += hzPerBin;
						}
					}
				}
			}
			public override void Resolve(float[] input, float[] output)
			{
				if (engine.firstPass)
				{
					int realWindow = engine.RealWindow;
					int loadOffset = engine.loadOffset;
					int chunks = engine.spectrumChunks;
					for (int i = 0, offset = 0; i < chunks; i++, offset += realWindow)
					{
						float result = 0;
						for (int j = 0; j < realWindow; j++)
						{
							result += input[j + offset] * window[j];
						}
						output[i + loadOffset] = result;
					}
				}
				/*
					windowSamples = new float[samples.Length][];
					for(int i = 0; i < samples.Length; i++)
						windowSamples[i] = new float[engine.RealWindow];

					//float weightSum = samples.Sum(s => Math.Abs(s.weight));
					for(int s = 0; s < samples.Length; s++)
					{
						//samples[s].weight /= weightSum;
						int chunkIndex = Mathf.FloorToInt((samples[s].time - halfWindowTime) * chunksPerSecond) - engine.loadOffset;
						if(0 <= chunkIndex && chunkIndex < engine.spectrumChunks)
						{ 
							float[] window = windowSamples[s];
							int j = chunkIndex * engine.RealWindow;
							for(int b = 0; b < engine.RealWindow; b++)
								window[b] = input[j + b];
						}
					}
				}
				if(!engine.firstPass || engine.spectrumChunks == engine.chunks)
				{
					int windowSize = engine.RealWindow;
					for(int i = 0; i < engine.spectrumChunks; i++)
					{
						int y = i * windowSize;
						float average = 0;
						for(int x = 0; x < windowSize; x++)
							average += input[y + x];
						average /= windowSize;
						float SS_tot = 0;
						for(int x = 0; x < windowSize; x++)
						{
							float r = input[y + x] - average;
							SS_tot += r * r;
						}
						float R2 = 0;
						for(int s = 0; s < samples.Length; s++)
						{
							float[] window = windowSamples[s];
							float SS_res = 0;
							for(int x = 0; x < windowSize; x++)
							{
								float r = input[y + x] - window[x];
								SS_res += r * r;
							}
							R2 = Math.Max(R2, 1.0f - SS_res * samples[s].weight / SS_tot );
						}
						output[i + engine.loadOffset] = R2;
					}
				}*/
			}
		}
		internal class LegacyMainTone : LegacyModificator<MainTone>
		{
			public LegacyMainTone(Modificator mod) : base(mod) { }
			public override void Apply(float[] input, float[] output)
			{
				int windowSize = engine.RealWindow;
				int count = Mathf.RoundToInt(mod.quantile * windowSize);
				float[] chunk = new float[windowSize];
				float[] topFreq = new float[windowSize];
				float[] frequencies = Enumerable.Range(0, windowSize).Select(v => (float)v / windowSize / count).ToArray();
				int offset = engine.loadOffset;
				int chunks = input.Length;
				for (int i = 0; i < chunks; i++)
				{
					Array.Copy(input, i * windowSize, chunk, 0, windowSize);
					Array.Copy(frequencies, 0, topFreq, 0, windowSize);
					Array.Sort(chunk, topFreq);
					float averageFrequency = 0;
					for(int x = 0; x < count; x++)
					{
						averageFrequency += topFreq[x];
					}
					output[i + offset] = averageFrequency * Mathf.Pow(2.0f, 6.9f * (averageFrequency - 1.0f));
				}
			}
		}
		internal class LegacyShift : LegacyModificator<Shift>
		{
			public LegacyShift(Modificator mod) : base(mod) { }
			public override void Apply(float[] input, float[] output)
			{
				float middle = mod.averageLevel;
				float prev = input[0];
				float minDv = 0, maxDv = 0;
				int chunks = input.Length;
				for (int i = 1; i < chunks; i++)
				{
					float v = input[i];
					float dv = v - prev;
					input[i] = dv;
					prev = v;
					maxDv = dv > maxDv ? dv : maxDv;
					minDv = dv < minDv ? dv : minDv;
				}
				if (maxDv < 1e-6f || minDv > -1e-6f)
					return;

				float attackBoost = (1.0f - middle) / maxDv;
				float fadeBoost = middle / -minDv;
				input[0] = middle;
				for (int i = 1; i < chunks; i++)
				{
					float dv = input[i];
					input[i] = (dv > 0 ? dv * attackBoost : dv * fadeBoost) + middle;
				}
			}
		}
		internal class LegacyTimeLimit : LegacyModificator<TimeLimit>
		{
			public LegacyTimeLimit(Modificator mod) : base(mod) { }
			public override void Apply(float[] input, float[] output)
			{
				if(mod.fadeIn == mod.start && mod.start == mod.end && mod.end == mod.fadeOut)
					return;
				LegacyVolumeOverTime.Multiply(engine, input, new AnimationCurve(new Keyframe(mod.fadeIn, 0), new Keyframe(mod.start, 1), new Keyframe(mod.end, 1), new Keyframe(mod.fadeOut, 0)));
			}
		}
		internal class LegacyVolumeOverTime : LegacyModificator<VolumeOverTime>
		{
			public LegacyVolumeOverTime(Modificator mod) : base(mod) { }
			public override void Apply(float[] input, float[] output)
			{
				Multiply(engine, input, mod.curve);
			}
			internal static void Multiply(LegacyProgram engine, float[] input, AnimationCurve curve)
			{
				float timePerChunk = engine.FFTWindow / engine.frequency / engine.plan.multisamples;
				float halfWindowTime = 0.5f * engine.FFTWindow / engine.frequency;
				for(int i = 0; i < input.Length; i++)
				{
					float time = i * timePerChunk + halfWindowTime;
					input[i] *= curve.Evaluate(time);
				}
			}
		}
		internal class LegacyDamping : LegacyModificator<Damping>
		{
			public LegacyDamping(Modificator mod) : base(mod) { }
			public override void Apply(float[] input, float[] output)
			{
				float fadeSpeed = 1.0f / mod.damping - 1.0f;
				float timePerChunk = engine.FFTWindow / engine.frequency / engine.plan.multisamples;
				float lastValue = 0;
				float factor = Mathf.Exp(-fadeSpeed * timePerChunk);
				for(int i = 0; i < input.Length; i++)
				{
					lastValue *= factor;
					if(Mathf.Abs(input[i]) > Mathf.Abs(lastValue))
						lastValue = input[i];
					else
						lastValue = input[i] = lastValue + input[i] * (1.0f - factor);
				}
			}
		}
		internal class LegacyAmplitudeSmooth : LegacyModificator<AmplitudeSmooth>
		{
			public override bool UseTempBuffer { get { return true; } }
			public LegacyAmplitudeSmooth(Modificator mod) : base(mod) { }
			public override void Apply(float[] input, float[] output)
			{
				engine.SmoothSpectrum(input, output, mod.smoothness);
			}
		}
		internal class LegacyAccumulate : LegacyModificator<Accumulate>
		{
			public LegacyAccumulate(Modificator mod) : base(mod) { }
			public override void Apply(float[] input, float[] output)
			{
				float value = 0;
				for(int i = 0; i < input.Length; i++)
				{
					value += input[i];
					input[i] = value;
				}
			}
		}
		internal class LegacyNormalize : LegacyModificator<Normalize>
		{
			public LegacyNormalize(Modificator mod) : base(mod) { }
			public override void Apply(float[] input, float[] output)
			{
				LegacyProgram.Normalize(input);
			}
		}
		internal class LegacyRemap : LegacyModificator<Remap>
		{
			public LegacyRemap(Modificator mod) : base(mod) { }
			public override void Apply(float[] input, float[] output)
			{
				AnimationCurve remap = mod.remap;
				for(int i = 0; i < input.Length; i++)
					input[i] = remap.Evaluate(input[i]);
			}
		}
		internal class LegacyCustomPeaks : LegacyModificator<CustomPeaks>
		{
			public LegacyCustomPeaks(Modificator mod) : base(mod) { }
			public override void Apply(float[] input, float[] output)
			{
				float chunksPerSecond = engine.frequency / engine.FFTWindow * engine.plan.multisamples;
				float secondsPerChunk = 1.0f / chunksPerSecond;
				float halfWindowTime = 0.5f * engine.FFTWindow / engine.frequency;
				CustomPeaks.CustomPeak[] peaks = mod.peaks;
				AnimationCurve shape = mod.shape;
				Keyframe[] shapeKeys = shape.keys;
				float firstKeyTime = shapeKeys[0].time;
				float lastKeyTime = shapeKeys[shapeKeys.Length - 1].time;
				for (int p = 0; p < peaks.Length; p++)
				{
					float peakTime = peaks[p].time;
					float peakWidth = peaks[p].width;
					float multiplier = peaks[p].multiplier;
					float startTime = peakTime + peakWidth * firstKeyTime;
					float endTime   = peakTime + peakWidth * lastKeyTime;
					int startChunk = Mathf.Max(0, Mathf.FloorToInt(startTime * chunksPerSecond - 0.5f));
					int endChunk = Mathf.Min(input.Length, Mathf.CeilToInt(endTime * chunksPerSecond - 0.5f));
					for(int i = startChunk; i < endChunk; i++)
					{
						float time = i * secondsPerChunk + halfWindowTime;
						float oldValue = input[i];
						input[i] = Mathf.Max(oldValue, shape.Evaluate((time - peakTime) / peakWidth) * multiplier); 
					}
				}
			}
		}
		internal class LegacyBeatTrigger : LegacyModificator<BeatTrigger>
		{
			public LegacyBeatTrigger(Modificator mod) : base(mod) { }
			/*public void ApplyV2(float[] input, float[] output)
			{
				Plan plan = engine.Plan;
				float BPS = engine.bpm / 60.0f;
				float chunksPerSecond = engine.Frequency / plan.WindowSize * plan.multisamples;
				//int beatSize = Mathf.CeilToInt(60.0f / engine.bpm * chunksPerSecond);
				float fadeSpeed = Mathf.Log(0.01f) * BPS;
				float factor = Mathf.Exp(fadeSpeed / chunksPerSecond);
			}*/
			public override void Apply(float[] input, float[] output)
			{
				Plan plan = engine.Plan;
				float[] values = mod.values;
				float threshold = mod.threshold;
				int peakCount = input.Length;

				float chunksPerSecond = engine.Frequency / plan.WindowSize * plan.multisamples;
				int beatSize = Mathf.CeilToInt(60.0f / engine.bpm * chunksPerSecond);
				//int wSize = Mathf.CeilToInt(window * chunksPerSecond);
				int rmsWindow = Mathf.CeilToInt(30.0f / engine.bpm * chunksPerSecond * mod.window);

				float[] delta = new float[peakCount];
				for(int i = 1; i < peakCount; i++)
					delta[i] = Mathf.Max(0, input[i] - input[i - 1]);

				LegacyProgram.Normalize(delta);

				float[] RMSs = new float[peakCount];
				for(int i = 0; i < peakCount; i++)
				{
					int start = Math.Max(0, i - rmsWindow);
					int end = Math.Min(peakCount, i + rmsWindow + 1);
					float rms = 0;
					for(int k = start; k < end; k++)
						rms += delta[k] * delta[k];

					RMSs[i] = Mathf.Sqrt(rms / (end - start));
				}
#if UNITY_EDITOR
				if (BitAnimator.debugMode)
				{
					if (mod.debugDeltaOut)
					{
						Array.Copy(delta, input, peakCount);
						return;
					}
					if(mod.debugRMSOut)
					{
						Array.Copy(RMSs, input, peakCount);
						return;
					}
				}
#endif
				bool[] isBeat = new bool[peakCount];
				int[] lastPeak = new int[peakCount];
		
				bool printLog = BitAnimator.debugMode;
				StringBuilder log = printLog ? new StringBuilder() : null;
				int logLines = 0;

				int p = 0;
				for (int i = 1; i < peakCount - 1; i++)
				{
					bool isMax = delta[i - 1] <= delta[i] && delta[i] >= delta[i + 1];
					if(isMax && (delta[i] - RMSs[i]) > threshold)
					{
						isBeat[i] = true;
						lastPeak[i] = i - p;
						p = i;
						float k = (delta[i] - RMSs[i]) / threshold;
						if (printLog)
						{
							log.AppendLine(String.Format("time = {0:F2}, threshold = {3,4:F1}% delta = {1:F5} rms = {2:F5}", (i + 0.5f * plan.multisamples) / chunksPerSecond, delta[i], RMSs[i], k * 100.0f));
							++logLines;
							if (logLines >= 100)
							{
								lock (LegacyProgram.log)
									LegacyProgram.log.Add(new LogEvent(log.ToString()));
								log.Clear();
								logLines = 0;
							}
						}
					}
				}
				if (printLog && logLines > 0)
				{
					lock (LegacyProgram.log)
						LegacyProgram.log.Add(new LogEvent(log.ToString()));
					log.Clear();
				}
				int[] nextPeak = new int[peakCount];
				p = peakCount - 1;
				for(int i = peakCount - 1; i > 0; i--)
				{
					if(isBeat[i])
					{
						nextPeak[i] = p - i;
						p = i;
					}
				}

				for(int i = 0; i < peakCount; i++)
					input[i] = 0;

				int state = 0;
				int valuesCount = values.Length;
				float value = valuesCount > 0 ? values[0] : 1.0f;
				float left = Math.Max(0, -mod.offset + 0.5f) * mod.width;
				float right = Math.Max(0, mod.offset + 0.5f) * mod.width;
				for(int i = 0; i < peakCount; i++)
				{
					if(isBeat[i])
					{
						int start = Mathf.FloorToInt(Mathf.Max(0, i - left * Math.Min(beatSize, lastPeak[i])));
						int end = Mathf.FloorToInt(Mathf.Min(peakCount, i + right * Math.Min(beatSize, nextPeak[i]) + 1.0f));
						for (int j = start; j < end; j++)
						{
							input[j] = value;
						}

						if (valuesCount > 0)
						{
							state = ++state % valuesCount;
							value = valuesCount > 0 ? values[state] : 1.0f;
						}
						else
						{
							value = 1.0f;
						}
					}
				}
			}
		}
		internal class LegacyRandomizer : LegacyModificator<Randomizer>
		{
			public LegacyRandomizer(Modificator mod) : base(mod) { }
			public override void Apply(float[] input, float[] output)
			{
				System.Random rand = new System.Random();
				float randomize = mod.randomize;
				for (int i = 0; i < input.Length; i++)
					input[i] = Mathf.LerpUnclamped(input[i], (float)rand.NextDouble(), randomize);
			}
		}
		internal class LegacyBeatFinder : LegacyModificator<BeatFinder>
		{
			DSPLib.FFT fft;
			float[] window;
			float[] chunk;
			float windowScale;
			public override bool UseTempBuffer { get { return true; } }
			public LegacyBeatFinder(Modificator mod) : base(mod) { }
			public override void Apply(float[] input, float[] output)
			{
				Plan plan = engine.Plan;
				float BPS = engine.bpm / 60.0f;
				float chunksPerSecond = engine.Frequency / plan.WindowSize * plan.multisamples;
				int bins = engine.RealWindow;
				//Log compession
				Logarithm(input);
				//Differentiation and Accumulation
				for(int i = 1; i < engine.spectrumChunks; i++)
				{
					float sum = 0;
					for(int x = 0; x < bins; x++)
					{
						sum += Mathf.Max(0.0f, input[i*bins + x] - input[(i - 1) * bins + x]);
					}
					sum /= bins;
					output[i + engine.loadOffset] = sum;
				}
				output[0 + engine.loadOffset] = output[1 + engine.loadOffset];
				//Normalization
				float[] buffer = engine.buffer;
				engine.SmoothSpectrum(output, buffer, 0.5f);
				for(int i = 0; i < engine.spectrumChunks; i++)
					output[i] = Mathf.Max(0.0f, output[i] - buffer[i]);

				if(fft == null)
				{
					int FFTWindow = Mathf.CeilToInt(chunksPerSecond * mod.searchTimeRange);
					int zeroPadding = Mathf.NextPowerOfTwo(FFTWindow) - FFTWindow;

					fft = new DSPLib.FFT();
					fft.Initialize((uint)FFTWindow, (uint)zeroPadding);
					window = DSPLib.DSP.Window.Coefficients(DSPLib.DSP.Window.Type.Hann, (uint)FFTWindow, 80.0f);
					windowScale = DSPLib.DSP.Window.ScaleFactor.Signal(window);
					chunk = new float[FFTWindow];
				}
				
				int multisamples = 8;
				int chunks = engine.spectrumChunks / chunk.Length * multisamples - (multisamples - 1);
				
				//float[] frequencies = fft.FrequencySpan(chunksPerSecond * searchTimeRange * 60.0f);
				float maxBPM = chunksPerSecond * 60.0f;
				float bpmPerChunk = maxBPM / Mathf.NextPowerOfTwo(chunk.Length);
				int realSize = Mathf.NextPowerOfTwo(chunk.Length) / 2;
				Numerics.Complex[] tempogramm = new Numerics.Complex[chunks * realSize];
				for(int i = 0; i < chunks; i++)
				{
					Array.Copy(output, i * chunk.Length / multisamples, chunk, 0, chunk.Length);
					Multiply(chunk, window);
					Numerics.Complex[] fftSpectrum = fft.Execute(chunk);
					Array.Copy(fftSpectrum, 1, tempogramm, i * realSize, realSize);
					/*float[] scaledFFTSpectrum = DSPLib.DSP.ConvertComplex.ToMagnitude(fftSpectrum);
					float[] FFTPhase = DSPLib.DSP.ConvertComplex.ToPhaseRadians(fftSpectrum);
					int[] indexes = Enumerable.Range(0, fftSpectrum.Length).ToArray();
					Array.Sort(scaledFFTSpectrum, indexes);
					for(int p = fftSpectrum.Length - 1; p >= fftSpectrum.Length - 5; p--)
					{
						scaledFFTSpectrum[indexes[p]];
					}
					//Array.Reverse();
					Multiply(scaledFFTSpectrum, windowScale);
					if(BitAnimator.debugMode)
						for(int j = 6; j < 300; j++)
						{
							float x = Mathf.Min(j*5 / bpmPerChunk, scaledFFTSpectrum.Length - 1);
							int k = Mathf.FloorToInt(x);
							int h = Mathf.CeilToInt(x);
							x = Mathf.Repeat(x, 1.0f);
							float value = Mathf.LerpUnclamped(scaledFFTSpectrum[k], scaledFFTSpectrum[h], x);
							float phase = Mathf.LerpUnclamped(FFTPhase[k], FFTPhase[h], x);
							max = Mathf.Max(max, value);
							Color color;
							color.r = Mathf.Max(0, phase / Mathf.PI);
							color.g = value;
							color.b = Mathf.Max(0, -phase / Mathf.PI);
							color.a = 1;
							colors[i + j * chunks] = color;
						}*/
				}
				Vector2[] gradient = new Vector2[chunks * realSize];
				float[] magnitudes = new float[chunks * realSize];
				for(int i = 0; i < tempogramm.Length; i++)
					magnitudes[i] = tempogramm[i].Magnitude;

				for(int x = 1; x < chunks ; x++)
				{
					for(int y = 1; y < realSize; y++)
					{
						Vector2 sum;
						sum.y = sum.x = magnitudes[x * realSize + y];
						sum.x -= magnitudes[(x - 1) * realSize + y];
						sum.y -= magnitudes[x * realSize + y - 1];
						gradient[x * realSize + y] = sum;
					}
				}
			}
			/*void BPM_Match(Numerics.Complex[] tempogramm, out float[] bpm, out float[] phase, out float[] match)
			{

			}*/
			void Logarithm(float[] values)
			{
				for(int i = 0; i < values.Length; i++)
					values[i] = Mathf.Log(values[i] + 1e-6f);
			}
		}
	}
}