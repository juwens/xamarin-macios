//
// Unit tests for AVMetadataObject
//
// Authors:
//	Rolf Bjarne Kvinge <rolf@xamarin.com>
//
// Copyright 2014 Xamarin Inc. All rights reserved.
//

#if !__TVOS__ && !__WATCHOS__

using System;
using System.Drawing;
using System.IO;
using System.Threading;
using Foundation;
using AVFoundation;
using CoreMedia;
using ObjCRuntime;
using NUnit.Framework;

namespace MonoTouchFixtures.AVFoundation {

	[TestFixture]
	[Preserve (AllMembers = true)]
	public class MetadataObjectTest {

		[Test]
		public void Defaults ()
		{
			TestRuntime.AssertXcodeVersion (6, 0);
			TestRuntime.AssertSystemVersion (PlatformName.MacOSX, 10, 10, throwIfOtherPlatform: false);

			using (var obj = new AVMetadataFaceObject ()) {
				Assert.AreEqual ((nint) 0, obj.FaceID, "FaceID");
				Assert.AreEqual (false, obj.HasRollAngle, "HasRollAngle");
				Assert.AreEqual (false, obj.HasYawAngle, "HasYawAngle");
#if !MONOMAC // No Type property for Mac
				Assert.AreEqual (AVMetadataObjectType.Face, obj.Type, "Type");
#endif
				Assert.AreEqual (AVMetadataObject.TypeFace, obj.WeakType, "WeakType");
			}

#if !MONOMAC // iOS only
			using (var obj = new AVMetadataMachineReadableCodeObject ()) {
				Assert.IsNotNull (obj.Corners, "Corners");
				Assert.AreEqual (0, obj.Corners.Length, "Corners");
				Assert.IsNull (obj.StringValue, "StringValue");
				Assert.AreEqual (AVMetadataObjectType.None, obj.Type, "Type");
				Assert.IsNull (obj.WeakType, "WeakType");
		}
#endif
		}
	}
}

#endif // !__TVOS__ && !__WATCHOS__
