// 
// SecAccessControl.cs: Implements the managed SecAccessControl representation
//
// Authors: 
//  Miguel de Icaza  <miguel@xamarin.com>
//
// Copyright 2014, 2015 Xamarin Inc.
//
// Notice: to avoid having to track the object and then having to remove it
// this class exists merely to set the desired flags, and the sole consumer
// of this API, creates the object on demands and passes ownership when
// calling SecAddItem.
//

using System;
using System.Runtime.InteropServices;
using ObjCRuntime;
using CoreFoundation;
using Foundation;
using System.Runtime.Versioning;

namespace Security {

	[Flags]
	[Native]
#if XAMCORE_4_0
	// changed to CFOptionFlags in Xcode 8 SDK
	public enum SecAccessControlCreateFlags : ulong {
#else
	// CFOptionFlags -> SecAccessControl.h
	public enum SecAccessControlCreateFlags : long {
#endif
		UserPresence        = 1 << 0,

		[Advice ("'BiometryAny' is preferred over 'TouchIDAny' since Xcode 9.3. Touch ID and Face ID together are biometric authentication mechanisms.")]
#if !NET
		[iOS (9,0)][Mac (10,12,1)]
#endif
		TouchIDAny          = BiometryAny,

		[Advice ("'BiometryCurrentSet' is preferred over 'TouchIDCurrentSet' since Xcode 9.3. Touch ID and Face ID together are biometric authentication mechanisms.")]
#if !NET
		[iOS (9,0)][Mac (10,12,1)]
#endif
		TouchIDCurrentSet   = BiometryCurrentSet,

		// Added in iOS 11.3 and macOS 10.13.4 but keeping initial availability attribute because it's using the value
		// of 'TouchIDAny' which iOS 9 / macOS 10.12.1 will accept.
#if !NET
		[iOS (9,0), Mac (10,12,1)]
#endif
		BiometryAny         = 1 << 1,

		// Added in iOS 11.3 and macOS 10.13.4 but keeping initial availability attribute because it's using the value
		// of 'TouchIDCurrentSet' which iOS 9 / macOS 10.12.1 will accept.
#if !NET
		[iOS (9,0), Mac (10,12,1)]
#endif
		BiometryCurrentSet  = 1 << 3,

#if !NET
		[iOS (9,0)][Mac (10,11)]
#endif
		DevicePasscode      = 1 << 4,

#if !NET
		[Mac (10,15)][NoiOS][NoTV][NoWatch]
#else
		[SupportedOSPlatform ("macos10.15")]
#endif
		Watch               = 1 << 5,

#if !NET
		[iOS (9,0)][Mac (10,12,1)]
#endif
		Or                  = 1 << 14,

#if !NET
		[iOS (9,0)][Mac (10,12,1)]
#endif
		And                 = 1 << 15,

#if !NET
		[iOS (9,0)][Mac (10,12,1)]
#endif
		PrivateKeyUsage     = 1 << 30,

#if !NET
		[iOS (9,0)][Mac (10,12,1)]
#endif
		ApplicationPassword = 1 << 31,
	}
	
#if !NET
	[Mac (10,10)][iOS (8,0)]
#endif
	public partial class SecAccessControl : INativeObject, IDisposable {

		private IntPtr handle;

		public IntPtr Handle {
			get {
#if !COREBUILD
				if (handle == IntPtr.Zero) {
					IntPtr error;
					handle = SecAccessControlCreateWithFlags (IntPtr.Zero, KeysAccessible.FromSecAccessible (Accessible), (nint)(int)Flags, out error);
				}
#endif
				return handle;
			}
			internal set { handle = value; }
		}

		public void Dispose ()
		{
#if !COREBUILD
			Dispose (true);
#endif
			GC.SuppressFinalize (this);
		}

#if !COREBUILD
		internal SecAccessControl (IntPtr handle)
		{
			// note: the properties won't match reality
			Handle = handle;
		}

		public SecAccessControl (SecAccessible accessible, SecAccessControlCreateFlags flags = SecAccessControlCreateFlags.UserPresence)
		{
			Accessible = accessible;
			Flags = flags;
		}

		protected virtual void Dispose (bool disposing)
		{
			if (handle != IntPtr.Zero){
				CFObject.CFRelease (handle);
				handle = IntPtr.Zero;
			}
		}

		~SecAccessControl ()
		{
			Dispose (false);
		}
			
		public SecAccessible Accessible { get; private set; }
		public SecAccessControlCreateFlags Flags { get; private set; }

		[DllImport (Constants.SecurityLibrary)]
		extern static IntPtr SecAccessControlCreateWithFlags (IntPtr allocator, /* CFTypeRef */ IntPtr protection, /* SecAccessControlCreateFlags */ nint flags, out IntPtr error);
#endif
	}
}
