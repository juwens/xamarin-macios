//
// INIntentResolutionResult Generic variant
//
// Authors:
//	Alex Soto  <alexsoto@microsoft.com>
//
// Copyright 2016 Xamarin Inc. All rights reserved.
//

using System;
using System.Runtime.Versioning;
using Foundation;
using ObjCRuntime;

namespace Intents {

#if NET
	[SupportedOSPlatform ("tvos14.0")]
#else
	[iOS (10, 0)]
	[Mac (10, 12, 0, PlatformArchitecture.Arch64)]
	[Watch (3, 2)]
	[TV (14,0)]
#endif
	[Register ("INIntentResolutionResult", SkipRegistration = true)]
	public sealed partial class INIntentResolutionResult<ObjectType> : INIntentResolutionResult
		where ObjectType : class, INativeObject 
	{
		internal INIntentResolutionResult (IntPtr handle) : base (handle)
		{
		}
	}

	public partial class INIntentResolutionResult {

		public static INIntentResolutionResult NeedsValue {
			get {
				throw new NotImplementedException ("All subclasses of INIntentResolutionResult must re-implement this property");
			}
		}

		public static INIntentResolutionResult NotRequired {
			get {
				throw new NotImplementedException ("All subclasses of INIntentResolutionResult must re-implement this property");
			}
		}

		public static INIntentResolutionResult Unsupported {
			get {
				throw new NotImplementedException ("All subclasses of INIntentResolutionResult must re-implement this property");
			}
		}

#if NET
		[SupportedOSPlatform ("ios13.0")]
		[SupportedOSPlatform ("macos11.0")]
#else
		[Watch (6,0), iOS (13,0), Mac (11,0)]
#endif
		public static INIntentResolutionResult GetUnsupported (nint reason) => throw new NotImplementedException ("All subclasses of INIntentResolutionResult must re-implement this method");

#if NET
		[SupportedOSPlatform ("ios13.0")]
		[SupportedOSPlatform ("macos11.0")]
#else
		[Watch (6,0), iOS (13,0), Mac (11,0)]
#endif
		public static INIntentResolutionResult GetConfirmationRequired (NSObject itemToConfirm, nint reason) => throw new NotImplementedException ("All subclasses of INIntentResolutionResult must re-implement this method");

	}
}
