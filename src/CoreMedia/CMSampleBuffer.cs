// 
// CMSampleBuffer.cs: Implements the managed CMSampleBuffer
//
// Authors: Mono Team
//			Marek Safar (marek.safar@gmail.com)
//     
// Copyright 2010 Novell, Inc
// Copyright 2012-2014 Xamarin Inc
//

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Collections.Generic;

using Foundation;
using CoreFoundation;
using ObjCRuntime;

using OSStatus = System.nint;

#if !COREBUILD
using AudioToolbox;
using CoreVideo;
#if !MONOMAC
using UIKit;
#endif
#endif

namespace CoreMedia {

#if !NET
	[Watch (6,0)]
#endif
	public class CMSampleBuffer : ICMAttachmentBearer 
#if !COREBUILD
	, IDisposable
#endif
	{
#if !COREBUILD
		internal IntPtr handle;
		GCHandle invalidate;

		internal CMSampleBuffer (IntPtr handle)
		{
			this.handle = handle;
		}

		[Preserve (Conditional=true)]
		internal CMSampleBuffer (IntPtr handle, bool owns)
		{
			if (!owns)
				CFObject.CFRetain (handle);

			this.handle = handle;
		}
		
		~CMSampleBuffer ()
		{
			Dispose (false);
		}
		
		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
		}

		public IntPtr Handle {
			get { return handle; }
		}
	
		protected virtual void Dispose (bool disposing)
		{
			if (invalidate.IsAllocated)
				invalidate.Free ();

			if (handle != IntPtr.Zero){
				CFObject.CFRelease (handle);
				handle = IntPtr.Zero;
			}
		}

		[DllImport(Constants.CoreMediaLibrary)]
		extern static CMSampleBufferError CMAudioSampleBufferCreateWithPacketDescriptions (
			/* CFAllocatorRef */ IntPtr allocator,
			/* CMBlockBufferRef */ IntPtr dataBuffer,
			/* Boolean */ [MarshalAs (UnmanagedType.I1)] bool dataReady,
			/* CMSampleBufferMakeDataReadyCallback */ IntPtr makeDataReadyCallback,
			/* void */ IntPtr makeDataReadyRefcon,
			/* CMFormatDescriptionRef */ IntPtr formatDescription,
			/* CMItemCount */ nint numSamples,
			CMTime sbufPTS,
			/* AudioStreamPacketDescription* */ AudioStreamPacketDescription[] packetDescriptions,
			/* CMSampleBufferRef* */ out IntPtr sBufOut);

		public static CMSampleBuffer CreateWithPacketDescriptions (CMBlockBuffer dataBuffer, CMFormatDescription formatDescription, int samplesCount,
			CMTime sampleTimestamp, AudioStreamPacketDescription[] packetDescriptions, out CMSampleBufferError error)
		{
			if (formatDescription == null)
				throw new ArgumentNullException ("formatDescription");
			if (samplesCount <= 0)
				throw new ArgumentOutOfRangeException ("samplesCount");

			IntPtr buffer;
			error = CMAudioSampleBufferCreateWithPacketDescriptions (IntPtr.Zero,
				dataBuffer == null ? IntPtr.Zero : dataBuffer.handle,
				true, IntPtr.Zero, IntPtr.Zero,
				formatDescription.handle,
				samplesCount, sampleTimestamp,
				packetDescriptions,
				out buffer);

			if (error != CMSampleBufferError.None)
				return null;

			return new CMSampleBuffer (buffer, true);
		}

		[DllImport(Constants.CoreMediaLibrary)]
		unsafe static extern OSStatus CMSampleBufferCreateCopyWithNewTiming (
			/* CFAllocatorRef */ IntPtr allocator,
			/* CMSampleBufferRef */ IntPtr originalSBuf,
			/* CMItemCount */ nint numSampleTimingEntries,
			CMSampleTimingInfo* sampleTimingArray,
			/* CMSampleBufferRef* */ out IntPtr sBufCopyOut
			);

		public static CMSampleBuffer CreateWithNewTiming (CMSampleBuffer original, CMSampleTimingInfo [] timing)
		{
			OSStatus status;
			return CreateWithNewTiming (original, timing, out status);
		}

		public unsafe static CMSampleBuffer CreateWithNewTiming (CMSampleBuffer original, CMSampleTimingInfo [] timing, out OSStatus status)
		{
			if (original == null)
				throw new ArgumentNullException ("original");

			nint count = timing == null ? 0 : timing.Length;
			IntPtr handle;

			fixed (CMSampleTimingInfo *t = timing)
				if ((status = CMSampleBufferCreateCopyWithNewTiming (IntPtr.Zero, original.Handle, count, t, out handle)) != 0)
					return null;
			
			return new CMSampleBuffer (handle, true);
		}

		// CMSampleBufferCallBlockForEachSample is not bound because it's signature is problematic wrt AOT and
		// we can provide the same managed API using CMSampleBufferCallForEachSample

		[DllImport(Constants.CoreMediaLibrary)]
		unsafe static extern CMSampleBufferError CMSampleBufferCallForEachSample (
			/* CMSampleBufferRef */ IntPtr sbuf,
			CMSampleBufferCallForEachSampleCallback callback, 
		   /* void* */ IntPtr refcon);

		delegate CMSampleBufferError CMSampleBufferCallForEachSampleCallback (/* CMSampleBufferRef */ IntPtr
			sampleBuffer, int index, /* void* */ IntPtr refcon);

#if !MONOMAC
		[MonoPInvokeCallback (typeof (CMSampleBufferCallForEachSampleCallback))]
#endif
		static CMSampleBufferError ForEachSampleHandler (IntPtr sbuf, int index, IntPtr refCon)
		{
			GCHandle gch = GCHandle.FromIntPtr (refCon);
			var obj = gch.Target as Tuple<Func<CMSampleBuffer,int,CMSampleBufferError>, CMSampleBuffer>;
			if (obj == null)
				return CMSampleBufferError.RequiredParameterMissing;
			return obj.Item1 (obj.Item2, index);
		}

		public CMSampleBufferError CallForEachSample (Func<CMSampleBuffer,int,CMSampleBufferError> callback)
		{
			// it makes no sense not to provide a callback - and it also crash the app
			if (callback == null)
				throw new ArgumentNullException ("callback");

			GCHandle h = GCHandle.Alloc (Tuple.Create (callback, this));
			var result = CMSampleBufferCallForEachSample (handle, ForEachSampleHandler, (IntPtr)h);
			h.Free ();
			return result;
		}

/*
		[DllImport(Constants.CoreMediaLibrary)]
		int CMSampleBufferCopySampleBufferForRange (
		   CFAllocatorRef allocator,
		   CMSampleBufferRef sbuf,
		   CFRange sampleRange,
		   CMSampleBufferRef *sBufOut
		);

		[DllImport(Constants.CoreMediaLibrary)]
		int CMSampleBufferCreate (
		   CFAllocatorRef allocator,
		   CMBlockBufferRef dataBuffer,
		   Boolean dataReady,
		   CMSampleBufferMakeDataReadyCallback makeDataReadyCallback,
		   void *makeDataReadyRefcon,
		   CMFormatDescriptionRef formatDescription,
		   int numSamples,
		   int numSampleTimingEntries,
		   const CMSampleTimingInfo *sampleTimingArray,
		   int numSampleSizeEntries,
		   const uint *sampleSizeArray,
		   CMSampleBufferRef *sBufOut
		);

		[DllImport(Constants.CoreMediaLibrary)]
		int CMSampleBufferCreateCopy (
		   CFAllocatorRef allocator,
		   CMSampleBufferRef sbuf,
		   CMSampleBufferRef *sbufCopyOut
		);
		*/

		[DllImport(Constants.CoreMediaLibrary)]
		static extern /* OSStatus */ CMSampleBufferError CMSampleBufferCreateForImageBuffer (
			/* CFAllocatorRef */ IntPtr allocator,
			/* CVImageBufferRef */ IntPtr imageBuffer,
			/* Boolean */ [MarshalAs (UnmanagedType.I1)] bool dataReady,
			/* CMSampleBufferMakeDataReadyCallback */ IntPtr makeDataReadyCallback,
			/* void* */ IntPtr makeDataReadyRefcon,
			/* CMVideoFormatDescriptionRef */ IntPtr formatDescription,
			/* const CMSampleTimingInfo* */ ref CMSampleTimingInfo sampleTiming,
			/* CMSampleBufferRef* */ out IntPtr bufOut
		);

		public static CMSampleBuffer CreateForImageBuffer (CVImageBuffer imageBuffer, bool dataReady, CMVideoFormatDescription formatDescription, CMSampleTimingInfo sampleTiming, out CMSampleBufferError error)
		{
			if (imageBuffer == null)
				throw new ArgumentNullException ("imageBuffer");
			if (formatDescription == null)
				throw new ArgumentNullException ("formatDescription");

			IntPtr buffer;
			error = CMSampleBufferCreateForImageBuffer (IntPtr.Zero,
				imageBuffer.handle, dataReady,
				IntPtr.Zero, IntPtr.Zero,
				formatDescription.handle,
				ref sampleTiming,
				out buffer);

			if (error != CMSampleBufferError.None)
				return null;

			return new CMSampleBuffer (buffer, true);
		}

		[DllImport(Constants.CoreMediaLibrary)]
		[return: MarshalAs (UnmanagedType.I1)]
		extern static /* Boolean */ bool CMSampleBufferDataIsReady (/* CMSampleBufferRef */ IntPtr sbuf);
		
		public bool DataIsReady
		{
			get
			{
				return CMSampleBufferDataIsReady (handle);
			}
		}

		/*[DllImport(Constants.CoreMediaLibrary)]
		int CMSampleBufferGetAudioBufferListWithRetainedBlockBuffer (
		   CMSampleBufferRef sbuf,
		   uint *bufferListSizeNeededOut,
		   AudioBufferList *bufferListOut,
		   uint bufferListSize,
		   CFAllocatorRef bbufStructAllocator,
		   CFAllocatorRef bbufMemoryAllocator,
		   uint32_t flags,
		   CMBlockBufferRef *blockBufferOut
		);

		[DllImport(Constants.CoreMediaLibrary)]
		int CMSampleBufferGetAudioStreamPacketDescriptions (
		   CMSampleBufferRef sbuf,
		   uint packetDescriptionsSize,
		   AudioStreamPacketDescription *packetDescriptionsOut,
		   uint *packetDescriptionsSizeNeededOut
		);

		[DllImport(Constants.CoreMediaLibrary)]
		int CMSampleBufferGetAudioStreamPacketDescriptionsPtr (
		   CMSampleBufferRef sbuf,
		   const AudioStreamPacketDescription **packetDescriptionsPtrOut,
		   uint *packetDescriptionsSizeOut
		);*/

		[DllImport(Constants.CoreMediaLibrary)]
		extern static /* CMBlockBufferRef */ IntPtr CMSampleBufferGetDataBuffer (/* CMSampleBufferRef */ IntPtr sbuf);
		
		public CMBlockBuffer GetDataBuffer ()
		{
			var blockHandle = CMSampleBufferGetDataBuffer (handle);			
			if (blockHandle == IntPtr.Zero)
			{
				return null;
			}
			else
			{
				return new CMBlockBuffer (blockHandle, false);
			}
		}

		[DllImport(Constants.CoreMediaLibrary)]
		extern static CMTime CMSampleBufferGetDecodeTimeStamp (/* CMSampleBufferRef */ IntPtr sbuf);
		
		public CMTime DecodeTimeStamp
		{
			get
			{
				return CMSampleBufferGetDecodeTimeStamp (handle);
			}
		}

		[DllImport(Constants.CoreMediaLibrary)]
		extern static CMTime CMSampleBufferGetDuration (/* CMSampleBufferRef */ IntPtr sbuf);
		
		public CMTime Duration
		{
			get
			{
				return CMSampleBufferGetDuration (handle);
			}
		}

		[DllImport(Constants.CoreMediaLibrary)]
		extern static /* CMFormatDescriptionRef */ IntPtr CMSampleBufferGetFormatDescription (/* CMSampleBufferRef */ IntPtr sbuf);

		public CMAudioFormatDescription GetAudioFormatDescription ()
		{
			var descHandle = CMSampleBufferGetFormatDescription (handle);
			if (descHandle == IntPtr.Zero)
				return null;

			return new CMAudioFormatDescription (descHandle, false);
		}

		public CMVideoFormatDescription GetVideoFormatDescription ()
		{
			var descHandle = CMSampleBufferGetFormatDescription (handle);
			if (descHandle == IntPtr.Zero)
				return null;

			return new CMVideoFormatDescription (descHandle, false);
		}

		[DllImport(Constants.CoreMediaLibrary)]
		extern static /* CVImageBufferRef */ IntPtr CMSampleBufferGetImageBuffer (/* CMSampleBufferRef */ IntPtr sbuf);

		public CVImageBuffer GetImageBuffer ()
		{
			IntPtr ib = CMSampleBufferGetImageBuffer (handle);
			if (ib == IntPtr.Zero)
				return null;

			var ibt = CFType.GetTypeID (ib);
			if (ibt == CVPixelBuffer.GetTypeID ())
				return new CVPixelBuffer (ib, false);
			return new CVImageBuffer (ib, false);
		}

		[DllImport(Constants.CoreMediaLibrary)]
		extern static /* CMItemCount */ nint CMSampleBufferGetNumSamples (/* CMSampleBufferRef */ IntPtr sbuf);
		
		public nint NumSamples
		{
			get
			{
				return CMSampleBufferGetNumSamples (handle);
			}
		}

		[DllImport(Constants.CoreMediaLibrary)]
		extern static CMTime CMSampleBufferGetOutputDecodeTimeStamp (/* CMSampleBufferRef */ IntPtr sbuf);
		
		public CMTime OutputDecodeTimeStamp
		{
			get
			{
				return CMSampleBufferGetOutputDecodeTimeStamp (handle);
			}
		}

		[DllImport(Constants.CoreMediaLibrary)]
		extern static CMTime CMSampleBufferGetOutputDuration (/* CMSampleBufferRef */ IntPtr sbuf);
		
		public CMTime OutputDuration
		{
			get
			{
				return CMSampleBufferGetOutputDuration (handle);
			}
		}

		[DllImport(Constants.CoreMediaLibrary)]
		extern static CMTime CMSampleBufferGetOutputPresentationTimeStamp (/* CMSampleBufferRef */ IntPtr sbuf);
		
		public CMTime OutputPresentationTimeStamp
		{
			get
			{
				return CMSampleBufferGetOutputPresentationTimeStamp (handle);
			}
		}
		
		[DllImport(Constants.CoreMediaLibrary)]
		extern static /* OSStatus */ CMSampleBufferError CMSampleBufferSetOutputPresentationTimeStamp (/* CMSampleBufferRef */ IntPtr sbuf, CMTime outputPresentationTimeStamp);

		/*[DllImport(Constants.CoreMediaLibrary)]
		int CMSampleBufferGetOutputSampleTimingInfoArray (
		   CMSampleBufferRef sbuf,
		   int timingArrayEntries,
		   CMSampleTimingInfo *timingArrayOut,
		   int *timingArrayEntriesNeededOut
		);*/

		[DllImport(Constants.CoreMediaLibrary)]
		extern static CMTime CMSampleBufferGetPresentationTimeStamp (/* CMSampleBufferRef */ IntPtr sbuf);
		
		public CMTime PresentationTimeStamp {
			get {
				return CMSampleBufferGetPresentationTimeStamp (handle);
			}
			set {
				var result = CMSampleBufferSetOutputPresentationTimeStamp (handle, value);
				if (result != 0)
					throw new ArgumentException (result.ToString ());
			}
		}

		[DllImport(Constants.CoreMediaLibrary)]
		extern static /* CFArrayRef */ IntPtr CMSampleBufferGetSampleAttachmentsArray (/* CMSampleBufferRef */ IntPtr sbuf, /* Boolean */ [MarshalAs (UnmanagedType.I1)] bool createIfNecessary);
		
		public CMSampleBufferAttachmentSettings [] GetSampleAttachments (bool createIfNecessary)
		{
			var cfArrayRef = CMSampleBufferGetSampleAttachmentsArray (handle, createIfNecessary);
			if (cfArrayRef == IntPtr.Zero)
			{
				return new CMSampleBufferAttachmentSettings [0];
			}
			else
			{
				return NSArray.ArrayFromHandle (cfArrayRef, h => new CMSampleBufferAttachmentSettings ((NSMutableDictionary) Runtime.GetNSObject (h)));
			}
		}

		[DllImport(Constants.CoreMediaLibrary)]
		extern static /* size_t */ nuint CMSampleBufferGetSampleSize (/* CMSampleBufferRef */ IntPtr sbuf, /* CMItemIndex */ nint sampleIndex);
		
		public nuint GetSampleSize (nint sampleIndex)
		{
			return CMSampleBufferGetSampleSize (handle, sampleIndex);
		}
		
		/*[DllImport(Constants.CoreMediaLibrary)]
		int CMSampleBufferGetSampleSizeArray (
		   CMSampleBufferRef sbuf,
		   int sizeArrayEntries,
		   uint *sizeArrayOut,
		   int *sizeArrayEntriesNeededOut
		);
		
		[DllImport(Constants.CoreMediaLibrary)]
		int CMSampleBufferGetSampleTimingInfo (
		   CMSampleBufferRef sbuf,
		   int sampleIndex,
		   CMSampleTimingInfo *timingInfoOut
		);
		*/
		
		[DllImport(Constants.CoreMediaLibrary)]
		unsafe static extern OSStatus CMSampleBufferGetSampleTimingInfoArray (
			/* CMSampleBufferRef */ IntPtr sbuf,
			/* CMItemCount */ nint timingArrayEntries,
			CMSampleTimingInfo* timingArrayOut,
			/* CMItemCount* */ out nint timingArrayEntriesNeededOut
		);

		public CMSampleTimingInfo [] GetSampleTimingInfo ()
		{
			OSStatus status;
			return GetSampleTimingInfo (out status);
		}

		public unsafe CMSampleTimingInfo [] GetSampleTimingInfo (out OSStatus status) {
			nint count;

			status = 0;

			if (handle == IntPtr.Zero)
				return null;

			if ((status = CMSampleBufferGetSampleTimingInfoArray (handle, 0, null, out count)) != 0)
				return null;

			CMSampleTimingInfo [] pInfo = new CMSampleTimingInfo [count];

			if (count == 0)
				return pInfo;

			fixed (CMSampleTimingInfo* info = pInfo)
				if ((status = CMSampleBufferGetSampleTimingInfoArray (handle, count, info, out count)) != 0)
					return null;

			return pInfo;
		}

		static string OSStatusToString (OSStatus status)
		{
			return new NSError (NSError.OsStatusErrorDomain, status).LocalizedDescription;
		}

		[DllImport(Constants.CoreMediaLibrary)]
		extern static /* size_t */ nuint CMSampleBufferGetTotalSampleSize (/* CMSampleBufferRef */ IntPtr sbuf);
		
		public nuint TotalSampleSize
		{
			get
			{
				return CMSampleBufferGetTotalSampleSize (handle);
			}
		}
		
		[DllImport(Constants.CoreMediaLibrary)]
		extern static /* CFTypeID */ nint CMSampleBufferGetTypeID ();
		
		public static nint GetTypeID ()
		{
			return CMSampleBufferGetTypeID ();
		}
		
		[DllImport(Constants.CoreMediaLibrary)]
		extern static /* OSStatus */ CMSampleBufferError CMSampleBufferInvalidate (/* CMSampleBufferRef */ IntPtr sbuf);

		public CMSampleBufferError Invalidate ()
		{
			return CMSampleBufferInvalidate (handle);
		}
		
		[DllImport(Constants.CoreMediaLibrary)]
		[return: MarshalAs (UnmanagedType.I1)]
		extern static /* Boolean */ bool CMSampleBufferIsValid (/* CMSampleBufferRef */ IntPtr sbuf);
		
		public bool IsValid
		{
			get
			{
				return CMSampleBufferIsValid (handle);
			}
		}
		
		[DllImport(Constants.CoreMediaLibrary)]
		extern static /* OSStatus */ CMSampleBufferError CMSampleBufferMakeDataReady (IntPtr handle);

		public CMSampleBufferError MakeDataReady ()
		{
			return CMSampleBufferMakeDataReady (handle);
		}
		
		[DllImport(Constants.CoreMediaLibrary)]
		extern static /* OSStatus */ CMSampleBufferError CMSampleBufferSetDataBuffer (IntPtr handle, IntPtr dataBufferHandle);
		
		public CMSampleBufferError SetDataBuffer (CMBlockBuffer dataBuffer)
		{
			var dataBufferHandle = dataBuffer == null ? IntPtr.Zero : dataBuffer.handle;
			return CMSampleBufferSetDataBuffer (handle, dataBufferHandle);
		}
		
		/*[DllImport(Constants.CoreMediaLibrary)]
		int CMSampleBufferSetDataBufferFromAudioBufferList (
		   CMSampleBufferRef sbuf,
		   CFAllocatorRef bbufStructAllocator,
		   CFAllocatorRef bbufMemoryAllocator,
		   uint32_t flags,
		   const AudioBufferList *bufferList
		);*/
		
		[DllImport(Constants.CoreMediaLibrary)]
		extern static /* OSStatus */ CMSampleBufferError CMSampleBufferSetDataReady (/* CMSampleBufferRef */ IntPtr sbuf);

		public CMSampleBufferError SetDataReady ()
		{
			return CMSampleBufferSetDataReady (handle);
		}
		
#if false
		// new in iOS 8 beta 5 - but the signature is not easy to bind with the AOT limitation, i.e. MonoPInvokeCallback
		[DllImport(Constants.CoreMediaLibrary)]
		extern static /* OSStatus */ CMSampleBufferError CMSampleBufferSetInvalidateHandler (
			/* CMSampleBufferRef */ IntPtr sbuf,
			/* CMSampleBufferInvalidateHandler */ IntPtr invalidateHandler);
#endif
		// however there was already a similar call that we did not bound (not sure why) 
		// and can provide the same feature (since iOS 4 not 8.0)
		[DllImport(Constants.CoreMediaLibrary)]
		extern static /* OSStatus */ CMSampleBufferError CMSampleBufferSetInvalidateCallback (
			/* CMSampleBufferRef */ IntPtr sbuf,
			/* CMSampleBufferInvalidateCallback */ CMSampleBufferInvalidateCallback invalidateCallback,
			/* uint64_t */ ulong invalidateRefCon);

		delegate void CMSampleBufferInvalidateCallback (/* CMSampleBufferRef */ IntPtr sbuf, 
			/* uint64_t */ ulong invalidateRefCon);

		static CMSampleBufferInvalidateCallback invalidate_handler = InvalidateHandler;

#if !MONOMAC
		[MonoPInvokeCallback (typeof (CMSampleBufferInvalidateCallback))]
#endif
		static void InvalidateHandler (IntPtr sbuf, ulong invalidateRefCon)
		{
			GCHandle gch = GCHandle.FromIntPtr ((IntPtr) invalidateRefCon);
			var obj = gch.Target as Tuple<Action<CMSampleBuffer>, CMSampleBuffer>;
			if (obj != null)
				obj.Item1 (obj.Item2);
		}

		public CMSampleBufferError SetInvalidateCallback (Action<CMSampleBuffer> invalidateHandler)
		{
			if (invalidateHandler == null) {
				if (invalidate.IsAllocated)
					invalidate.Free ();

				return CMSampleBufferSetInvalidateCallback (handle, null, 0);
			}

			// only one callback can be assigned - and ObjC does not let you re-assign a different one,
			// i.e. it returns RequiredParameterMissing, we could fake this but that would only complexify 
			// porting apps (different behavior)
			if (invalidate.IsAllocated)
				return CMSampleBufferError.RequiredParameterMissing;

			invalidate = GCHandle.Alloc (Tuple.Create (invalidateHandler, this));
			return CMSampleBufferSetInvalidateCallback (handle, invalidate_handler, (ulong)(IntPtr)invalidate);
		}
							
		[DllImport(Constants.CoreMediaLibrary)]
		extern static /* OSStatus */ CMSampleBufferError CMSampleBufferTrackDataReadiness (/* CMSampleBufferRef */ IntPtr sbuf, /* CMSampleBufferRef */ IntPtr sbufToTrack);

		public CMSampleBufferError TrackDataReadiness (CMSampleBuffer bufferToTrack)
		{
			var handleToTrack = bufferToTrack == null ? IntPtr.Zero : bufferToTrack.handle;
			return CMSampleBufferTrackDataReadiness (handle, handleToTrack);
		}

#if !NET
		[iOS (7,0)][Mac (10,9)]
#endif
		[DllImport(Constants.CoreMediaLibrary)]
		extern static /* OSStatus */ CMSampleBufferError CMSampleBufferCopyPCMDataIntoAudioBufferList (/* CMSampleBufferRef */ IntPtr sbuf, /* int32_t */ int frameOffset, /* int32_t */ int numFrames, /* AudioBufferList* */ IntPtr bufferList);

#if !NET
		[iOS (7,0)][Mac (10,9)]
#endif
		public CMSampleBufferError CopyPCMDataIntoAudioBufferList (int frameOffset, int numFrames, AudioBuffers bufferList)
		{
			if (bufferList == null)
				throw new ArgumentNullException ("bufferList");

			return CMSampleBufferCopyPCMDataIntoAudioBufferList (handle, frameOffset, numFrames, (IntPtr) bufferList);
		}

#if !NET
		[iOS (8,0)][Mac (10,10)]
#endif
		[DllImport(Constants.CoreMediaLibrary)]
		extern static /* OSStatus */ CMSampleBufferError CMAudioSampleBufferCreateReadyWithPacketDescriptions (
			/* CFAllocatorRef */ IntPtr allocator,
			/* CMBlockBufferRef */ IntPtr dataBuffer,
			/* CMFormatDescriptionRef */ IntPtr formatDescription,
			/* CMItemCount */ nint numSamples,
			CMTime sbufPTS,
			/* AudioStreamPacketDescription* */ AudioStreamPacketDescription[] packetDescriptions,
			/* CMSampleBufferRef* */ out IntPtr sBufOut);

#if !NET
		[iOS (8,0)][Mac (10,10)]
#endif
		public static CMSampleBuffer CreateReadyWithPacketDescriptions (CMBlockBuffer dataBuffer, CMFormatDescription formatDescription, int samplesCount,
			CMTime sampleTimestamp, AudioStreamPacketDescription[] packetDescriptions, out CMSampleBufferError error)
		{
			if (dataBuffer == null)
				throw new ArgumentNullException ("dataBuffer");
			if (formatDescription == null)
				throw new ArgumentNullException ("formatDescription");
			if (samplesCount <= 0)
				throw new ArgumentOutOfRangeException ("samplesCount");

			IntPtr buffer;
			error = CMAudioSampleBufferCreateReadyWithPacketDescriptions (IntPtr.Zero, dataBuffer.handle,
				formatDescription.handle, samplesCount, sampleTimestamp, packetDescriptions, out buffer);

			if (error != CMSampleBufferError.None)
				return null;

			return new CMSampleBuffer (buffer, true);
		}

#if !NET
		[iOS (8,0)][Mac (10,10)]
#endif
		[DllImport(Constants.CoreMediaLibrary)]
		extern static /* OSStatus */ CMSampleBufferError CMSampleBufferCreateReady (
			/* CFAllocatorRef */ IntPtr allocator,
			/* CMBlockBufferRef */ IntPtr dataBuffer,
			/* CMFormatDescriptionRef */ IntPtr formatDescription,	// can be null
			/* CMItemCount */ nint numSamples,						// can be 0
			/* CMItemCount */ nint numSampleTimingEntries,			// 0, 1 or numSamples
			CMSampleTimingInfo[] sampleTimingArray,					// can be null
			/* CMItemCount */ nint numSampleSizeEntries,			// 0, 1 or numSamples
			/* size_t* */ nuint[] sampleSizeArray,					// can be null
			/* CMSampleBufferRef* */ out IntPtr sBufOut);

#if !NET
		[iOS (8,0)][Mac (10,10)]
#endif
		public static CMSampleBuffer CreateReady (CMBlockBuffer dataBuffer, CMFormatDescription formatDescription, 
			int samplesCount, CMSampleTimingInfo[] sampleTimingArray, nuint[] sampleSizeArray, 
			out CMSampleBufferError error)
		{
			if (dataBuffer == null)
				throw new ArgumentNullException ("dataBuffer");
			if (samplesCount < 0)
				throw new ArgumentOutOfRangeException ("samplesCount");

			IntPtr buffer;
			var fdh = formatDescription == null ? IntPtr.Zero : formatDescription.Handle;
			var timingCount = sampleTimingArray == null ? 0 : sampleTimingArray.Length;
			var sizeCount = sampleSizeArray == null ? 0 : sampleSizeArray.Length;
			error = CMSampleBufferCreateReady (IntPtr.Zero, dataBuffer.handle, fdh, samplesCount, timingCount,
				sampleTimingArray, sizeCount, sampleSizeArray, out buffer);

			if (error != CMSampleBufferError.None)
				return null;

			return new CMSampleBuffer (buffer, true);
		}

#if !NET
		[iOS (8,0)][Mac (10,10)]
#endif
		[DllImport(Constants.CoreMediaLibrary)]
		extern static /* OSStatus */ CMSampleBufferError CMSampleBufferCreateReadyWithImageBuffer (
			/* CFAllocatorRef */ IntPtr allocator,
			/* CVImageBufferRef */ IntPtr imageBuffer,
			/* CMFormatDescriptionRef */ IntPtr formatDescription,  // not null
			/* const CMSampleTimingInfo * CM_NONNULL */ ref CMSampleTimingInfo sampleTiming,
			/* CMSampleBufferRef* */ out IntPtr sBufOut);

#if !XAMCORE_4_0
#if !WATCH
#if !NET
		[Obsolete ("Use the 'CreateReadyWithImageBuffer' overload with a single ref, not array, 'CMSampleTimingInfo' parameter.")]
		[iOS (8,0)][Mac (10,10)]
#else
		[Obsolete ("Use the 'CreateReadyWithImageBuffer' overload with a single ref, not array, 'CMSampleTimingInfo' parameter.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#endif
		public static CMSampleBuffer CreateReadyWithImageBuffer (CVImageBuffer imageBuffer, 
			CMFormatDescription formatDescription, CMSampleTimingInfo[] sampleTiming, out CMSampleBufferError error)
		{
			if (sampleTiming == null)
				throw new ArgumentNullException (nameof (sampleTiming));
			if (sampleTiming.Length != 1)
				throw new ArgumentException ("Only a single sample is allowed.", nameof (sampleTiming));
			return CreateReadyWithImageBuffer (imageBuffer, formatDescription, sampleTiming, out error);
		}
#endif // !WATCH
#endif // !XAMCORE_4_0
#if !NET
		[iOS (8,0)][Mac (10,10)]
#endif
		public static CMSampleBuffer CreateReadyWithImageBuffer (CVImageBuffer imageBuffer,
			CMFormatDescription formatDescription, ref CMSampleTimingInfo sampleTiming, out CMSampleBufferError error)
		{
			if (imageBuffer == null)
				throw new ArgumentNullException (nameof (imageBuffer));
			if (formatDescription == null)
				throw new ArgumentNullException (nameof (formatDescription));

			IntPtr buffer;
			error = CMSampleBufferCreateReadyWithImageBuffer (IntPtr.Zero, imageBuffer.handle,
				formatDescription.Handle, ref sampleTiming, out buffer);

			if (error != CMSampleBufferError.None)
				return null;

			return new CMSampleBuffer (buffer, true);
		}
#endif // !COREBUILD
	}

#if !COREBUILD
	public partial class CMSampleBufferAttachmentSettings : DictionaryContainer {

		internal CMSampleBufferAttachmentSettings (NSMutableDictionary dictionary)
			: base (dictionary)
		{
		}

		public bool? NotSync {
			get {
				return GetBoolValue (CMSampleAttachmentKey.NotSync);
			}
			set {
				SetBooleanValue (CMSampleAttachmentKey.NotSync, value);
			}
		}

		public bool? PartialSync {
			get {
				return GetBoolValue (CMSampleAttachmentKey.PartialSync);
			}
			set {
				SetBooleanValue (CMSampleAttachmentKey.PartialSync, value);
			}
		}

		public bool? RedundantCoding {
			get {
				return GetBoolValue (CMSampleAttachmentKey.HasRedundantCoding);
			}
			set {
				SetBooleanValue (CMSampleAttachmentKey.HasRedundantCoding, value);
			}
		}

		public bool? DependedOnByOthers {
			get {
				return GetBoolValue (CMSampleAttachmentKey.IsDependedOnByOthers);
			}
			set {
				SetBooleanValue (CMSampleAttachmentKey.IsDependedOnByOthers, value);
			}
		}

		public bool? DependsOnOthers {
			get {
				return GetBoolValue (CMSampleAttachmentKey.DependsOnOthers);
			}
			set {
				SetBooleanValue (CMSampleAttachmentKey.DependsOnOthers, value);
			}
		}

		public bool? EarlierDisplayTimesAllowed {
			get {
				return GetBoolValue (CMSampleAttachmentKey.EarlierDisplayTimesAllowed);
			}
			set {
				SetBooleanValue (CMSampleAttachmentKey.EarlierDisplayTimesAllowed, value);
			}
		}

		public bool? DisplayImmediately {
			get {
				return GetBoolValue (CMSampleAttachmentKey.DisplayImmediately);
			}
			set {
				SetBooleanValue (CMSampleAttachmentKey.DisplayImmediately, value);
			}
		}

		public bool? DoNotDisplay {
			get {
				return GetBoolValue (CMSampleAttachmentKey.DoNotDisplay);
			}
			set {
				SetBooleanValue (CMSampleAttachmentKey.DoNotDisplay, value);
			}
		}

		public bool? ResetDecoderBeforeDecoding {
			get {
				return GetBoolValue (CMSampleAttachmentKey.ResetDecoderBeforeDecoding);
			}
			set {
				SetBooleanValue (CMSampleAttachmentKey.ResetDecoderBeforeDecoding, value);
			}
		}

		public bool? DrainAfterDecoding {
			get {
				return GetBoolValue (CMSampleAttachmentKey.DrainAfterDecoding);
			}
			set {
				SetBooleanValue (CMSampleAttachmentKey.DrainAfterDecoding, value);
			}
		}

		public bool? Reverse {
			get {
				return GetBoolValue (CMSampleAttachmentKey.Reverse);
			}
			set {
				SetBooleanValue (CMSampleAttachmentKey.Reverse, value);
			}
		}

		public bool? FillDiscontinuitiesWithSilence {
			get {
				return GetBoolValue (CMSampleAttachmentKey.FillDiscontinuitiesWithSilence);
			}
			set {
				SetBooleanValue (CMSampleAttachmentKey.FillDiscontinuitiesWithSilence, value);
			}
		}

		public bool? EmptyMedia {
			get {
				return GetBoolValue (CMSampleAttachmentKey.EmptyMedia);
			}
			set {
				SetBooleanValue (CMSampleAttachmentKey.EmptyMedia, value);
			}
		}

		public bool? PermanentEmptyMedia {
			get {
				return GetBoolValue (CMSampleAttachmentKey.PermanentEmptyMedia);
			}
			set {
				SetBooleanValue (CMSampleAttachmentKey.PermanentEmptyMedia, value);
			}
		}

		public bool? DisplayEmptyMediaImmediately {
			get {
				return GetBoolValue (CMSampleAttachmentKey.DisplayEmptyMediaImmediately);
			}
			set {
				SetBooleanValue (CMSampleAttachmentKey.DisplayEmptyMediaImmediately, value);
			}
		}

		public bool? EndsPreviousSampleDuration {
			get {
				return GetBoolValue (CMSampleAttachmentKey.EndsPreviousSampleDuration);
			}
			set {
				SetBooleanValue (CMSampleAttachmentKey.EndsPreviousSampleDuration, value);
			}
		}

#if !MONOMAC
		public string DroppedFrameReason {
			get {
				return GetStringValue (CMSampleAttachmentKey.DroppedFrameReason);
			}
		}

#if !WATCH
#if !NET
		[iOS (9,0)]
#endif
		public LensStabilizationStatus StillImageLensStabilizationStatus {
			get {
				string reason = GetStringValue (CMSampleAttachmentKey.StillImageLensStabilizationInfo);
				if (reason == null)
					return LensStabilizationStatus.None;

				if (reason == CMSampleAttachmentKey.BufferLensStabilizationInfo_Active)
					return LensStabilizationStatus.Active;
				if (reason == CMSampleAttachmentKey.BufferLensStabilizationInfo_OutOfRange)
					return LensStabilizationStatus.OutOfRange;
				if (reason == CMSampleAttachmentKey.BufferLensStabilizationInfo_Unavailable)
					return LensStabilizationStatus.Unavailable;
				if (reason == CMSampleAttachmentKey.BufferLensStabilizationInfo_Off)
					return LensStabilizationStatus.Off;

				return LensStabilizationStatus.None;
			}
		}
#endif // !WATCH
#endif // !MONOMAC
	}
#endif
}
