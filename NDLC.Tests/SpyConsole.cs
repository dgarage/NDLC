using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.IO;
using System.Net.Http.Headers;
using System.Text;
using Xunit.Abstractions;

namespace NDLC.Tests
{
	public class SpyConsole : IConsole
	{
		class TestStreamWriter : IStandardStreamWriter
		{
			public TestStreamWriter(ITestOutputHelper log, string suffix)
			{
				Log = log;
				Suffix = suffix;
			}

			public ITestOutputHelper Log { get; }
			public string Suffix { get; }

			public void Write(string value)
			{
				Logs.Append(value);
				value = value.TrimEnd();
				if (string.IsNullOrEmpty(value))
					return;
				Log.WriteLine(Suffix + value);
			}
			public StringBuilder Logs = new StringBuilder();
			public void Clear()
			{
				Logs.Clear();
			}
		}

		public void Clear()
		{
			outWriter.Clear();
			errWriter.Clear();
		}

		TestStreamWriter outWriter;
		TestStreamWriter errWriter;
		public SpyConsole(ITestOutputHelper log)
		{
			outWriter = new TestStreamWriter(log, "");
			errWriter = new TestStreamWriter(log, "");
		}

		internal string GetOutput()
		{
			return outWriter.Logs.ToString();
		}

		public IStandardStreamWriter Out => outWriter;

		public bool IsOutputRedirected => throw new NotImplementedException();

		public IStandardStreamWriter Error => errWriter;

		public bool IsErrorRedirected => throw new NotImplementedException();

		public bool IsInputRedirected => throw new NotImplementedException();
	}
}
