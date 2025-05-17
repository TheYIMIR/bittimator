
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
using UnityEngine;
using AudioVisualization.Modificators;

namespace AudioVisualization
{
	public partial class ComputeProgram : Engine
	{
		public interface ICSSpectrumResolver
		{
			void Resolve(ComputeProgram core, ComputeBuffer input, ComputeBuffer output);
		}
		public interface ICSModificator : IModificator, IDisposable
		{
			bool MultipassRequired { get; }
			void Apply(ComputeProgram core, ComputeBuffer input, ComputeBuffer output);
		}
		public class ComputeTask : Task
		{
			public static Factory<SpectrumResolver, ICSSpectrumResolver> resolverFactory = new Factory<SpectrumResolver, ICSSpectrumResolver>();
			public static Factory<Modificator, ICSModificator> modFactory = new Factory<Modificator, ICSModificator>();
			//static Type[] modTypes = System.Reflection.Assembly.GetAssembly(typeof(Modificator)).GetTypes().Where(t => t.IsSubclassOf(typeof(Modificator)) && t.GetInterfaces().Contains(typeof(ILegacyModificator))).ToArray();
			internal ICSSpectrumResolver csResolver;
			//static Type[] modTypes = System.Reflection.Assembly.GetAssembly(typeof(Modificator)).GetTypes().Where(t => t.IsSubclassOf(typeof(Modificator)) && t.GetInterfaces().Contains(typeof(ICSModificator))).ToArray();
			internal ICSModificator[] gpuMods;
			public ComputeBuffer values;
			public ComputeBuffer buffer;
			public ComputeBuffer keyframes;
			public Keyframe[][] hostKeyframes;
			public ComputeBuffer gradientKeyframes;
			public int gradientKeys;
			static ComputeTask()
			{
				//modFactory.Register(mod => new CSMainTone(mod), typeof(MainTone));
				//modFactory.Register(mod => new CSShift(mod), typeof(Shift));
				modFactory.Register(mod => new CSTimeLimit(mod), typeof(TimeLimit));
				modFactory.Register(mod => new CSVolumeOverTime(mod), typeof(VolumeOverTime));
				modFactory.Register(mod => new CSDamping(mod), typeof(Damping));
				modFactory.Register(mod => new CSAmplitudeSmooth(mod), typeof(AmplitudeSmooth));
				modFactory.Register(mod => new CSAccumulate(mod), typeof(Accumulate));
				modFactory.Register(mod => new CSNormalize(mod), typeof(Normalize));
				modFactory.Register(mod => new CSRemap(mod), typeof(Remap));
				//modFactory.Register(mod => new CSCustomPeaks(mod), typeof(CustomPeaks));
				//modFactory.Register(mod => new CSBeatTrigger(mod), typeof(BeatTrigger));
				//modFactory.Register(mod => new CSRandomizer(mod), typeof(Randomizer));
				//modFactory.Register(mod => new CSBeatFinder(mod), typeof(BeatFinder));
			}
			public ComputeTask(BitAnimator.RecordSlot slot, int valuesCount) : base(slot)
			{
				gpuMods = slot.modificators.Select(m => modFactory.Convert(m)).Where(m => m != null).ToArray();
				//Устанавливаем соотношение амплитуды спектра и результирующих значений ключевых кадров
				GradientColorKey[] colors = slot.colors.colorKeys;
				GradientAlphaKey[] alpha = slot.colors.alphaKeys;
				if (slot.type == BitAnimator.PropertyType.Color)
				{
					Keyframe[] ks = new Keyframe[colors.Length * 3 + alpha.Length];
					int offset;
					for(int c = 0; c < 3; c++)
					{
						offset = c * colors.Length;
						for(int i = 0; i < colors.Length; i++)
						{
							ks[offset + i].time = colors[i].time;
							ks[offset + i].value = colors[i].color[c];
						}
					}
					offset = 3 * colors.Length;
					for(int i = 0; i < colors.Length; i++)
					{
						ks[offset + i].time = alpha[i].time;
						ks[offset + i].value = alpha[i].alpha;
					}
					gradientKeyframes = new ComputeBuffer(ks.Length, ComputeProgram.keyframeSize);
					gradientKeyframes.SetData(ks);
					gradientKeys = colors.Length;
				}

				values = new ComputeBuffer(valuesCount, 4);
				buffer = new ComputeBuffer(valuesCount, 4);
				//Резервируем буффер для ключей XYZW|RGBA
				if(Channels > 0)
					keyframes = new ComputeBuffer(valuesCount * Channels, ComputeProgram.keyframeSize);
			}
			public void Swap()
			{
				ComputeBuffer temp = values;
				values = buffer;
				buffer = temp;
			}
			override public Keyframe[] GetKeyframes(int channel)
			{
				int count = keyframes.count / Channels;
				if(hostKeyframes == null)
				{
					hostKeyframes = new Keyframe[Channels][];
					for(int i = 0; i < Channels; i++)
					{
						hostKeyframes[i] = new Keyframe[count];
						keyframes.GetData(hostKeyframes[i], 0, i * count, count);
					}
				}
				return hostKeyframes[channel];
			}
			override public void Dispose()
			{
				foreach(ICSModificator mod in gpuMods)
					mod.Dispose();
				if(values != null)
					values.Dispose();
				if(buffer != null)
					buffer.Dispose();
				if(keyframes != null)
					keyframes.Dispose();
				if(gradientKeyframes != null)
					gradientKeyframes.Dispose();
				values = null;
				buffer = null;
				keyframes = null;
				gradientKeyframes = null;
			}
		}
		
		internal class CSModificator<T> where T : Modificator, IModificator
		{
			public T mod;
			public bool Enabled { get { return mod.enabled; } set { mod.enabled = value; } }
			public virtual bool UseTempBuffer { get { return false; } }
			public virtual bool UseMask { get { return false; } }
			public virtual bool MultipassRequired { get { return false; } }
			public virtual ExecutionQueue Queue { get { return ExecutionQueue.Peaks; } }
			public CSModificator(Modificator mod)
			{
				this.mod = (T)mod;
			}
		}
		internal class CSTimeLimit : CSModificator<TimeLimit>, ICSModificator
		{
			public CSTimeLimit(Modificator mod) : base(mod) { }
			public void Apply(ComputeProgram core, ComputeBuffer input, ComputeBuffer output)
			{
				if (mod.fadeIn == mod.start && mod.start == mod.end && mod.end == mod.fadeOut)
					return;
				Keyframe[] k = new Keyframe[4];
				k[0].time = mod.fadeIn;
				k[1].time = mod.start;
				k[1].value = 1;
				k[2].time = mod.end;
				k[2].value = 1;
				k[3].time = mod.fadeOut;
				core.Multiply(input, k);
			}
			public void Dispose() { }
		}
		internal class CSVolumeOverTime : CSModificator<VolumeOverTime>, ICSModificator
		{
			public CSVolumeOverTime(Modificator mod) : base(mod) { }
			public void Apply(ComputeProgram core, ComputeBuffer input, ComputeBuffer output)
			{
				core.Multiply(input, mod.curve.keys);
			}
			public void Dispose() { }
		}
		internal class CSDamping : CSModificator<Damping>, ICSModificator
		{
			public override bool UseTempBuffer { get { return true; } }
			public CSDamping(Modificator mod) : base(mod) { }
			public void Apply(ComputeProgram core, ComputeBuffer input, ComputeBuffer output)
			{
				ApplyFading(core, input, output, input.count, 1.0f / mod.damping - 1.0f);
			}
			internal static void ApplyFading(ComputeProgram core, ComputeBuffer input, ComputeBuffer output, int count, float fadeSpeed)
			{
				ComputeShader utilsCS = core.utilsCS;
				Kernel DampingKernel = core.DampingKernel;
				Kernel CopyBuffer = core.CopyBuffer;
				utilsCS.SetInts(_GridOffset, 0, 0, 0);
				utilsCS.SetInts(_GridSize, count, 1, 1);
				float chunkTime = (float)core.FFTWindow / core.plan.multisamples / core.frequency;
				bool swap = true;
				for(int offset = 1; offset < count; offset *= 2)
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
				if(swap)
				{
					utilsCS.SetBuffer(CopyBuffer.ID, _Input, input);
					utilsCS.SetBuffer(CopyBuffer.ID, _Output, output);
					utilsCS.DispatchGrid(CopyBuffer, count);
				}
			}
			public void Dispose() { }
		}
		internal class CSAmplitudeSmooth : CSModificator<AmplitudeSmooth>, ICSModificator
		{
			public override bool UseTempBuffer { get { return true; } }
			public CSAmplitudeSmooth(Modificator mod) : base(mod) { }
			public void Apply(ComputeProgram core, ComputeBuffer input, ComputeBuffer output)
			{
				core.SmoothSpectrum(input, output, mod.smoothness);
			}
			public void Dispose() { }
		}
		internal class CSAccumulate : CSModificator<Accumulate>, ICSModificator
		{
			public override bool UseTempBuffer { get { return true; } }
			public CSAccumulate(Modificator mod) : base(mod) { }
			public void Apply(ComputeProgram core, ComputeBuffer input, ComputeBuffer output)
			{
				CalculatePrefixSum(core, input, output, input.count);
			}
			internal static void CalculatePrefixSum(ComputeProgram core, ComputeBuffer input, ComputeBuffer output, int count)
			{
				ComputeShader utilsCS = core.utilsCS;
				Kernel PrefixSum = core.PrefixSum;
				Kernel CopyBuffer = core.CopyBuffer;
				utilsCS.SetBuffer(PrefixSum.ID, _Input, input);
				utilsCS.SetBuffer(PrefixSum.ID, _Output, output);
				utilsCS.SetInts(_GridOffset, 0, 0, 0);
				utilsCS.SetInts(_GridSize, count, 1, 1);
				bool swap = true;
				for(int x = 1; x < count; x *= 2)
				{
					utilsCS.SetBuffer(PrefixSum.ID, _Input, input);
					utilsCS.SetBuffer(PrefixSum.ID, _Output, output);
					utilsCS.SetInt(_N, x);
					utilsCS.DispatchGrid(PrefixSum, count);
					Swap(ref input, ref output);
					swap ^= true;
				}
				if(swap)
				{
					utilsCS.SetBuffer(CopyBuffer.ID, _Input, input);
					utilsCS.SetBuffer(CopyBuffer.ID, _Output, output);
					utilsCS.DispatchGrid(CopyBuffer, count);
				}
			}
			public void Dispose() { }
		}
		internal class CSNormalize : CSModificator<Normalize>, ICSModificator
		{
			public CSNormalize(Modificator mod) : base(mod) { }
			public void Apply(ComputeProgram core, ComputeBuffer input, ComputeBuffer output)
			{
				core.Normalize(input);
			}
			public void Dispose() { }
		}
		internal class CSRemap : CSModificator<Remap>, ICSModificator
		{
			protected ComputeBuffer remapKeyframes;
			public override bool UseTempBuffer { get { return true; } }
			public CSRemap(Modificator mod) : base(mod) { }
			public void Apply(ComputeProgram core, ComputeBuffer input, ComputeBuffer output)
			{
				core.ApplyRemap(input, output, input.count, remapKeyframes);
			}
			public void Dispose()
			{
				remapKeyframes.Dispose();
			}
		}
	}
}