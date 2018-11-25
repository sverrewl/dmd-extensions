﻿using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using LibDmd.Output.Virtual;
using NLog;
using SkiaSharp.Extended.Svg;

namespace LibDmd.Common
{
	/// <summary>
	/// A borderless virtual DMD that resizes with the same aspect ratio (if not disabled)
	/// </summary>
	public partial class VirtualAlphaNumericDisplay : VirtualWindow
	{
		public VirtualAlphaNumericDisplay(int numChars, int numLines, ISubject<Dictionary<int, SKSvg>> segmentsLoaded)
		{
			InitializeComponent();
			Initialize();

			AlphaNumericDisplay.NumChars = numChars;
			AlphaNumericDisplay.NumLines = numLines;
			AlphaNumericDisplay.SegmentsLoaded = segmentsLoaded;
		}

		public override IVirtualControl VirtualControl => AlphaNumericDisplay;
	}
}