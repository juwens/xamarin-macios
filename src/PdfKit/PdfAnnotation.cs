//
// PdfAnnotation.cs
//
// Authors:
//	Alex Soto  <alexsoto@microsoft.com>
//
// Copyright 2017 Xamarin Inc. All rights reserved.
//

using System;
using System.Runtime.Versioning;

using CoreGraphics;
using Foundation;
using ObjCRuntime;

#nullable enable

namespace PdfKit {
	public partial class PdfAnnotation {

#if !NET
		[Mac (10,12)]
#endif
		public bool SetValue<T> (T value, PdfAnnotationKey key) where T : class, INativeObject
		{
			if (value == null)
				ObjCRuntime.ThrowHelper.ThrowArgumentNullException (nameof (value));

			return _SetValue (value.Handle, key.GetConstant ()!);
		}

#if !NET
		[Mac (10,12)]
#endif
		public bool SetValue (string str, PdfAnnotationKey key)
		{
			var nstr = NSString.CreateNative (str);
			try {
				return _SetValue (nstr, key.GetConstant ()!);
			} finally {
				NSString.ReleaseNative (nstr);
			}
		}

#if !NET
		[Mac (10,12)]
#endif
		public T GetValue<T> (PdfAnnotationKey key) where T : class, INativeObject
		{
			return Runtime.GetINativeObject<T> (_GetValue (key.GetConstant ()!), true);
		}

		public PdfAnnotationKey AnnotationType {
			get { return PdfAnnotationKeyExtensions.GetValue ((NSString) Type); }
			set { Type = value.GetConstant (); }
		}

#if !NET
		[Mac (10,13)]
#endif
		public CGPoint[] QuadrilateralPoints {
			get {
				return NSArray.ArrayFromHandleFunc<CGPoint> (_QuadrilateralPoints, (v) =>
					{
						using (var value = new NSValue (v))
							return value.CGPointValue;
					});
			}
			set {
				if (value == null) {
					_QuadrilateralPoints = IntPtr.Zero;
				} else {
					using (var arr = new NSMutableArray ()) {
						for (int i = 0; i < value.Length; i++)
							arr.Add (NSValue.FromCGPoint (value [i]));
						_QuadrilateralPoints = arr.Handle;
					}
				}
			}
		}
	}
}
