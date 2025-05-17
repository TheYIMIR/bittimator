
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

using DSPLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;
using UnityEngine;
using System.Linq;

namespace AudioVisualization
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public sealed class EngineInfoAttribute : Attribute
	{
		public string Name;
		public bool Renderer;
		public EngineInfoAttribute(string Name, bool Renderer = false)
		{
			this.Name = Name;
			this.Renderer = Renderer;
		}
	}
	public abstract class Engine : IDisposable
	{
		public enum CoreType
		{
			Auto = 0, Legacy = 1, ComputeShaders = 2
		}
		public static int maxUseRAM = SystemInfo.systemMemorySize / 100;
		public static int maxUseVRAM = SystemInfo.graphicsMemorySize / 100;
		protected AudioClipContainer audioClip = new AudioClipContainer();
		protected Plan plan;
		protected int FFTWindow;
		protected int RealWindow;
		protected int bufferSwap;
		protected int chunks;
		protected int spectrumChunks;
		protected int settingsHash;
		protected float windowScale;
		protected float frequency;
		public int bpm;
		public float beatOffset;
		public float Progress { get; protected set; }
		public string Status { get; set; }
		public virtual void Initialize(EngineSettings settings, Plan plan, AudioClip audio) { }
		public abstract IEnumerator ComputeAnimation(IEnumerable<BitAnimator.RecordSlot> slots);
		public abstract IEnumerable<Task> GetTasks();
		public abstract IEnumerator ComputeBPM();
		public virtual void Dispose() {}
		public Plan Plan { get { return plan; } }
		public float Frequency { get { return frequency; } }
		public abstract CoreType Type { get; }
		protected bool AssignSettings(EngineSettings settings, Plan plan)
		{
			int hash = (settings != null ? settings.GetHashCode() * -1521134295 : 0) + plan.GetHashCode();
			if (hash != settingsHash)
			{
				settingsHash = hash;
				return true;
			}
			return false;
		}
	}
	[Serializable]
	public class EngineSettings : ScriptableObject
	{
		public static bool operator ==(EngineSettings settings, EngineSettings another)
		{
			if (ReferenceEquals(settings, null) && ReferenceEquals(another, null))
				return true;
			return !ReferenceEquals(settings, null) && settings.Equals(another);
		}
		public static bool operator !=(EngineSettings settings, EngineSettings another)
		{
			return !(settings == another);
		}
		public override bool Equals(object other) { return ReferenceEquals(this, other); }
		public override int GetHashCode() { return base.GetHashCode(); }
#if UNITY_EDITOR
		public virtual void DrawProperty() { }
#endif
	}
	[Flags]
	public enum Mode
	{
		LogFrequency = 1,
		LogAmplitude = 2,
		EnergyCorrection = 4,
		CalculateFons = 8,
		UseRuntimeFFT = 16,
		RemapVolume = 32,
		ResolveMultisamles = 64,
		RuntimeNormalize = 128
	}
	[Serializable]
	[StructLayout(LayoutKind.Sequential)]
	public struct Plan
	{
		[Range(7, 16)]
		public int windowLogSize;
		[Range(1, 32)]
		public int multisamples;
		public Mode mode;
		public DSP.Window.Type filter;
		[Range(0, 200)]
		public float windowParam;
		[Range(0, 1)]
		public float quality;
		public Plan(int _windowLogSize = 12, 
			int _multisamples = 2, 
			DSP.Window.Type _filter = DSP.Window.Type.Hann, 
			Mode _mode = Mode.EnergyCorrection,
			float _windowParam = 60.0f,
			float _quality = 0.5f)
		{
			windowLogSize = _windowLogSize;
			multisamples = _multisamples;
			filter = _filter;
			mode = _mode;
			windowParam = _windowParam;
			quality = _quality;
		}
		public int WindowSize
		{
			get { return 1 << windowLogSize; }
			set { windowLogSize = Mathf.CeilToInt(Mathf.Log(value, 2.0f)); }
		}
		public override bool Equals(object plan)
		{
			if (plan is Plan)
			{
				return (Plan)plan == this;
			}
			else
				return false;
		}

		public override int GetHashCode()
		{
			var hashCode = -130738350;
			hashCode = hashCode * -1521134295 + windowLogSize.GetHashCode();
			hashCode = hashCode * -1521134295 + multisamples.GetHashCode();
			hashCode = hashCode * -1521134295 + mode.GetHashCode();
			hashCode = hashCode * -1521134295 + filter.GetHashCode();
			hashCode = hashCode * -1521134295 + windowParam.GetHashCode();
			hashCode = hashCode * -1521134295 + quality.GetHashCode();
			return hashCode;
		}

		public static bool operator ==(Plan plan, Plan another)
		{
			return plan.windowLogSize == another.windowLogSize
				&& plan.multisamples == another.multisamples
				&& plan.filter == another.filter
				&& plan.windowParam == another.windowParam
				&& plan.mode == another.mode
				&& plan.quality == another.quality;
		}
		public static bool operator !=(Plan plan, Plan another)
		{
			return plan.windowLogSize != another.windowLogSize
				|| plan.multisamples != another.multisamples
				|| plan.filter != another.filter
				|| plan.windowParam != another.windowParam
				|| plan.mode != another.mode
				|| plan.quality != another.quality;
		}
	}
	public abstract class Task : IDisposable
	{
		public BitAnimator.RecordSlot slot;
		public Task() {}
		public Task(BitAnimator.RecordSlot slot)
		{
			this.slot = slot;
		}
		public abstract Keyframe[] GetKeyframes(int channel);
		public abstract void Dispose();
		public float AmplitudeRange(int channel)
		{
			return slot.maxValue[channel] - slot.minValue[channel];
		}
		public int Channels
		{
			get
			{
				return (slot.channelMask & 1) + (slot.channelMask >> 1 & 1) + (slot.channelMask >> 2 & 1) + (slot.channelMask >> 3 & 1);
			}
		}
	}
	public class Factory<I, O>
	{
		Dictionary<Type, Func<I, O>> factories = new Dictionary<Type, Func<I, O>>();
		internal void Register(Func<I, O> factory, Type type)
		{
			if (factories.ContainsKey(type))
				factories[type] = factory;
			else
				factories.Add(type, factory);
		}
		internal O Convert(I input)
		{
			Func<I, O> factory;
			if (factories.TryGetValue(input.GetType(), out factory))
				return factory(input);

			return default(O);
		}
	}
}
