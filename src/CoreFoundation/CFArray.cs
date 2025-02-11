// 
// CFArray.cs: P/Invokes for CFArray
//
// Authors:
//    Mono Team
//    Rolf Bjarne Kvinge (rolf@xamarin.com)
//
//     
// Copyright 2010 Novell, Inc
// Copyright 2012-2014 Xamarin Inc. All rights reserved.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Runtime.InteropServices;
using Foundation;
using ObjCRuntime;

using CFIndex = System.nint;
using CFArrayRef = System.IntPtr;
using CFAllocatorRef = System.IntPtr;

#nullable enable

namespace CoreFoundation {
	
	// interesting bits: https://github.com/opensource-apple/CF/blob/master/CFArray.c
	public partial class CFArray : NativeObject {

		internal CFArray (IntPtr handle)
			: base (handle, false)
		{
		}

		[Preserve (Conditional = true)]
		internal CFArray (IntPtr handle, bool owns)
			: base (handle, owns)
		{
		}
		
		[DllImport (Constants.CoreFoundationLibrary, EntryPoint="CFArrayGetTypeID")]
		internal extern static /* CFTypeID */ nint GetTypeID ();

		// pointer to a const struct (REALLY APPLE?)
		static IntPtr kCFTypeArrayCallbacks_ptr_value;
		static IntPtr kCFTypeArrayCallbacks_ptr {
			get {
				// FIXME: right now we can't use [Fields] for GetIndirect
				if (kCFTypeArrayCallbacks_ptr_value == IntPtr.Zero)
					kCFTypeArrayCallbacks_ptr_value = Dlfcn.GetIndirect (Libraries.CoreFoundation.Handle, "kCFTypeArrayCallBacks");
				return kCFTypeArrayCallbacks_ptr_value;
			}
		}

		internal static CFArray FromIntPtrs (params IntPtr[] values)
		{
			return new CFArray (Create (values), true);
		}

		internal static CFArray FromNativeObjects (params INativeObject[] values)
		{
			return new CFArray (Create (values), true);
		}

		public nint Count {
			get { return GetCount (GetCheckedHandle ()); }
		}

		[DllImport (Constants.CoreFoundationLibrary)]
		extern static /* CFArrayRef */ IntPtr CFArrayCreate (/* CFAllocatorRef */ IntPtr allocator, /* void** */ IntPtr values, nint numValues, /* CFArrayCallBacks* */ IntPtr callBacks);

		[DllImport (Constants.CoreFoundationLibrary)]
		internal extern static /* void* */ IntPtr CFArrayGetValueAtIndex (/* CFArrayRef */ IntPtr theArray, /* CFIndex */ nint idx);

		public IntPtr GetValue (nint index)
		{
			return CFArrayGetValueAtIndex (GetCheckedHandle (), index);
		}

		internal static unsafe IntPtr Create (params IntPtr[] values)
		{
			if (values is null)
				ObjCRuntime.ThrowHelper.ThrowArgumentNullException (nameof (values));
			fixed (IntPtr* pv = values) {
				return CFArrayCreate (IntPtr.Zero, 
						(IntPtr) pv,
						values.Length,
						kCFTypeArrayCallbacks_ptr);
			}
		}

		public static unsafe IntPtr Create (params INativeObject[] values)
		{
			if (values is null)
				ObjCRuntime.ThrowHelper.ThrowArgumentNullException (nameof (values));
			int c = values.Length;
			var _values = c <= 256 ? stackalloc IntPtr [c] : new IntPtr [c];
			for (int i = 0; i < c; ++i)
				_values [i] = values [i].Handle;
			fixed (IntPtr* pv = _values)
				return CFArrayCreate (IntPtr.Zero, (IntPtr) pv, c, kCFTypeArrayCallbacks_ptr);
		}

		[DllImport (Constants.CoreFoundationLibrary, EntryPoint="CFArrayGetCount")]
		internal extern static /* CFIndex */ nint GetCount (/* CFArrayRef */ IntPtr theArray);

		[DllImport (Constants.CoreFoundationLibrary)]
		extern static CFArrayRef CFArrayCreateCopy (CFAllocatorRef allocator, CFArrayRef theArray);

		internal CFArray Clone () => new CFArray (CFArrayCreateCopy (IntPtr.Zero, GetCheckedHandle ()), true);

		[DllImport (Constants.CoreFoundationLibrary)]
		internal extern static void CFArrayGetValues (/* CFArrayRef */ IntPtr theArray, CFRange range, /* const void ** */ IntPtr values);

		// identical signature to NSArray API
		static unsafe public string?[]? StringArrayFromHandle (IntPtr handle)
		{
			if (handle == IntPtr.Zero)
				return null;

			var c = (int) GetCount (handle);
			if (c == 0)
				return Array.Empty<string> ();

			var buffer = c <= 256 ? stackalloc IntPtr [c] : new IntPtr [c];
			fixed (void* ptr = buffer)
				CFArrayGetValues (handle, new CFRange (0, c), (IntPtr) ptr);

			string?[] ret = new string [c];
			for (var i = 0; i < c; i++)
				ret [i] = CFString.FromHandle (buffer [i]);
			return ret;
		}

		// identical signature to NSArray API
		static public T?[]? ArrayFromHandle<T> (IntPtr handle) where T : class, INativeObject
		{
			if (handle == IntPtr.Zero)
				return null;

			var c = (int) GetCount (handle);
			if (c == 0)
				return Array.Empty<T> ();

			var buffer = c <= 256 ? stackalloc IntPtr [c] : new IntPtr [c];
			unsafe {
				fixed (void* ptr = buffer)
					CFArrayGetValues (handle, new CFRange (0, c), (IntPtr) ptr);
			}

			T?[] ret = new T [c];
			for (var i = 0; i < c; i++) {
				var val = buffer [i];
				if (val != CFNullHandle)
					ret [i] = Runtime.GetINativeObject<T> (val, false);
			}
			return ret;
		}
	}
}
