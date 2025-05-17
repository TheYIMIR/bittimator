using System;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;
using UnityEngine.Video;
using Debug = UnityEngine.Debug;

public class FFMPEGMediaInfo
{
	public float Duration { get; private set; }
	public float Framerate { get; private set; }
	public Vector2Int FrameSize { get; private set; }
	public long Frames { get; private set; }
	public int Bitrate { get; private set; }
	public float AspectRatio { get; private set; }
	public long AudioFrequency { get; private set; }
	public long AudioSamples { get; private set; }
	public bool HasAudio { get; private set; }
	public bool HasVideo { get; private set; }

	public MediaStreamFormat format = null;
	public MediaStreamInfo[] streams = null;
	public static FFMPEGMediaInfo Parse(string json)
	{
		FFMPEGMediaInfo mi = JsonUtility.FromJson<FFMPEGMediaInfo>(json);
		mi.Refresh();
		return mi;
	}
	public FFMPEGMediaInfo()
	{
	}
	public FFMPEGMediaInfo(string filename)
	{
		if (String.IsNullOrEmpty(filename))
			return;

		string arguments = String.Format("-print_format json -show_format -show_streams -i \"{0}\"", filename);

		ProcessStartInfo start = new ProcessStartInfo(FFMPEG.FFprobePath, arguments)
		{
			CreateNoWindow = true,
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};
		using (Process ffprobe = Process.Start(start))
		{
			string json = ffprobe.StandardOutput.ReadToEnd();
			string errors = ffprobe.StandardError.ReadToEnd();
			ffprobe.WaitForExit();

			if (ffprobe.ExitCode != 0)
			{
				Debug.LogError(errors);
				return;
			}
			JsonUtility.FromJsonOverwrite(json, this);
			Refresh();
		}
	}
	public FFMPEGMediaInfo(VideoClip clip)
	{
		if (clip == null)
			return;

		Duration = (float)clip.length;
		Framerate = (float)clip.frameRate;
		FrameSize = new Vector2Int((int)clip.width, (int)clip.height);
		Frames = (long)clip.frameCount;
		Bitrate = 0;
		HasVideo = true;
	}
	[Serializable]
	public class MediaStreamFormat
	{
		public float Duration { get; private set; }
		public int Bitrate { get; private set; }
		//[SerializeField] string filename = null;
		//[SerializeField] int nb_streams = 0;
		//[SerializeField] int nb_programs = 0;
		//[SerializeField] string format_name = null;
		//[SerializeField] string format_long_name = null;
		//[SerializeField] string start_time = null;
		[SerializeField] string duration = null;
		//[SerializeField] string size = null;
		[SerializeField] string bit_rate = null;
		//[SerializeField] int probe_score = 0;
		//[SerializeField] Dictionary<string, string> tags = null;

		internal void Refresh()
		{
			if (!String.IsNullOrEmpty(duration))
			{
				float d;
				ParseFloat(duration, out d);
				Duration = d;
			}
			if (!String.IsNullOrEmpty(bit_rate))
				Bitrate = int.Parse(bit_rate, CultureInfo.InvariantCulture);
		}
	}
	[Serializable]
	public class MediaStreamInfo
	{
		public enum StreamType
		{
			Other, Video, Audio
		}
		public StreamType Content { get; private set; }
		public int Index { get { return index; } }
		public int Bitrate { get { return _bitrate; } }
		public long Frames { get { return _frames; } }
		public float Duration { get { return _duration; } }
		public float Framerate { get; private set; }
		public float AspectRatio { get; private set; }
		public long AudioFrequency { get; private set; }
		public long AudioSamples { get; private set; }
		public long TimeBase { get; private set; }
		public Vector2Int FrameSize { get; private set; }
		[SerializeField] int _bitrate = 0;
		[SerializeField] long _frames = 0;
		[SerializeField] float _duration = 0;
		[SerializeField] int index = 0;
		[SerializeField] string codec_type = null;
		[SerializeField] int width = 0;
		[SerializeField] int height = 0;
		[SerializeField] string display_aspect_ratio = null;
		[SerializeField] string r_frame_rate = null;
		[SerializeField] string duration = null;
		[SerializeField] string bit_rate = null;
		[SerializeField] string nb_frames = null;
		[SerializeField] string sample_rate = null;
		[SerializeField] string time_base = null;
		[SerializeField] long start_pts = 0;
		[SerializeField] long duration_ts = 0;
		internal void Refresh()
		{
			try
			{
				Content = (StreamType)Enum.Parse(typeof(StreamType), codec_type, true);

				ParseFloat(duration, out _duration);
				int.TryParse(bit_rate, out _bitrate);

				if (Content == StreamType.Video)
				{
					long.TryParse(nb_frames, out _frames);
					FrameSize = new Vector2Int(width, height);
					string[] rd = r_frame_rate.Split('/');
					float r, d;
					ParseFloat(rd[0], out r);
					if (rd.Length > 1 && ParseFloat(rd[1], out d))
						Framerate = r / d;
					else
						Framerate = r;

					if (!String.IsNullOrEmpty(display_aspect_ratio))
					{
						string[] xy = display_aspect_ratio.Split(':');
						float x, y;
						ParseFloat(xy[0], out x);
						if (xy.Length > 1 && ParseFloat(xy[1], out y))
							AspectRatio = x / y;
						else
							AspectRatio = x;
					}
					else
						AspectRatio = (float)FrameSize.x / FrameSize.y;
				}
				if (Content == StreamType.Audio)
				{
					long freq = 0;
					long.TryParse(sample_rate, out freq);
					AudioFrequency = freq;

					if (!String.IsNullOrEmpty(time_base))
					{
						long tb = 0;
						string[] rd = time_base.Split('/');
						long.TryParse(rd[1], out tb);
						TimeBase = tb;
					}
					long startSample = start_pts * AudioFrequency / TimeBase;
					long samples = duration_ts * AudioFrequency / TimeBase;
					AudioSamples = samples;
				}
			}
			catch (ArgumentException) { }
		}
	}
	public void Refresh()
	{
		format.Refresh();
		Duration = format.Duration;
		Bitrate = format.Bitrate;
		foreach (MediaStreamInfo s in streams)
		{
			s.Refresh();
			if (s.Content == MediaStreamInfo.StreamType.Video)
			{
				AspectRatio = s.AspectRatio;
				FrameSize = s.FrameSize;
				Framerate = s.Framerate;
				Frames = s.Frames;
				HasVideo = true;
			}
			if (s.Content == MediaStreamInfo.StreamType.Audio)
			{
				AudioFrequency = s.AudioFrequency;
				AudioSamples = s.AudioSamples;
				HasAudio = true;
			}
		}
	}
	static bool ParseFloat(string str, out float value)
	{
		return float.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out value);
	}
}