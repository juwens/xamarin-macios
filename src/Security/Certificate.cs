// 
// Certificate.cs: Implements the managed SecCertificate wrapper.
//
// Authors: 
//	Miguel de Icaza
//  Sebastien Pouliot  <sebastien@xamarin.com>
//
// Copyright 2010 Novell, Inc
// Copyright 2012-2013 Xamarin Inc.
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

#if !NET
#define NATIVE_APPLE_CERTIFICATE
#else 
using System.Runtime.Versioning;
#endif

using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using ObjCRuntime;
using CoreFoundation;
using Foundation;

namespace Security {

	public partial class SecCertificate : INativeObject, IDisposable {
		internal IntPtr handle;
		
		// invoked by marshallers
		public SecCertificate (IntPtr handle)
			: this (handle, false)
		{
		}
		
		[Preserve (Conditional = true)]
		internal SecCertificate (IntPtr handle, bool owns)
		{
			if (handle == IntPtr.Zero)
				throw new Exception ("Invalid handle");

			this.handle = handle;
			if (!owns)
				CFObject.CFRetain (handle);
		}
#if !COREBUILD
		[DllImport (Constants.SecurityLibrary, EntryPoint="SecCertificateGetTypeID")]
		public extern static nint GetTypeID ();
			
		[DllImport (Constants.SecurityLibrary)]
		extern static IntPtr SecCertificateCreateWithData (IntPtr allocator, IntPtr cfData);

		public SecCertificate (NSData data)
		{
			if (data == null)
				throw new ArgumentNullException ("data");

			Initialize (data);
		}

		public SecCertificate (byte[] data)
		{
			if (data == null)
				throw new ArgumentNullException ("data");

			using (NSData cert = NSData.FromArray (data)) {
				Initialize (cert);
			}
		}

		public SecCertificate (X509Certificate certificate)
		{
			if (certificate == null)
				throw new ArgumentNullException ("certificate");

#if NATIVE_APPLE_CERTIFICATE
			handle = certificate.Impl.GetNativeAppleCertificate ();
			if (handle != IntPtr.Zero) {
				CFObject.CFRetain (handle);
				return;
			}
#endif

			using (NSData cert = NSData.FromArray (certificate.GetRawCertData ())) {
				Initialize (cert);
			}
		}

#if NATIVE_APPLE_CERTIFICATE
		internal SecCertificate (X509CertificateImpl impl)
		{
			handle = impl.GetNativeAppleCertificate ();
			if (handle != IntPtr.Zero) {
				CFObject.CFRetain (handle);
				return;
			}

			using (NSData cert = NSData.FromArray (impl.RawData)) {
				Initialize (cert);
			}
		}
#endif

		public SecCertificate (X509Certificate2 certificate)
		{
			if (certificate == null)
				throw new ArgumentNullException ("certificate");

#if NATIVE_APPLE_CERTIFICATE
			handle = certificate.Impl.GetNativeAppleCertificate ();
			if (handle != IntPtr.Zero) {
				CFObject.CFRetain (handle);
				return;
			}
#endif

			using (NSData cert = NSData.FromArray (certificate.RawData)) {
				Initialize (cert);
			}
		}

		void Initialize (NSData data)
		{
			handle = SecCertificateCreateWithData (IntPtr.Zero, data.Handle);
			if (handle == IntPtr.Zero)
				throw new ArgumentException ("Not a valid DER-encoded X.509 certificate");
		}

		[DllImport (Constants.SecurityLibrary)]
		extern static IntPtr SecCertificateCopySubjectSummary (IntPtr cert);

		public string SubjectSummary {
			get {
				if (handle == IntPtr.Zero)
					throw new ObjectDisposedException ("SecCertificate");
				
				return CFString.FromHandle (SecCertificateCopySubjectSummary (handle), releaseHandle: true);
			}
		}

		[DllImport (Constants.SecurityLibrary)]
		extern static /* CFDataRef */ IntPtr SecCertificateCopyData (/* SecCertificateRef */ IntPtr cert);

		public NSData DerData {
			get {
				if (handle == IntPtr.Zero)
					throw new ObjectDisposedException ("SecCertificate");

				IntPtr data = SecCertificateCopyData (handle);
				if (data == IntPtr.Zero)
					throw new ArgumentException ("Not a valid certificate");
				return new NSData (data, true);
			}
		}

		byte[] GetRawData ()
		{
			using (NSData data = DerData) {
				int len = (int)data.Length;
				byte[] raw = new byte [len];
				Marshal.Copy (data.Bytes, raw, 0, len);
				return raw;
			}
		}

		public X509Certificate ToX509Certificate ()
		{
#if NATIVE_APPLE_CERTIFICATE
			if (handle == IntPtr.Zero)
				throw new ObjectDisposedException ("SecCertificate");

			var impl = new Mono.AppleTls.X509CertificateImplApple (handle, false);
			return new X509Certificate (impl);
#else
			return new X509Certificate (GetRawData ());
#endif
		}

		public X509Certificate2 ToX509Certificate2 ()
		{
			return new X509Certificate2 (GetRawData ());
		}

		internal static bool Equals (SecCertificate first, SecCertificate second)
		{
			/*
			 * This is a little bit expensive, but unfortunately there is no better API to compare two
			 * SecCertificateRef's for equality.
			 */
			if (first == null)
				throw new ArgumentNullException ("first");
			if (second == null)
				throw new ArgumentNullException ("second");
			if (first.Handle == second.Handle)
				return true;

			using (var firstData = first.DerData)
			using (var secondData = second.DerData) {
				if (firstData.Handle == secondData.Handle)
					return true;

				if (firstData.Length != secondData.Length)
					return false;
				nint length = (nint)firstData.Length;
				for (nint i = 0; i < length; i++) {
					if (firstData [i] != secondData [i])
						return false;
				}

				return true;
			}
		}

#if !__MACCATALYST__ // Neither the macOS nor the non-MacOS one works on Mac Catalyst
#if MONOMAC
		/* Only available on OS X v10.7 or later */
		[DllImport (Constants.SecurityLibrary)]
		extern static /* CFDictionaryRef */ IntPtr SecCertificateCopyValues (/* SecCertificateRef */ IntPtr certificate, /* CFArrayRef */ IntPtr keys, /* CFErrorRef _Nullable * */ IntPtr error);

#if !NET
		[Deprecated (PlatformName.MacOSX, 10,14, message: "Use 'GetKey' instead.")]
#else
		[UnsupportedOSPlatform ("macos10.14")]
#if MONOMAC
		[Obsolete ("Starting with macos10.14 use 'GetKey' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#endif
#endif
		public NSData GetPublicKey ()
		{
			if (handle == IntPtr.Zero)
				throw new ObjectDisposedException ("SecCertificate");

			IntPtr result;
			using (var oids = NSArray.FromIntPtrs (new IntPtr[] { SecCertificateOIDs.SubjectPublicKey })) {
				result = SecCertificateCopyValues (handle, oids.Handle, IntPtr.Zero);
				if (result == IntPtr.Zero)
					throw new ArgumentException ("Not a valid certificate");
			}

			using (var dict = new NSDictionary (result, true)) {
				var ptr = dict.LowlevelObjectForKey (SecCertificateOIDs.SubjectPublicKey);
				if (ptr == IntPtr.Zero)
					return null;

				var publicKeyDict = new NSDictionary (ptr, false);
				var dataPtr = publicKeyDict.LowlevelObjectForKey (SecPropertyKey.Value);
				if (dataPtr == IntPtr.Zero)
					return null;

				return new NSData (dataPtr);
			}
		}
#else
#if !NET
		[iOS (10,3)]
		[TV (10,3)]
#else
		[SupportedOSPlatform ("ios10.3")]
		[SupportedOSPlatform ("tvos10.3")]
#endif
#if !NET
		[Deprecated (PlatformName.iOS, 12,0)]
		[Deprecated (PlatformName.TvOS, 12,0)]
		[Deprecated (PlatformName.WatchOS, 5,0)]
#else
		[UnsupportedOSPlatform ("ios12.0")]
		[UnsupportedOSPlatform ("tvos12.0")]
#if IOS
		[Obsolete ("Starting with ios12.0 use 'SecCertificateCopyKey' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif TVOS
		[Obsolete ("Starting with tvos12.0 use 'SecCertificateCopyKey' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#endif
#endif
		[DllImport (Constants.SecurityLibrary)]
		static extern /* __nullable SecKeyRef */ IntPtr SecCertificateCopyPublicKey (IntPtr /* SecCertificateRef */ certificate);

#if !NET
		[iOS (10,3)]
		[TV (10,3)]
#else
		[SupportedOSPlatform ("ios10.3")]
		[SupportedOSPlatform ("tvos10.3")]
#endif
#if !NET
		[Deprecated (PlatformName.iOS, 12,0, message: "Use 'GetKey' instead.")]
		[Deprecated (PlatformName.TvOS, 12,0, message: "Use 'GetKey' instead.")]
		[Deprecated (PlatformName.WatchOS, 5,0, message: "Use 'GetKey' instead.")]
#else
		[UnsupportedOSPlatform ("ios12.0")]
		[UnsupportedOSPlatform ("tvos12.0")]
#if IOS
		[Obsolete ("Starting with ios12.0 use 'GetKey' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif TVOS
		[Obsolete ("Starting with tvos12.0 use 'GetKey' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#endif
#endif
		public SecKey GetPublicKey ()
		{
			IntPtr data = SecCertificateCopyPublicKey (handle);
			return (data == IntPtr.Zero) ? null : new SecKey (data, true);
		}
#endif
#endif // !__MACCATALYST__

#if !NET
		[TV (12,0)][Mac (10,14)][iOS (12,0)][Watch (5,0)]
#else
		[SupportedOSPlatform ("ios12.0")]
		[SupportedOSPlatform ("tvos12.0")]
		[SupportedOSPlatform ("macos10.14")]
#endif
		[DllImport (Constants.SecurityLibrary)]
		static extern IntPtr /* SecKeyRef* */ SecCertificateCopyKey (IntPtr /* SecKeyRef* */ key);

#if !NET
		[TV (12,0)][Mac (10,14)][iOS (12,0)][Watch (5,0)]
#else
		[SupportedOSPlatform ("ios12.0")]
		[SupportedOSPlatform ("tvos12.0")]
		[SupportedOSPlatform ("macos10.14")]
#endif
		public SecKey GetKey ()
		{
			var key = SecCertificateCopyKey (handle);
			return key == IntPtr.Zero ? null : new SecKey (key, true);
		}

#if !NET
		[iOS (10,3)] // [Mac (10,5)]
		[TV (10,3)]
		[Watch (3,3)]
#else
		[SupportedOSPlatform ("ios10.3")]
		[SupportedOSPlatform ("tvos10.3")]
#endif
		[DllImport (Constants.SecurityLibrary)]
		static extern /* OSStatus */ int SecCertificateCopyCommonName (IntPtr /* SecCertificateRef */ certificate, out IntPtr /* CFStringRef * __nonnull CF_RETURNS_RETAINED */ commonName);

#if !NET
		[iOS (10,3)]
		[TV (10,3)]
		[Watch (3,3)]
#else
		[SupportedOSPlatform ("ios10.3")]
		[SupportedOSPlatform ("tvos10.3")]
#endif
		public string GetCommonName ()
		{
			IntPtr cn;
			if (SecCertificateCopyCommonName (handle, out cn) == 0)
				return CFString.FromHandle (cn, releaseHandle: true);
			return null;
		}

#if !NET
		[iOS (10,3)] // [Mac (10,5)]
		[TV (10,3)]
		[Watch (3,3)]
#else
		[SupportedOSPlatform ("ios10.3")]
		[SupportedOSPlatform ("tvos10.3")]
#endif
		[DllImport (Constants.SecurityLibrary)]
		static extern /* OSStatus */ int SecCertificateCopyEmailAddresses (IntPtr /* SecCertificateRef */ certificate, out IntPtr /* CFArrayRef * __nonnull CF_RETURNS_RETAINED */ emailAddresses);

#if !NET
		[iOS (10,3)]
		[TV (10,3)]
		[Watch (3,3)]
#else
		[SupportedOSPlatform ("ios10.3")]
		[SupportedOSPlatform ("tvos10.3")]
#endif
		public string[] GetEmailAddresses ()
		{
			string[] results = null;
			IntPtr emails;
			if (SecCertificateCopyEmailAddresses (handle, out emails) == 0) {
				results = CFArray.StringArrayFromHandle (emails);
				if (emails != IntPtr.Zero)
					CFObject.CFRelease (emails);
			}
			return results;
		}

#if !NET
		[iOS (10,3)]
		[Mac (10,12,4)]
		[TV (10,3)]
		[Watch (3,3)]
#else
		[SupportedOSPlatform ("ios10.3")]
		[SupportedOSPlatform ("tvos10.3")]
#endif
		[DllImport (Constants.SecurityLibrary)]
		static extern /* __nullable CFDataRef */ IntPtr SecCertificateCopyNormalizedIssuerSequence (IntPtr /* SecCertificateRef */ certificate);

#if !NET
		[iOS (10,3)]
		[Mac (10,12,4)]
		[TV (10,3)]
		[Watch (3,3)]
#else
		[SupportedOSPlatform ("ios10.3")]
		[SupportedOSPlatform ("tvos10.3")]
#endif
		public NSData GetNormalizedIssuerSequence ()
		{
			IntPtr data = SecCertificateCopyNormalizedIssuerSequence (handle);
			return (data == IntPtr.Zero) ? null : new NSData (data, true);
		}

#if !NET
		[iOS (10,3)]
		[Mac (10,12,4)]
		[TV (10,3)]
		[Watch (3,3)]
#else
		[SupportedOSPlatform ("ios10.3")]
		[SupportedOSPlatform ("tvos10.3")]
#endif
		[DllImport (Constants.SecurityLibrary)]
		static extern /* __nullable CFDataRef */ IntPtr SecCertificateCopyNormalizedSubjectSequence (IntPtr /* SecCertificateRef */ certificate);

#if !NET
		[iOS (10,3)]
		[Mac (10,12,4)]
		[TV (10,3)]
		[Watch (3,3)]
#else
		[SupportedOSPlatform ("ios10.3")]
		[SupportedOSPlatform ("tvos10.3")]
#endif
		public NSData GetNormalizedSubjectSequence ()
		{
			IntPtr data = SecCertificateCopyNormalizedSubjectSequence (handle);
			return (data == IntPtr.Zero) ? null : new NSData (data, true);
		}

#if MONOMAC
		[DllImport (Constants.SecurityLibrary)]
#if !NET
		[Deprecated (PlatformName.MacOSX, 10,13, message: "Use 'GetSerialNumber' instead.")]
#else
		[UnsupportedOSPlatform ("macos.10.13")]
#if MONOMAC
		[Obsolete ("Starting with macos.10.13 use 'GetSerialNumber' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#endif
#endif
		static extern /* __nullable CFDataRef */ IntPtr SecCertificateCopySerialNumber (IntPtr /* SecCertificateRef */ certificate, IntPtr /* CFErrorRef * */ error);
#else // !MONOMAC
#if !NET
		[iOS (10,3)]
#else
		[SupportedOSPlatform ("ios10.3")]
#endif
#if !NET
		[Deprecated (PlatformName.iOS, 11,0, message: "Use 'GetSerialNumber' instead.")]
		[Deprecated (PlatformName.MacOSX, 10,13, message: "Use 'GetSerialNumber' instead.")]
		[Deprecated (PlatformName.WatchOS, 4,0, message: "Use 'GetSerialNumber' instead.")]
		[Deprecated (PlatformName.TvOS, 11,0, message: "Use 'GetSerialNumber' instead.")]
#else
		[UnsupportedOSPlatform ("ios11.0")]
		[UnsupportedOSPlatform ("tvos11.0")]
		[UnsupportedOSPlatform ("macos10.13")]
#if IOS
		[Obsolete ("Starting with ios11.0 use 'GetSerialNumber' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif TVOS
		[Obsolete ("Starting with tvos11.0 use 'GetSerialNumber' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif MONOMAC
		[Obsolete ("Starting with macos10.13 use 'GetSerialNumber' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#endif
#endif
		[DllImport (Constants.SecurityLibrary)]
		static extern /* __nullable CFDataRef */ IntPtr SecCertificateCopySerialNumber (IntPtr /* SecCertificateRef */ certificate);
#endif
#if !NET
		[iOS (10,3)]
#else
		[SupportedOSPlatform ("ios10.3")]
#endif
#if !NET
		[Deprecated (PlatformName.iOS, 11,0, message: "Use 'GetSerialNumber(out NSError)' instead.")]
		[Deprecated (PlatformName.MacOSX, 10,13, message: "Use 'GetSerialNumber(out NSError)' instead.")]
		[Deprecated (PlatformName.WatchOS, 4,0, message: "Use 'GetSerialNumber(out NSError)' instead.")]
		[Deprecated (PlatformName.TvOS, 11,0, message: "Use 'GetSerialNumber(out NSError)' instead.")]
#else
		[UnsupportedOSPlatform ("ios11.0")]
		[UnsupportedOSPlatform ("tvos11.0")]
		[UnsupportedOSPlatform ("macos10.13")]
#if IOS
		[Obsolete ("Starting with ios11.0 use 'GetSerialNumber(out NSError)' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif TVOS
		[Obsolete ("Starting with tvos11.0 use 'GetSerialNumber(out NSError)' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif MONOMAC
		[Obsolete ("Starting with macos10.13 use 'GetSerialNumber(out NSError)' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#endif
#endif
		public NSData GetSerialNumber ()
		{
#if MONOMAC
			IntPtr data = SecCertificateCopySerialNumber (handle, IntPtr.Zero);
#else
			IntPtr data = SecCertificateCopySerialNumber (handle);
#endif
			return (data == IntPtr.Zero) ? null : new NSData (data, true);
		}

#if !NET
		[iOS (11,0)][TV (11,0)][Watch (4,0)][Mac (10,13)]
#else
		[SupportedOSPlatform ("ios11.0")]
		[SupportedOSPlatform ("tvos11.0")]
#endif
		[DllImport (Constants.SecurityLibrary)]
		static extern /* __nullable CFDataRef */ IntPtr SecCertificateCopySerialNumberData (IntPtr /* SecCertificateRef */ certificate, ref IntPtr /* CFErrorRef * */ error);

#if !NET
		[iOS (11,0)][TV (11,0)][Watch (4,0)][Mac (10,13)]
#else
		[SupportedOSPlatform ("ios11.0")]
		[SupportedOSPlatform ("tvos11.0")]
#endif
		public NSData GetSerialNumber (out NSError error)
		{
			IntPtr err = IntPtr.Zero;
			IntPtr data = SecCertificateCopySerialNumberData (handle, ref err);
			error = Runtime.GetNSObject<NSError> (err);
			return (data == IntPtr.Zero) ? null : new NSData (data, true);
		}

#endif // COREBUILD
		 
		~SecCertificate ()
		{
			Dispose (false);
		}

		public IntPtr Handle {
			get {
				return handle;
			}
		}

		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
		}

		protected virtual void Dispose (bool disposing)
		{
			if (handle != IntPtr.Zero){
				CFObject.CFRelease (handle);
				handle = IntPtr.Zero;
			}
		}
	}

	public partial class SecIdentity : INativeObject, IDisposable {
		internal IntPtr handle;
		
		// invoked by marshallers
		public SecIdentity (IntPtr handle)
			: this (handle, false)
		{
		}
		
		[Preserve (Conditional = true)]
		internal SecIdentity (IntPtr handle, bool owns)
		{
			this.handle = handle;
			if (!owns)
				CFObject.CFRetain (handle);
		}

#if !COREBUILD
		[DllImport (Constants.SecurityLibrary, EntryPoint="SecIdentityGetTypeID")]
		public extern static nint GetTypeID ();

		[DllImport (Constants.SecurityLibrary)]
		extern static /* OSStatus */ SecStatusCode SecIdentityCopyCertificate (/* SecIdentityRef */ IntPtr identityRef,  /* SecCertificateRef* */ out IntPtr certificateRef);

		public SecCertificate Certificate {
			get {
				if (handle == IntPtr.Zero)
					throw new ObjectDisposedException ("SecIdentity");
				IntPtr cert;
				SecStatusCode result = SecIdentityCopyCertificate (handle, out cert);
				if (result != SecStatusCode.Success)
					throw new InvalidOperationException (result.ToString ());
				return new SecCertificate (cert, true);
			}
		}

		public static SecIdentity Import (byte[] data, string password)
		{
			if (data == null)
				throw new ArgumentNullException ("data");
			if (string.IsNullOrEmpty (password)) // SecPKCS12Import() doesn't allow empty passwords.
				throw new ArgumentException ("password");
			using (var pwstring = new NSString (password))
			using (var options = NSDictionary.FromObjectAndKey (pwstring, SecImportExport.Passphrase)) {
				NSDictionary[] array;
				SecStatusCode result = SecImportExport.ImportPkcs12 (data, options, out array);
				if (result != SecStatusCode.Success)
					throw new InvalidOperationException (result.ToString ());

				return new SecIdentity (array [0].LowlevelObjectForKey (SecImportExport.Identity.Handle));
			}
		}

		public static SecIdentity Import (X509Certificate2 certificate)
		{
			if (certificate == null)
				throw new ArgumentNullException ("certificate");
			if (!certificate.HasPrivateKey)
				throw new InvalidOperationException ("Need X509Certificate2 with a private key.");

			/*
			 * SecPSK12Import does not allow any empty passwords, so let's generate
			 * a semi-random one here.
			 */
			var password = Guid.NewGuid ().ToString ();
			var pkcs12 = certificate.Export (X509ContentType.Pfx, password);
			return Import (pkcs12, password);
		}
#endif

		~SecIdentity ()
		{
			Dispose (false);
		}

		public IntPtr Handle {
			get {
				return handle;
			}
		}

		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
		}

		protected virtual void Dispose (bool disposing)
		{
			if (handle != IntPtr.Zero){
				CFObject.CFRelease (handle);
				handle = IntPtr.Zero;
			}
		}
	}

	public partial class SecKey : INativeObject, IDisposable {
		internal IntPtr handle;
		
		// invoked by marshallers
		public SecKey (IntPtr handle)
			: this (handle, false)
		{
		}
		
		[Preserve (Conditional = true)]
		public SecKey (IntPtr handle, bool owns)
		{
			this.handle = handle;
			if (!owns)
				CFObject.CFRetain (handle);
		}

#if !COREBUILD
		[DllImport (Constants.SecurityLibrary, EntryPoint="SecKeyGetTypeID")]
		public extern static nint GetTypeID ();
		
#if !NET
		[Deprecated (PlatformName.MacOSX, 12, 0, message: "Use 'SecKeyCreateRandomKey' instead.")]
		[Deprecated (PlatformName.iOS, 15, 0, message: "Use 'SecKeyCreateRandomKey' instead.")]
		[Deprecated (PlatformName.MacCatalyst, 15, 0, message: "Use 'SecKeyCreateRandomKey' instead.")]
		[Deprecated (PlatformName.TvOS, 15, 0, message: "Use 'SecKeyCreateRandomKey' instead.")] 
		[Deprecated (PlatformName.WatchOS, 8, 0, message:  "Use 'SecKeyCreateRandomKey' instead.")]
#else
		[UnsupportedOSPlatform ("macos12.0")]
		[UnsupportedOSPlatform ("ios15.0")]
		[UnsupportedOSPlatform ("tvos15.0")]
		[UnsupportedOSPlatform ("maccatalyst15.0")]
#if __MACCATALYST__
		[Obsolete ("Starting with maccatalyst15.0 use 'SecKeyCreateRandomKey' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif IOS
		[Obsolete ("Starting with ios15.0 use 'SecKeyCreateRandomKey' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif TVOS
		[Obsolete ("Starting with tvos15.0 use 'SecKeyCreateRandomKey' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif MONOMAC
		[Obsolete ("Starting with macos12.0 use 'SecKeyCreateRandomKey' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#endif
#endif
		[DllImport (Constants.SecurityLibrary)]
		extern static SecStatusCode SecKeyGeneratePair (IntPtr dictHandle, out IntPtr pubKey, out IntPtr privKey);

		// TODO: pull all the TypeRefs needed for the NSDictionary
		
#if !NET
		[Deprecated (PlatformName.MacOSX, 12, 0, message: "Use 'CreateRandomKey' instead.")]
		[Deprecated (PlatformName.iOS, 15, 0, message: "Use 'CreateRandomKey' instead.")]
		[Deprecated (PlatformName.MacCatalyst, 15, 0, message: "Use 'CreateRandomKey' instead.")]
		[Deprecated (PlatformName.TvOS, 15, 0, message: "Use 'CreateRandomKey' instead.")] 
		[Deprecated (PlatformName.WatchOS, 8, 0, message:  "Use 'CreateRandomKey' instead.")]
#else
		[UnsupportedOSPlatform ("macos12.0")]
		[UnsupportedOSPlatform ("ios15.0")]
		[UnsupportedOSPlatform ("tvos15.0")]
		[UnsupportedOSPlatform ("maccatalyst15.0")]
#if __MACCATALYST__
		[Obsolete ("Starting with maccatalyst15.0 use 'CreateRandomKey' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif IOS
		[Obsolete ("Starting with ios15.0 use 'CreateRandomKey' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif TVOS
		[Obsolete ("Starting with tvos15.0 use 'CreateRandomKey' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif MONOMAC
		[Obsolete ("Starting with macos12.0 use 'CreateRandomKey' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#endif
#endif
		public static SecStatusCode GenerateKeyPair (NSDictionary parameters, out SecKey publicKey, out SecKey privateKey)
		{
			if (parameters == null)
				throw new ArgumentNullException ("parameters");

			IntPtr pub, priv;
			
			var res = SecKeyGeneratePair (parameters.Handle, out pub, out priv);
			if (res == SecStatusCode.Success){
				publicKey = new SecKey (pub, true);
				privateKey = new SecKey (priv, true);
			} else
				publicKey = privateKey = null;
			return res;
		}

		[Advice ("On iOS this method applies the attributes to both public and private key. To apply different attributes to each key, use 'GenerateKeyPair (SecKeyType, int, SecPublicPrivateKeyAttrs, SecPublicPrivateKeyAttrs, out SecKey, out SecKey)' instead.")]
		public static SecStatusCode GenerateKeyPair (SecKeyType type, int keySizeInBits, SecPublicPrivateKeyAttrs publicAndPrivateKeyAttrs, out SecKey publicKey, out SecKey privateKey)
		{
#if !MONOMAC
			// iOS (+friends) need to pass the strong dictionary for public and private key attributes to specific keys
			// instead of merging them with other attributes.
			return GenerateKeyPair (type, keySizeInBits, publicAndPrivateKeyAttrs, publicAndPrivateKeyAttrs, out publicKey, out privateKey);
#else
			if (type == SecKeyType.Invalid)
				throw new ArgumentException ("invalid 'SecKeyType'", nameof (type));

			NSMutableDictionary dic;
			if (publicAndPrivateKeyAttrs != null)
				dic = new NSMutableDictionary (publicAndPrivateKeyAttrs.GetDictionary ());
			else
				dic = new NSMutableDictionary ();
			dic.LowlevelSetObject (type.GetConstant (), SecAttributeKey.Type);
			dic.LowlevelSetObject (new NSNumber (keySizeInBits), SecKeyGenerationAttributeKeys.KeySizeInBitsKey.Handle);
			return GenerateKeyPair (dic, out publicKey, out privateKey);
#endif
		}
#if !MONOMAC
		public static SecStatusCode GenerateKeyPair (SecKeyType type, int keySizeInBits, SecPublicPrivateKeyAttrs publicKeyAttrs, SecPublicPrivateKeyAttrs privateKeyAttrs, out SecKey publicKey, out SecKey privateKey)
		{
			if (type == SecKeyType.Invalid)
				throw new ArgumentException ("invalid 'SecKeyType'", nameof (type));

			using (var dic = new NSMutableDictionary ()) {
				dic.LowlevelSetObject (type.GetConstant (), SecAttributeKey.Type);
				using (var ksib = new NSNumber (keySizeInBits)) {
					dic.LowlevelSetObject (ksib, SecKeyGenerationAttributeKeys.KeySizeInBitsKey.Handle);
					if (publicKeyAttrs != null)
						dic.LowlevelSetObject (publicKeyAttrs.GetDictionary (), SecKeyGenerationAttributeKeys.PublicKeyAttrsKey.Handle);
					if (privateKeyAttrs != null)
						dic.LowlevelSetObject (privateKeyAttrs.GetDictionary (), SecKeyGenerationAttributeKeys.PrivateKeyAttrsKey.Handle);
					return GenerateKeyPair (dic, out publicKey, out privateKey);
				}
			}
		}
#endif
			
		[DllImport (Constants.SecurityLibrary)]
		extern static /* size_t */ nint SecKeyGetBlockSize (IntPtr handle);

		public int BlockSize {
			get {
				if (handle == IntPtr.Zero)
					throw new ObjectDisposedException ("SecKey");
				
				return (int) SecKeyGetBlockSize (handle);
			}
		}

#if !NET
		[Deprecated (PlatformName.iOS, 15, 0, message: "Use 'SecKeyCreateSignature' instead.")]
		[Deprecated (PlatformName.MacCatalyst, 15, 0, message: "Use 'SecKeyCreateSignature' instead.")]
		[Deprecated (PlatformName.TvOS, 15, 0, message: "Use 'SecKeyCreateSignature' instead.")] 
		[Deprecated (PlatformName.WatchOS, 8, 0, message: "Use 'SecKeyCreateSignature' instead.")]
#else
		[UnsupportedOSPlatform ("ios15.0")]
		[UnsupportedOSPlatform ("tvos15.0")]
		[UnsupportedOSPlatform ("maccatalyst15.0")]
#if __MACCATALYST__
		[Obsolete ("Starting with maccatalyst15.0 use 'SecKeyCreateSignature' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif IOS
		[Obsolete ("Starting with ios15.0 use 'SecKeyCreateSignature' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif TVOS
		[Obsolete ("Starting with tvos15.0 use 'SecKeyCreateSignature' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#endif
#endif
		[DllImport (Constants.SecurityLibrary)]
		extern static SecStatusCode SecKeyRawSign (IntPtr handle, SecPadding padding, IntPtr dataToSign, nint dataToSignLen, IntPtr sig, ref nint sigLen);

#if !NET
		[Deprecated (PlatformName.iOS, 15, 0, message: "Use 'CreateSignature' instead.")]
		[Deprecated (PlatformName.MacCatalyst, 15, 0, message: "Use 'CreateSignature' instead.")]
		[Deprecated (PlatformName.TvOS, 15, 0, message: "Use 'CreateSignature' instead.")] 
		[Deprecated (PlatformName.WatchOS, 8, 0, message: "Use 'CreateSignature' instead.")]
#else
		[UnsupportedOSPlatform ("ios15.0")]
		[UnsupportedOSPlatform ("tvos15.0")]
		[UnsupportedOSPlatform ("maccatalyst15.0")]
#if __MACCATALYST__
		[Obsolete ("Starting with maccatalyst15.0 use 'CreateSignature' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif IOS
		[Obsolete ("Starting with ios15.0 use 'CreateSignature' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif TVOS
		[Obsolete ("Starting with tvos15.0 use 'CreateSignature' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#endif
#endif
		public SecStatusCode RawSign (SecPadding padding, IntPtr dataToSign, int dataToSignLen, out byte [] result)
		{
			if (handle == IntPtr.Zero)
				throw new ObjectDisposedException ("SecKey");
			if (dataToSign == IntPtr.Zero)
				throw new ArgumentException ("dataToSign");

			return _RawSign (padding, dataToSign, dataToSignLen, out result);
		}

		public unsafe SecStatusCode RawSign (SecPadding padding, byte [] dataToSign, out byte [] result)
		{
			if (handle == IntPtr.Zero)
				throw new ObjectDisposedException ("SecKey");
			if (dataToSign == null)
				throw new ArgumentNullException ("dataToSign");

			fixed (byte *bp = dataToSign)
				return _RawSign (padding, (IntPtr) bp, dataToSign.Length, out result);
		}

		unsafe SecStatusCode _RawSign (SecPadding padding, IntPtr dataToSign, int dataToSignLen, out byte [] result)
		{
			SecStatusCode status;
			nint len = 1024;
			result = new byte [len];
			fixed (byte *p = result) {
				status = SecKeyRawSign (handle, padding, dataToSign, dataToSignLen, (IntPtr) p, ref len);
				Array.Resize (ref result, (int) len);
			}
			return status;
		}
		
#if !NET
		[Deprecated (PlatformName.iOS, 15, 0, message: "Use 'SecKeyVerifySignature' instead.")]
		[Deprecated (PlatformName.MacCatalyst, 15, 0, message: "Use 'SecKeyVerifySignature' instead.")]
		[Deprecated (PlatformName.TvOS, 15, 0, message: "Use 'SecKeyVerifySignature' instead.")] 
		[Deprecated (PlatformName.WatchOS, 8, 0, message: "Use 'SecKeyVerifySignature' instead.")]
#else
		[UnsupportedOSPlatform ("ios15.0")]
		[UnsupportedOSPlatform ("tvos15.0")]
		[UnsupportedOSPlatform ("maccatalyst15.0")]
#if __MACCATALYST__
		[Obsolete ("Starting with maccatalyst15.0 use 'SecKeyVerifySignature' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif IOS
		[Obsolete ("Starting with ios15.0 use 'SecKeyVerifySignature' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif TVOS
		[Obsolete ("Starting with tvos15.0 use 'SecKeyVerifySignature' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#endif
#endif
		[DllImport (Constants.SecurityLibrary)]
		extern static SecStatusCode SecKeyRawVerify (IntPtr handle, SecPadding padding, IntPtr signedData, nint signedLen, IntPtr sign, nint signLen);

#if !NET
		[Deprecated (PlatformName.iOS, 15, 0, message: "Use 'VerifySignature' instead.")]
		[Deprecated (PlatformName.MacCatalyst, 15, 0, message: "Use 'VerifySignature' instead.")]
		[Deprecated (PlatformName.TvOS, 15, 0, message: "Use 'VerifySignature' instead.")] 
		[Deprecated (PlatformName.WatchOS, 8, 0, message: "Use 'VerifySignature' instead.")]
#else
		[UnsupportedOSPlatform ("ios15.0")]
		[UnsupportedOSPlatform ("tvos15.0")]
		[UnsupportedOSPlatform ("maccatalyst15.0")]
#if __MACCATALYST__
		[Obsolete ("Starting with maccatalyst15.0 use 'VerifySignature' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif IOS
		[Obsolete ("Starting with ios15.0 use 'VerifySignature' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif TVOS
		[Obsolete ("Starting with tvos15.0 use 'VerifySignature' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#endif
#endif
		public unsafe SecStatusCode RawVerify (SecPadding padding, IntPtr signedData, int signedDataLen, IntPtr signature, int signatureLen)
		{
			if (handle == IntPtr.Zero)
				throw new ObjectDisposedException ("SecKey");

			return SecKeyRawVerify (handle, padding, signedData, (nint) signedDataLen, signature, (nint) signatureLen);
		}

		public SecStatusCode RawVerify (SecPadding padding, byte [] signedData, byte [] signature)
		{
			if (handle == IntPtr.Zero)
				throw new ObjectDisposedException ("SecKey");

			if (signature == null)
				throw new ArgumentNullException ("signature");
			if (signedData == null)
				throw new ArgumentNullException ("signedData");
			unsafe {
				// SecKeyRawVerify will try to read from the signedData/signature pointers even if
				// the corresponding length is 0, which may crash (happens in Xcode 11 beta 1)
				// so if length is 0, then pass an array with one element.
				var signatureArray = signature.Length == 0 ? new byte [] { 0 } : signature;
				var signedDataArray = signedData.Length == 0 ? new byte [] { 0 } : signedData;
				fixed (byte *sp = signatureArray)
				fixed (byte *dp = signedDataArray) {
					return SecKeyRawVerify (handle, padding, (IntPtr) dp, (nint) signedData.Length, (IntPtr) sp, (nint) signature.Length);
				}
			}
		}
		
#if !NET
		[Deprecated (PlatformName.iOS, 15, 0, message: "Use 'SecKeyCreateEncryptedData' instead.")]
		[Deprecated (PlatformName.TvOS, 15, 0, message: "Use 'SecKeyCreateEncryptedData' instead.")]
		[Deprecated (PlatformName.MacCatalyst, 15, 0, message: "Use 'SecKeyCreateEncryptedData' instead.")]
		[Deprecated (PlatformName.WatchOS, 8, 0, message: "Use 'SecKeyCreateEncryptedData' instead.")]
#else
		[UnsupportedOSPlatform ("ios15.0")]
		[UnsupportedOSPlatform ("tvos15.0")]
		[UnsupportedOSPlatform ("maccatalyst15.0")]
#if __MACCATALYST__
		[Obsolete ("Starting with maccatalyst15.0 use 'SecKeyCreateEncryptedData' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif IOS
		[Obsolete ("Starting with ios15.0 use 'SecKeyCreateEncryptedData' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif TVOS
		[Obsolete ("Starting with tvos15.0 use 'SecKeyCreateEncryptedData' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#endif
#endif
		[DllImport (Constants.SecurityLibrary)]
		extern static SecStatusCode SecKeyEncrypt (IntPtr handle, SecPadding padding, IntPtr plainText, nint plainTextLen, IntPtr cipherText, ref nint cipherTextLengh);

#if !NET
		[Deprecated (PlatformName.iOS, 15, 0, message: "Use 'CreateEncryptedData' instead.")]
		[Deprecated (PlatformName.TvOS, 15, 0, message: "Use 'CreateEncryptedData' instead.")]
		[Deprecated (PlatformName.MacCatalyst, 15, 0, message: "Use 'CreateEncryptedData' instead.")]
		[Deprecated (PlatformName.WatchOS, 8, 0, message: "Use 'CreateEncryptedData' instead.")]
#else
		[UnsupportedOSPlatform ("ios15.0")]
		[UnsupportedOSPlatform ("tvos15.0")]
		[UnsupportedOSPlatform ("maccatalyst15.0")]
#if __MACCATALYST__
		[Obsolete ("Starting with maccatalyst15.0 use 'CreateEncryptedData' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif IOS
		[Obsolete ("Starting with ios15.0 use 'CreateEncryptedData' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif TVOS
		[Obsolete ("Starting with tvos15.0 use 'CreateEncryptedData' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#endif
#endif
		public unsafe SecStatusCode Encrypt (SecPadding padding, IntPtr plainText, nint plainTextLen, IntPtr cipherText, ref nint cipherTextLen)
		{
			if (handle == IntPtr.Zero)
				throw new ObjectDisposedException ("SecKey");

			return SecKeyEncrypt (handle, padding, plainText, plainTextLen, cipherText, ref cipherTextLen);
		}

		public SecStatusCode Encrypt (SecPadding padding, byte [] plainText, byte [] cipherText)
		{
			if (handle == IntPtr.Zero)
				throw new ObjectDisposedException ("SecKey");

			if (cipherText == null)
				throw new ArgumentNullException ("cipherText");
			if (plainText == null)
				throw new ArgumentNullException ("plainText");
			unsafe {
				fixed (byte *cp = cipherText)
				fixed (byte *pp = plainText) {
					nint len = (nint) cipherText.Length;
					return SecKeyEncrypt (handle, padding, (IntPtr) pp, (nint) plainText.Length, (IntPtr) cp, ref len);
				}
			}
		}

		public SecStatusCode Encrypt (SecPadding padding, byte [] plainText, out byte [] cipherText)
		{
			cipherText = new byte [BlockSize];
			return Encrypt (padding, plainText, cipherText);
		}

#if !NET
		[Deprecated (PlatformName.iOS, 15, 0, message: "Use 'SecKeyCreateDecryptedData' instead.")]
		[Deprecated (PlatformName.TvOS, 15, 0, message: "Use 'SecKeyCreateDecryptedData' instead.")]
		[Deprecated (PlatformName.MacCatalyst, 15, 0, message: "Use 'SecKeyCreateDecryptedData' instead.")]
		[Deprecated (PlatformName.WatchOS, 8, 0, message: "Use 'SecKeyCreateDecryptedData' instead.")]
#else
		[UnsupportedOSPlatform ("ios15.0")]
		[UnsupportedOSPlatform ("tvos15.0")]
		[UnsupportedOSPlatform ("maccatalyst15.0")]
#if __MACCATALYST__
		[Obsolete ("Starting with maccatalyst15.0 use 'SecKeyCreateDecryptedData' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif IOS
		[Obsolete ("Starting with ios15.0 use 'SecKeyCreateDecryptedData' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif TVOS
		[Obsolete ("Starting with tvos15.0 use 'SecKeyCreateDecryptedData' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#endif
#endif
		[DllImport (Constants.SecurityLibrary)]
		extern static SecStatusCode SecKeyDecrypt (IntPtr handle, SecPadding padding, IntPtr cipherTextLen, nint cipherLen, IntPtr plainText, ref nint plainTextLen);

#if !NET
		[Deprecated (PlatformName.iOS, 15, 0, message: "Use 'CreateDecryptedData' instead.")]
		[Deprecated (PlatformName.TvOS, 15, 0, message: "Use 'CreateDecryptedData' instead.")]
		[Deprecated (PlatformName.MacCatalyst, 15, 0, message: "Use 'CreateDecryptedData' instead.")]
		[Deprecated (PlatformName.WatchOS, 8, 0, message: "Use 'CreateDecryptedData' instead.")]
#else
		[UnsupportedOSPlatform ("ios15.0")]
		[UnsupportedOSPlatform ("tvos15.0")]
		[UnsupportedOSPlatform ("maccatalyst15.0")]
#if __MACCATALYST__
		[Obsolete ("Starting with maccatalyst15.0 use 'CreateDecryptedData' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif IOS
		[Obsolete ("Starting with ios15.0 use 'CreateDecryptedData' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#elif TVOS
		[Obsolete ("Starting with tvos15.0 use 'CreateDecryptedData' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#endif
#endif
		public unsafe SecStatusCode Decrypt (SecPadding padding, IntPtr cipherText, nint cipherTextLen, IntPtr plainText, ref nint plainTextLen)
		{
			if (handle == IntPtr.Zero)
				throw new ObjectDisposedException ("SecKey");

			return SecKeyDecrypt (handle, padding, cipherText, cipherTextLen, plainText, ref plainTextLen);
		}

		SecStatusCode _Decrypt (SecPadding padding, byte [] cipherText, ref byte [] plainText)
		{
			if (handle == IntPtr.Zero)
				throw new ObjectDisposedException ("SecKey");

			if (cipherText == null)
				throw new ArgumentNullException ("cipherText");
		
			unsafe {
				fixed (byte *cp = cipherText) {
					if (plainText == null)
						plainText = new byte [cipherText.Length];
					nint len = plainText.Length;
					SecStatusCode status;
					fixed (byte *pp = plainText)
						status = SecKeyDecrypt (handle, padding, (IntPtr)cp, (nint)cipherText.Length, (IntPtr)pp, ref len);
					if (len < plainText.Length)
						Array.Resize<byte> (ref plainText, (int) len);
					return status;
				}
			}
		}

		public SecStatusCode Decrypt (SecPadding padding, byte [] cipherText, out byte [] plainText)
		{
			plainText = null;
			return _Decrypt (padding, cipherText, ref plainText);
		}

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		[DllImport (Constants.SecurityLibrary)]
		static extern IntPtr /* SecKeyRef _Nullable */ SecKeyCreateRandomKey (IntPtr /* CFDictionaryRef* */ parameters, out IntPtr /* CFErrorRef** */ error);

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		static public SecKey CreateRandomKey (NSDictionary parameters, out NSError error)
		{
			if (parameters == null)
				throw new ArgumentNullException (nameof (parameters));

			IntPtr err;
			var key = SecKeyCreateRandomKey (parameters.Handle, out err);
			error = err == IntPtr.Zero ? null : new NSError (err);
			return key == IntPtr.Zero ? null : new SecKey (key, true);
		}

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		static public SecKey CreateRandomKey (SecKeyType keyType, int keySizeInBits, NSDictionary parameters, out NSError error)
		{
			using (var ks = new NSNumber (keySizeInBits))
			using (var md = parameters == null ? new NSMutableDictionary () : new NSMutableDictionary (parameters)) {
				md.LowlevelSetObject (keyType.GetConstant (), SecKeyGenerationAttributeKeys.KeyTypeKey.Handle);
				md.LowlevelSetObject (ks, SecKeyGenerationAttributeKeys.KeySizeInBitsKey.Handle);
				return CreateRandomKey (md, out error);
			}
		}

#if !NET
		[Watch (3, 0)][TV (10, 0)][Mac (10, 12)][iOS (10, 0)]
#endif
		static public SecKey CreateRandomKey (SecKeyGenerationParameters parameters, out NSError error)
		{
			if (parameters == null)
				throw new ArgumentNullException (nameof (parameters));
			if (parameters.KeyType == SecKeyType.Invalid)
				throw new ArgumentException ("invalid 'SecKeyType'", "SecKeyGeneration.KeyType");

			using (var dictionary = parameters.GetDictionary ()) {
				return CreateRandomKey (dictionary, out error);
			}
		}

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		[DllImport (Constants.SecurityLibrary)]
		static extern IntPtr /* SecKeyRef _Nullable */ SecKeyCreateWithData (IntPtr /* CFDataRef* */ keyData, IntPtr /* CFDictionaryRef* */ attributes, out IntPtr /* CFErrorRef** */ error);

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		static public SecKey Create (NSData keyData, NSDictionary parameters, out NSError error)
		{
			if (keyData == null)
				throw new ArgumentNullException (nameof (keyData));
			if (parameters == null)
				throw new ArgumentNullException (nameof (parameters));

			IntPtr err;
			var key = SecKeyCreateWithData (keyData.Handle, parameters.Handle, out err);
			error = err == IntPtr.Zero ? null : new NSError (err);
			return key == IntPtr.Zero ? null : new SecKey (key, true);
		}

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		static public SecKey Create (NSData keyData, SecKeyType keyType, SecKeyClass keyClass, int keySizeInBits, NSDictionary parameters, out NSError error)
		{
			using (var ks = new NSNumber (keySizeInBits))
			using (var md = parameters == null ? new NSMutableDictionary () : new NSMutableDictionary (parameters)) {
				md.LowlevelSetObject (keyType.GetConstant (), SecKeyGenerationAttributeKeys.KeyTypeKey.Handle);
				md.LowlevelSetObject (keyClass.GetConstant (), SecAttributeKey.KeyClass);
				md.LowlevelSetObject (ks, SecKeyGenerationAttributeKeys.KeySizeInBitsKey.Handle);
				return Create (keyData, md, out error);
			}
		}

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		[DllImport (Constants.SecurityLibrary)]
		static extern IntPtr /* CFDataRef _Nullable */ SecKeyCopyExternalRepresentation (IntPtr /* SecKeyRef* */ key, out IntPtr /* CFErrorRef** */ error);

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		public NSData GetExternalRepresentation (out NSError error)
		{
			IntPtr err;
			var data = SecKeyCopyExternalRepresentation (handle, out err);
			error = err == IntPtr.Zero ? null : new NSError (err);
			return Runtime.GetNSObject<NSData> (data, true);
		}

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		public NSData GetExternalRepresentation ()
		{
			IntPtr err;
			var data = SecKeyCopyExternalRepresentation (handle, out err);
			return Runtime.GetNSObject<NSData> (data, true);
		}

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		[DllImport (Constants.SecurityLibrary)]
		static extern IntPtr /* CFDictionaryRef _Nullable */ SecKeyCopyAttributes (IntPtr /* SecKeyRef* */ key);

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		public NSDictionary GetAttributes ()
		{
			var dict = SecKeyCopyAttributes (handle);
			return Runtime.GetNSObject<NSDictionary> (dict, true);
		}

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		[DllImport (Constants.SecurityLibrary)]
		static extern IntPtr /* SecKeyRef* */ SecKeyCopyPublicKey (IntPtr /* SecKeyRef* */ key);

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		public SecKey GetPublicKey ()
		{
			var key = SecKeyCopyPublicKey (handle);
			return key == IntPtr.Zero ? null : new SecKey (key, true);
		}

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		[DllImport (Constants.SecurityLibrary)]
		[return: MarshalAs (UnmanagedType.U1)]
		static extern bool /* Boolean */ SecKeyIsAlgorithmSupported (IntPtr /* SecKeyRef* */ key, /* SecKeyOperationType */ nint operation, IntPtr /* SecKeyAlgorithm* */ algorithm);

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		public bool IsAlgorithmSupported (SecKeyOperationType operation, SecKeyAlgorithm algorithm)
		{
			return SecKeyIsAlgorithmSupported (handle, (int) operation, algorithm.GetConstant ().Handle);
		}

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		[DllImport (Constants.SecurityLibrary)]
		static extern /* CFDataRef _Nullable */ IntPtr SecKeyCreateSignature (/* SecKeyRef */ IntPtr key, /* SecKeyAlgorithm */ IntPtr algorithm, /* CFDataRef */ IntPtr dataToSign, /* CFErrorRef* */ out IntPtr error);

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		public NSData CreateSignature (SecKeyAlgorithm algorithm, NSData dataToSign, out NSError error)
		{
			if (dataToSign == null)
				throw new ArgumentNullException (nameof (dataToSign));

			IntPtr err;
			var data = SecKeyCreateSignature (Handle, algorithm.GetConstant ().Handle, dataToSign.Handle, out err);
			error = err == IntPtr.Zero ? null : new NSError (err);
			return Runtime.GetNSObject<NSData> (data, true);
		}

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		[DllImport (Constants.SecurityLibrary)]
		[return: MarshalAs (UnmanagedType.U1)]
		static extern /* Boolean */ bool SecKeyVerifySignature (/* SecKeyRef */ IntPtr key, /* SecKeyAlgorithm */ IntPtr algorithm, /* CFDataRef */ IntPtr signedData, /* CFDataRef */ IntPtr signature, /* CFErrorRef* */ out IntPtr error);

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		public bool VerifySignature (SecKeyAlgorithm algorithm, NSData signedData, NSData signature, out NSError error)
		{
			if (signedData == null)
				throw new ArgumentNullException (nameof (signedData));
			if (signature == null)
				throw new ArgumentNullException (nameof (signature));
			
			IntPtr err;
			var result = SecKeyVerifySignature (Handle, algorithm.GetConstant ().Handle, signedData.Handle, signature.Handle, out err);
			error = err == IntPtr.Zero ? null : new NSError (err);
			return result;
		}

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		[DllImport (Constants.SecurityLibrary)]
		static extern /* CFDataRef _Nullable */ IntPtr SecKeyCreateEncryptedData (/* SecKeyRef */ IntPtr key, /* SecKeyAlgorithm */ IntPtr algorithm, /* CFDataRef */ IntPtr plaintext, /* CFErrorRef* */ out IntPtr error);

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		public NSData CreateEncryptedData (SecKeyAlgorithm algorithm, NSData plaintext, out NSError error)
		{
			if (plaintext == null)
				throw new ArgumentNullException (nameof (plaintext));

			IntPtr err;
			var data = SecKeyCreateEncryptedData (Handle, algorithm.GetConstant ().Handle, plaintext.Handle, out err);
			error = err == IntPtr.Zero ? null : new NSError (err);
			return Runtime.GetNSObject<NSData> (data, true);
		}

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		[DllImport (Constants.SecurityLibrary)]
		static extern /* CFDataRef _Nullable */ IntPtr SecKeyCreateDecryptedData (/* SecKeyRef */ IntPtr key, /* SecKeyAlgorithm */ IntPtr algorithm, /* CFDataRef */ IntPtr ciphertext, /* CFErrorRef* */ out IntPtr error);

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		public NSData CreateDecryptedData (SecKeyAlgorithm algorithm, NSData ciphertext, out NSError error)
		{
			if (ciphertext == null)
				throw new ArgumentNullException (nameof (ciphertext));

			IntPtr err;
			var data = SecKeyCreateDecryptedData (Handle, algorithm.GetConstant ().Handle, ciphertext.Handle, out err);
			error = err == IntPtr.Zero ? null : new NSError (err);
			return Runtime.GetNSObject<NSData> (data, true);
		}

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		[DllImport (Constants.SecurityLibrary)]
		static extern /* CFDataRef _Nullable */ IntPtr SecKeyCopyKeyExchangeResult (/* SecKeyRef */ IntPtr privateKey, /* SecKeyAlgorithm */ IntPtr algorithm, /* SecKeyRef */ IntPtr publicKey, /* CFDictionaryRef */ IntPtr parameters, /* CFErrorRef* */ out IntPtr error);

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		public NSData GetKeyExchangeResult (SecKeyAlgorithm algorithm, SecKey publicKey, NSDictionary parameters, out NSError error)
		{
			if (publicKey == null)
				throw new ArgumentNullException (nameof (publicKey));
			if (parameters == null)
				throw new ArgumentNullException (nameof (parameters));

			IntPtr err;
			var data = SecKeyCopyKeyExchangeResult (Handle, algorithm.GetConstant ().Handle, publicKey.Handle, parameters.Handle, out err);
			error = err == IntPtr.Zero ? null : new NSError (err);
			return Runtime.GetNSObject<NSData> (data, true);
		}

#if !NET
		[Watch (3,0)][TV (10,0)][Mac (10,12)][iOS (10,0)]
#endif
		public NSData GetKeyExchangeResult (SecKeyAlgorithm algorithm, SecKey publicKey, SecKeyKeyExchangeParameter parameters, out NSError error)
		{
			return GetKeyExchangeResult (algorithm, publicKey, parameters?.Dictionary, out error);
		}

#endif
		~SecKey ()
		{
			Dispose (false);
		}

		public IntPtr Handle {
			get {
				return handle;
			}
		}

		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
		}

		protected virtual void Dispose (bool disposing)
		{
			if (handle != IntPtr.Zero){
				CFObject.CFRelease (handle);
				handle = IntPtr.Zero;
			}
		}
	}
}
