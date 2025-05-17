
// Copyright © 2021 Leviant. 
// E-mail: leviant@yandex.ru  
// Discord: Leviant#8796
// My Discord server: https://discord.gg/MdykFMf
// PayPal: https://paypal.me/LeviantTech
// License: http://opensource.org/licenses/MIT
// Version: 1.3 (01.10.2021)

using System;
using System.Diagnostics;
using UnityEngine;

namespace AudioVisualization
{
	public enum PlotGraphic
	{
		Peaks, Spectrum, Histogram//, Wave, BPM, Tones
	}
	public interface ISpectrumRenderer : IDisposable
    {
		float Maximum { get; }
        float RMS { get; }
		bool ApplyMods { get; set; }
		float ViewScale { get; set; }
		Engine.CoreType Type { get; }
		void Initialize(EngineSettings settings, Plan plan, AudioClip clip);
        void SetTask(BitAnimator.RecordSlot task, PlotGraphic type);
        void Render(Texture2D output);
        void Render(RenderTexture renderTarget, float time, float multiply);
	}
	[DebuggerDisplay("Start = {offset}, End = {offset + length}")]
	public sealed class CacheBlock : IComparable<CacheBlock>, IDisposable
	{
		public int offset;
		public int length;
		public float timeStart;
		public float timeEnd;
		public float maxValue;
		public float creationTime;
		public Texture texture;
		public CacheBlock() { }
		public CacheBlock(int _offest = 0, int _length = 0) { offset = _offest; length = _length; }
		public int End { get { return offset + length; } }
		public int CompareTo(CacheBlock other)
		{
			return offset - other.offset;
		}
		public bool Overlap(int start, int end)
		{
			int toThisBlock = offset - end;
			int toOtherBlock = start - End;
			return toThisBlock < 0 && toOtherBlock < 0;
		}
		public int OverlappedRange(int start, int end)
		{
			int floor = Mathf.Max(offset, start);
			int ceil = Mathf.Min(End, end);
			return Mathf.Max(0, ceil - floor);
		}
		public bool OverlapTime(float start, float end)
		{
			float toThisBlock = timeStart - end;
			float toOtherBlock = start - timeEnd;
			return toThisBlock < 0.0f && toOtherBlock < 0.0f;
		}
		// returns 0 if there are no space between them
		// returns +distance if this block on right side
		// returns -distance if this block on left side
		public int Distance(CacheBlock other)
		{
			int toThisBlock = offset - other.End;
			int toOtherBlock = other.offset - End;
			if(toThisBlock >= 0)
				return toThisBlock;
			else if(toOtherBlock >= 0)
				return -toOtherBlock;
			else
				return 0;
		}
		public void Dispose()
		{
			if(texture != null)
				Texture.DestroyImmediate(texture);
			texture = null;
		}
	}
}
