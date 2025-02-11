//
// PublicPrivateKeyAuthentication.cs
//
// Authors:
//	TJ Lambert <antlambe@microsoft.com>
//
// Copyright 2021 Microsoft Corporation
//

#if !TVOS && !WATCH

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using CoreGraphics;
using Foundation;
using ObjCRuntime;
using System.Linq;

#nullable enable

namespace AuthenticationServices {

#if !NET
	[NoWatch, NoTV, Mac (12,0), iOS (15,0), MacCatalyst (15,0)]
#else
	[SupportedOSPlatform ("ios15.0")]
	[SupportedOSPlatform ("macos12.0")]
	[SupportedOSPlatform ("maccatalyst15.0")]
	[UnsupportedOSPlatform ("tvos")]
#endif
	public static class PublicPrivateKeyAuthentication {
		[DllImport (Constants.AuthenticationServicesLibrary)]
		static extern /* NSString[] */ IntPtr ASAuthorizationAllSupportedPublicKeyCredentialDescriptorTransports ();

		public static ASAuthorizationSecurityKeyPublicKeyCredentialDescriptorTransport[]? GetAllSupportedPublicKeyCredentialDescriptorTransports () {
			NSString[]? nsStringArray = NSArray.ArrayFromHandle<NSString> (ASAuthorizationAllSupportedPublicKeyCredentialDescriptorTransports ());

			if (nsStringArray == null)
				return null;

			ASAuthorizationSecurityKeyPublicKeyCredentialDescriptorTransport[] asArray = new ASAuthorizationSecurityKeyPublicKeyCredentialDescriptorTransport[nsStringArray.Count ()];
			for (var i = 0; i < nsStringArray.Count (); i++) {
				switch (nsStringArray[i].Description){
					case "usb":
						asArray[i] = ASAuthorizationSecurityKeyPublicKeyCredentialDescriptorTransport.Usb;
						break;
					case "nfc":
						asArray[i] = ASAuthorizationSecurityKeyPublicKeyCredentialDescriptorTransport.Nfc;
						break;
					case "ble":
						asArray[i] = ASAuthorizationSecurityKeyPublicKeyCredentialDescriptorTransport.Bluetooth;
						break;
					default:
						break;
				}
			}
			return asArray;
		}
	}
}

#endif
